using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using Xunit;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;
using static ShiftYar.Domain.Enums.UserModel.UserEnums;

namespace ShiftYar.Application.Tests;

public class FairWorkedHoursDistributionTests
{
    [Fact]
    public void Optimize_BalancesNightAndEffectiveHours_AmongIdenticalRotatingUsers()
    {
        var start = new DateTime(2026, 8, 23);
        var days = 21;

        var users = Enumerable.Range(1, 6).Select(i => new UserConstraint
        {
            UserId = i,
            Gender = i % 2 == 0 ? UserGender.Female : UserGender.Male,
            SpecialtyId = 10,
            IsActive = true,
            ShiftType = ShiftTypes.RotatingShift,
            ShiftSubType = ShiftSubTypes.ThreeShifts,
            AllowedShiftLabels = [ShiftLabel.Morning, ShiftLabel.Evening, ShiftLabel.Night],
            MaxConsecutiveShifts = 30,
            MinRestDaysBetweenShifts = 0,
            MaxShiftsPerWeek = 7,
            IncludedInProductivityPlan = true,
            ProductivityRequiredHours = 200m,
            OvertimeConsent = true
        }).ToList();

        var constraints = new ShiftConstraints
        {
            DepartmentId = 1,
            StartDate = start,
            EndDate = start.AddDays(days - 1),
            UserConstraints = users,
            ShiftRequirements =
            [
                Shift(1, ShiftLabel.Morning, 6, TimeSpan.FromHours(8), TimeSpan.FromHours(14)),
                Shift(2, ShiftLabel.Evening, 6, TimeSpan.FromHours(14), TimeSpan.FromHours(20)),
                Shift(3, ShiftLabel.Night, 12, TimeSpan.FromHours(20), TimeSpan.FromHours(8))
            ],
            HardRules = new HardRuleSet
            {
                ForbidDuplicateDailyAssignments = true,
                EnforceMaxShiftsPerDay = true,
                EnforceMinRestDays = false,
                EnforceMaxConsecutiveShifts = false,
                EnforceSpecialtyCapacity = true,
                EnforceProductivityHours = false
            },
            GlobalConstraints = new GlobalConstraints { MaxShiftsPerDay = 1 },
            SoftWeights = new SoftRuleWeights
            {
                FairShiftCountBalanceWeight = 2.0,
                FairWorkedHoursBalanceWeight = 3.0,
                FairNightShiftBalanceWeight = 3.0,
                ShiftLabelBalanceWeight = 2.0,
                NightShiftDistributionBySeniorityWeight = 2.0
            }
        };

        var parameters = new SimulatedAnnealingParameters
        {
            InitialTemperature = 500,
            FinalTemperature = 0.1,
            CoolingRate = 0.93,
            MaxIterations = 4000,
            MaxIterationsWithoutImprovement = 500,
            PenaltyWeight = 1000
        };

        var bestNightSpread = int.MaxValue;
        var bestHourSpread = double.MaxValue;

        for (var run = 0; run < 3; run++)
        {
            var solution = new SimulatedAnnealingScheduler(constraints, parameters).Optimize();

            var nightCounts = users
                .Select(u => solution.GetUserAllAssignments(u.UserId).Count(a => a.ShiftLabel == ShiftLabel.Night))
                .ToList();
            var nightSpread = nightCounts.Max() - nightCounts.Min();
            bestNightSpread = Math.Min(bestNightSpread, nightSpread);

            // تقریبی: هر شب ~۱۸ ساعت مؤثر، هر روز ~۶
            var approxHours = users.Select(u =>
            {
                var ua = solution.GetUserAllAssignments(u.UserId);
                return ua.Sum(a => a.ShiftLabel == ShiftLabel.Night ? 18.0 : 6.0);
            }).ToList();
            var hourSpread = approxHours.Max() - approxHours.Min();
            bestHourSpread = Math.Min(bestHourSpread, hourSpread);

            Assert.True(
                nightSpread <= 4,
                $"Run {run}: night imbalance — nights=[{string.Join(",", nightCounts)}] spread={nightSpread}");
        }

        Assert.True(bestNightSpread <= 3, $"Best night spread too high: {bestNightSpread}");
        Assert.True(bestHourSpread <= 48, $"Best hour spread too high: {bestHourSpread}");
    }

    private static ShiftRequirement Shift(int id, ShiftLabel label, double hours, TimeSpan start, TimeSpan end) => new()
    {
        ShiftId = id,
        ShiftLabel = label,
        DepartmentId = 1,
        DurationHours = hours,
        StartTime = start,
        EndTime = end,
        SpecialtyRequirements =
        [
            new SpecialtyRequirement { SpecialtyId = 10, RequiredTotalCount = 1 }
        ]
    };
}
