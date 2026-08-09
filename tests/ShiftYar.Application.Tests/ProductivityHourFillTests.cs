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

    [Fact]
    public void ProductivityHourFillGuard_RebalancesFromOverTargetToUnderTarget_ByRequiredHoursRatio()
    {
        var start = new DateTime(2026, 7, 23);
        var users = new List<UserConstraint>
        {
            MakeUser(1, requiredHours: 152, exactNights: 5),
            MakeUser(2, requiredHours: 156, exactNights: 3),
            MakeUser(3, requiredHours: 156, exactNights: 3)
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
                EnforceProductivityHours = true,
                AllowEveningAfterNightShift = false
            },
            GlobalConstraints = new GlobalConstraints { MaxShiftsPerDay = 1 }
        };

        var solution = new ShiftSolution();
        // کاربر ۱ (موظفی کمتر) ساعات زیاد؛ ۲ و ۳ کسری شدید
        for (var i = 0; i < 8; i++)
        {
            solution.AddAssignment(1, 3, start.AddDays(i * 2), ShiftLabel.Night, false);
            solution.AddAssignment(1, 1, start.AddDays(i * 2 + 1), ShiftLabel.Morning, false);
        }

        solution.AddAssignment(2, 1, start.AddDays(1), ShiftLabel.Morning, false);
        solution.AddAssignment(3, 2, start.AddDays(2), ShiftLabel.Evening, false);

        var lookup = ShiftYar.Application.Common.Utilities.ProductivityWorkedHoursCalculator
            .BuildShiftInfoLookup(constraints.ShiftRequirements);
        var ratioBefore = RatioSpread(solution, users, lookup, constraints);

        ProductivityHourFillGuard.Enforce(solution, constraints);

        var ratioAfter = RatioSpread(solution, users, lookup, constraints);
        var worked1 = Worked(solution, 1, lookup, constraints);
        var worked2 = Worked(solution, 2, lookup, constraints);
        var worked3 = Worked(solution, 3, lookup, constraints);

        Assert.True(ratioAfter <= ratioBefore + 0.05, $"Ratio spread worsened: before={ratioBefore:F2}, after={ratioAfter:F2}");
        Assert.True(worked2 + worked3 > worked1 * 0.6, "Hours should move toward under-target users");
    }

    [Fact]
    public void EnforceFinalBalance_MovesNonProtectedShiftFromUserWithApprovedRequestOnOtherDay()
    {
        var start = new DateTime(2026, 8, 1);
        var donor = MakeUser(6, requiredHours: 152);
        donor.RequiredShiftSlots.Add(new ShiftSlotConstraint
        {
            Date = start.AddDays(10),
            ShiftLabel = ShiftLabel.Night
        });
        var receiver = MakeUser(2, requiredHours: 152);

        var constraints = new ShiftConstraints
        {
            StartDate = start,
            EndDate = start.AddDays(20),
            UserConstraints = [donor, receiver, MakeUser(3, 152)],
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
                EnforceSpecialtyCapacity = true
            },
            GlobalConstraints = new GlobalConstraints { MaxShiftsPerDay = 2 }
        };

        var solution = new ShiftSolution();
        for (var d = 0; d < 21; d++)
        {
            var day = start.AddDays(d);
            solution.AddAssignment(6, 1, day, ShiftLabel.Morning, false);
            solution.AddAssignment(6, 2, day, ShiftLabel.Evening, false);
        }

        solution.AddAssignment(6, 3, start.AddDays(10), ShiftLabel.Night, false);

        var lookup = ShiftYar.Application.Common.Utilities.ProductivityWorkedHoursCalculator
            .BuildShiftInfoLookup(constraints.ShiftRequirements);
        var beforeDonor = Worked(solution, 6, lookup, constraints);
        var beforeReceiver = Worked(solution, 2, lookup, constraints);

        ProductivityHourFillGuard.EnforceFinalBalance(solution, constraints);

        var afterDonor = Worked(solution, 6, lookup, constraints);
        var afterReceiver = Worked(solution, 2, lookup, constraints);

        Assert.True(afterDonor < beforeDonor - 4, "Donor with ON on another day should donate non-protected shifts.");
        Assert.True(afterReceiver > beforeReceiver + 4, "Receiver should gain hours from rebalance.");
        Assert.True(
            solution.GetShiftAssignments(3, start.AddDays(10)).Any(a => a.UserId == 6),
            "Approved night request must remain after final balance.");
    }

    private static double RatioSpread(
        ShiftSolution solution,
        List<UserConstraint> users,
        IReadOnlyDictionary<int, ShiftYar.Application.Common.Utilities.ProductivityWorkedHoursCalculator.ShiftWorkInfo> lookup,
        ShiftConstraints constraints)
    {
        var ratios = users.Select(u =>
        {
            var worked = Worked(solution, u.UserId, lookup, constraints);
            return worked / (double)u.ProductivityRequiredHours!.Value;
        }).ToList();
        var avg = ratios.Average();
        return ratios.Sum(r => Math.Abs(r - avg));
    }

    private static double Worked(
        ShiftSolution solution,
        int userId,
        IReadOnlyDictionary<int, ShiftYar.Application.Common.Utilities.ProductivityWorkedHoursCalculator.ShiftWorkInfo> lookup,
        ShiftConstraints constraints) =>
        ShiftYar.Application.Common.Utilities.ProductivityWorkedHoursCalculator.CalculateEffectiveWorkedHours(
            solution.GetUserAllAssignments(userId), lookup, constraints.IsHoliday);

    private static UserConstraint MakeUser(int id, decimal requiredHours, int? exactNights = null) => new()
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
        ExactNightShiftCount = exactNights,
        OvertimeConsent = false,
        MaxShiftsPerWeek = 7,
        MaxConsecutiveShifts = 30,
        MinRestDaysBetweenShifts = 1,
        MinDaysBetweenNightShifts = 2
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
