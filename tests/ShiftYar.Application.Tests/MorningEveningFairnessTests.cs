using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using Xunit;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;
using static ShiftYar.Domain.Enums.UserModel.UserEnums;

namespace ShiftYar.Application.Tests;

public class MorningEveningFairnessTests
{
    [Fact]
    public void Optimize_DoesNotGiveOneUserFarMoreMorningThanPeers()
    {
        var start = new DateTime(2026, 6, 22);
        var users = Enumerable.Range(1, 6).Select(i => MakeUser(i)).ToList();
        var constraints = BuildConstraints(start, days: 28, users);
        var parameters = SoftSaParams();

        var solution = new SimulatedAnnealingScheduler(constraints, parameters).Optimize();

        var morningCounts = users
            .Select(u => solution.GetUserAllAssignments(u.UserId).Count(a => a.ShiftLabel == ShiftLabel.Morning && !a.IsOnCall))
            .ToList();
        var eveningCounts = users
            .Select(u => solution.GetUserAllAssignments(u.UserId).Count(a => a.ShiftLabel == ShiftLabel.Evening && !a.IsOnCall))
            .ToList();

        Assert.True(morningCounts.Max() - morningCounts.Min() <= 6,
            $"Morning spread too large: [{string.Join(",", morningCounts)}]");
        Assert.True(eveningCounts.Max() - eveningCounts.Min() <= 6,
            $"Evening spread too large: [{string.Join(",", eveningCounts)}]");
    }

    [Fact]
    public void Optimize_RespectsMaxConsecutiveWorkdays()
    {
        var start = new DateTime(2026, 6, 22);
        var users = Enumerable.Range(1, 5).Select(i =>
        {
            var u = MakeUser(i);
            u.MaxConsecutiveShifts = 3;
            u.MaxShiftsPerWeek = 5;
            return u;
        }).ToList();

        var constraints = BuildConstraints(start, days: 21, users);
        constraints.HardRules.EnforceMaxConsecutiveShifts = true;
        constraints.HardRules.EnforceWeeklyMaxShifts = true;
        constraints.SoftWeights.WorkdaySpreadWeight = 5;
        constraints.SoftWeights.FairMorningEveningPeerWeight = 5;

        var solution = new SimulatedAnnealingScheduler(constraints, SoftSaParams()).Optimize();

        foreach (var user in users)
        {
            var workDates = solution.GetUserAllAssignments(user.UserId)
                .Where(a => !a.IsOnCall)
                .Select(a => a.Date.Date)
                .Distinct()
                .OrderBy(d => d)
                .ToList();

            var run = 1;
            for (var i = 1; i < workDates.Count; i++)
            {
                if ((workDates[i] - workDates[i - 1]).Days == 1)
                {
                    run++;
                    Assert.True(run <= 3,
                        $"User {user.UserId} has {run} consecutive workdays ending {workDates[i]:yyyy-MM-dd}");
                }
                else
                {
                    run = 1;
                }
            }
        }
    }

    [Fact]
    public void ProductivityHourFillGuard_DoesNotFillSevenDaysInAWeek()
    {
        var start = new DateTime(2026, 7, 1); // Wednesday
        var user = MakeUser(1);
        user.MaxConsecutiveShifts = 3;
        user.MaxShiftsPerWeek = 5;
        user.IncludedInProductivityPlan = true;
        user.ProductivityRequiredHours = 160m;
        user.OvertimeConsent = true;

        var others = Enumerable.Range(2, 4).Select(MakeUser).ToList();
        foreach (var o in others)
        {
            o.IncludedInProductivityPlan = true;
            o.ProductivityRequiredHours = 160m;
        }

        var constraints = BuildConstraints(start, days: 14, [user, .. others]);
        constraints.HardRules.EnforceMaxConsecutiveShifts = true;
        var solution = new ShiftSolution();

        // فقط چند شیفت اولیه برای بقیه تا ظرفیت خالی بماند
        for (var d = 0; d < 14; d++)
        {
            var day = start.AddDays(d);
            solution.AddAssignment(2 + (d % 4), 3, day, ShiftLabel.Night, false);
        }

        ProductivityHourFillGuard.Enforce(solution, constraints);

        var workDates = solution.GetUserAllAssignments(1)
            .Where(a => !a.IsOnCall)
            .Select(a => a.Date.Date)
            .Distinct()
            .ToList();

        foreach (var week in workDates.GroupBy(d => d.AddDays(-(int)d.DayOfWeek)))
        {
            Assert.True(week.Count() <= 5,
                $"User 1 got {week.Count()} workdays in a week (max 5)");
        }

        var ordered = workDates.OrderBy(d => d).ToList();
        var run = 1;
        for (var i = 1; i < ordered.Count; i++)
        {
            if ((ordered[i] - ordered[i - 1]).Days == 1)
            {
                run++;
                Assert.True(run <= 3, $"User 1 consecutive run={run}");
            }
            else
            {
                run = 1;
            }
        }
    }

    private static UserConstraint MakeUser(int id) => new()
    {
        UserId = id,
        Gender = id % 2 == 0 ? UserGender.Female : UserGender.Male,
        SpecialtyId = 10,
        IsActive = true,
        ShiftType = ShiftTypes.RotatingShift,
        ShiftSubType = ShiftSubTypes.ThreeShifts,
        AllowedShiftLabels = [ShiftLabel.Morning, ShiftLabel.Evening, ShiftLabel.Night],
        MaxConsecutiveShifts = 3,
        MinRestDaysBetweenShifts = 0,
        MaxShiftsPerWeek = 5,
        MaxNightShiftsPerMonth = 8,
        MinDaysBetweenNightShifts = 2
    };

    private static ShiftConstraints BuildConstraints(DateTime start, int days, List<UserConstraint> users) => new()
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
            EnforceMaxConsecutiveShifts = true,
            EnforceWeeklyMaxShifts = true,
            EnforceSpecialtyCapacity = true
        },
        GlobalConstraints = new GlobalConstraints { MaxShiftsPerDay = 2 },
        SoftWeights = new SoftRuleWeights
        {
            FairShiftCountBalanceWeight = 2,
            FairWorkedHoursBalanceWeight = 3,
            FairMorningEveningPeerWeight = 5,
            WorkdaySpreadWeight = 5,
            MorningEveningBalanceWeight = 2,
            FairNightShiftBalanceWeight = 2,
            ProductivityShortfallWeight = 3
        }
    };

    private static SimulatedAnnealingParameters SoftSaParams() => new()
    {
        InitialTemperature = 500,
        FinalTemperature = 0.1,
        CoolingRate = 0.97,
        MaxIterations = 2500,
        MaxIterationsWithoutImprovement = 400,
        PenaltyWeight = 1000
    };

    private static ShiftRequirement Shift(int id, ShiftLabel label) => new()
    {
        ShiftId = id,
        ShiftLabel = label,
        DepartmentId = 1,
        DurationHours = label == ShiftLabel.Night ? 12 : 6,
        StartTime = label switch
        {
            ShiftLabel.Morning => TimeSpan.FromHours(8),
            ShiftLabel.Evening => TimeSpan.FromHours(14),
            _ => TimeSpan.FromHours(20)
        },
        EndTime = label switch
        {
            ShiftLabel.Morning => TimeSpan.FromHours(14),
            ShiftLabel.Evening => TimeSpan.FromHours(20),
            _ => TimeSpan.FromHours(8)
        },
        SpecialtyRequirements =
        [
            new SpecialtyRequirement { SpecialtyId = 10, RequiredTotalCount = 1 }
        ]
    };
}
