using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using Xunit;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;
using static ShiftYar.Domain.Enums.UserModel.UserEnums;

namespace ShiftYar.Application.Tests;

public class FairDistributionTests
{
    [Fact]
    public void Optimize_DistributesShiftsFairly_AmongIdenticalRotatingUsers()
    {
        var start = new DateTime(2026, 8, 23);
        var days = 30;

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
            MaxShiftsPerWeek = 7
        }).ToList();

        var constraints = new ShiftConstraints
        {
            DepartmentId = 1,
            StartDate = start,
            EndDate = start.AddDays(days - 1),
            UserConstraints = users,
            ShiftRequirements =
            [
                Shift(1, ShiftLabel.Morning),
                Shift(2, ShiftLabel.Evening),
                Shift(3, ShiftLabel.Night)
            ],
            HardRules = new HardRuleSet
            {
                ForbidDuplicateDailyAssignments = true,
                EnforceMaxShiftsPerDay = true,
                EnforceMinRestDays = false,
                EnforceMaxConsecutiveShifts = false,
                EnforceSpecialtyCapacity = true
            },
            GlobalConstraints = new GlobalConstraints { MaxShiftsPerDay = 1 },
            SoftWeights = new SoftRuleWeights
            {
                FairShiftCountBalanceWeight = 2.0,
                ExtraShiftRotationWeight = 1.0,
                ShiftLabelBalanceWeight = 1.0
            }
        };

        var parameters = new SimulatedAnnealingParameters
        {
            InitialTemperature = 400,
            FinalTemperature = 0.1,
            CoolingRate = 0.93,
            MaxIterations = 3000,
            MaxIterationsWithoutImprovement = 400,
            PenaltyWeight = 1000
        };

        for (var run = 0; run < 3; run++)
        {
            var solution = new SimulatedAnnealingScheduler(constraints, parameters).Optimize();

            var counts = users
                .Select(u => solution.GetUserAllAssignments(u.UserId).Count)
                .ToList();

            // 90 انتساب بین 6 کاربر همسان → میانگین 15؛ پراکندگی باید محدود باشد
            var spread = counts.Max() - counts.Min();
            Assert.True(
                spread <= 6,
                $"Run {run}: unfair distribution — counts=[{string.Join(",", counts)}] spread={spread}");
        }
    }

    private static ShiftRequirement Shift(int id, ShiftLabel label) => new()
    {
        ShiftId = id,
        ShiftLabel = label,
        DepartmentId = 1,
        DurationHours = 8,
        SpecialtyRequirements =
        [
            new SpecialtyRequirement { SpecialtyId = 10, RequiredTotalCount = 1 }
        ]
    };
}
