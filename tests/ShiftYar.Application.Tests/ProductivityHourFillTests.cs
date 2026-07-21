using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using Xunit;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;
using static ShiftYar.Domain.Enums.UserModel.UserEnums;

namespace ShiftYar.Application.Tests;

public class ProductivityHourFillTests
{
    [Fact]
    public void ProductivityHourFillGuard_IncreasesWorkedHoursTowardTarget()
    {
        var start = new DateTime(2026, 8, 1);
        var users = new List<UserConstraint>
        {
            MakeUser(1, requiredHours: 120),
            MakeUser(2, requiredHours: 120),
            MakeUser(3, requiredHours: 120)
        };

        var constraints = new ShiftConstraints
        {
            StartDate = start,
            EndDate = start.AddDays(20),
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
                EnforceSpecialtyCapacity = true,
                EnforceProductivityHours = true
            },
            GlobalConstraints = new GlobalConstraints { MaxShiftsPerDay = 2 }
        };

        var solution = new ShiftSolution();
        // هر کاربر فقط ۲ شیفت روزانه دارد → کمبود موظفی
        solution.AddAssignment(1, 1, start.AddDays(1), ShiftLabel.Morning, false);
        solution.AddAssignment(2, 1, start.AddDays(2), ShiftLabel.Morning, false);
        solution.AddAssignment(3, 1, start.AddDays(3), ShiftLabel.Morning, false);

        var lookup = ShiftYar.Application.Common.Utilities.ProductivityWorkedHoursCalculator
            .BuildShiftInfoLookup(constraints.ShiftRequirements);
        var before = users.Sum(u =>
            ShiftYar.Application.Common.Utilities.ProductivityWorkedHoursCalculator.CalculateEffectiveWorkedHours(
                solution.GetUserAllAssignments(u.UserId), lookup, constraints.IsHoliday));

        ProductivityHourFillGuard.Enforce(solution, constraints);

        var after = users.Sum(u =>
            ShiftYar.Application.Common.Utilities.ProductivityWorkedHoursCalculator.CalculateEffectiveWorkedHours(
                solution.GetUserAllAssignments(u.UserId), lookup, constraints.IsHoliday));

        Assert.True(after > before + 10, $"Expected meaningful hour increase, before={before}, after={after}");
    }

    [Fact]
    public void Optimize_PenalizesProductivityShortfall()
    {
        var start = new DateTime(2026, 8, 1);
        var users = Enumerable.Range(1, 4).Select(i => MakeUser(i, requiredHours: 160)).ToList();
        var constraints = new ShiftConstraints
        {
            StartDate = start,
            EndDate = start.AddDays(25),
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
                EnforceSpecialtyCapacity = true,
                EnforceProductivityHours = true
            },
            GlobalConstraints = new GlobalConstraints { MaxShiftsPerDay = 2 },
            SoftWeights = new SoftRuleWeights
            {
                FairWorkedHoursBalanceWeight = 4.0,
                ProductivityShortfallWeight = 5.0,
                ExactNightQuotaWeight = 1.0
            }
        };

        var parameters = new SimulatedAnnealingParameters
        {
            InitialTemperature = 400,
            FinalTemperature = 0.1,
            CoolingRate = 0.93,
            MaxIterations = 2500,
            MaxIterationsWithoutImprovement = 300
        };

        var solution = new SimulatedAnnealingScheduler(constraints, parameters).Optimize();
        var lookup = ShiftYar.Application.Common.Utilities.ProductivityWorkedHoursCalculator
            .BuildShiftInfoLookup(constraints.ShiftRequirements);

        var ratios = users.Select(u =>
        {
            var worked = ShiftYar.Application.Common.Utilities.ProductivityWorkedHoursCalculator
                .CalculateEffectiveWorkedHours(solution.GetUserAllAssignments(u.UserId), lookup, constraints.IsHoliday);
            return worked / 160.0;
        }).ToList();

        Assert.True(ratios.Average() > 0.55, $"Average fill ratio too low: {ratios.Average():P0}");
        Assert.True(ratios.Max() - ratios.Min() < 0.35, $"Hour balance spread too high: [{string.Join(", ", ratios.Select(r => r.ToString("P0")))}]");
    }

    private static UserConstraint MakeUser(int id, decimal requiredHours) => new()
    {
        UserId = id,
        Gender = id % 2 == 0 ? UserGender.Female : UserGender.Male,
        SpecialtyId = 10,
        IsActive = true,
        ShiftType = ShiftTypes.RotatingShift,
        ShiftSubType = ShiftSubTypes.ThreeShifts,
        AllowedShiftLabels = [ShiftLabel.Morning, ShiftLabel.Evening, ShiftLabel.Night],
        IncludedInProductivityPlan = true,
        ProductivityRequiredHours = requiredHours,
        OvertimeConsent = false,
        MaxShiftsPerWeek = 7,
        MaxConsecutiveShifts = 30,
        MinRestDaysBetweenShifts = 0
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
