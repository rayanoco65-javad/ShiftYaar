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

    [Fact]
    public void FillAllRequiredHoursPass_FillsProjectDeficitEvenWhenNonProjectHasSmallDeficit()
    {
        var start = new DateTime(2026, 8, 1);
        var nonProject = MakeUser(9, requiredHours: 156);
        nonProject.IsProjectPersonnel = false;
        var project = MakeUser(5, requiredHours: 71);
        project.IsProjectPersonnel = true;

        var constraints = new ShiftConstraints
        {
            StartDate = start,
            EndDate = start.AddDays(25),
            UserConstraints = [nonProject, project],
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
        for (var i = 0; i < 21; i++)
        {
            solution.AddAssignment(9, 1, start.AddDays(i), ShiftLabel.Morning, false);
        }

        solution.AddAssignment(5, 1, start.AddDays(22), ShiftLabel.Morning, false);
        solution.AddAssignment(5, 2, start.AddDays(22), ShiftLabel.Evening, false);

        var lookup = ShiftYar.Application.Common.Utilities.ProductivityWorkedHoursCalculator
            .BuildShiftInfoLookup(constraints.ShiftRequirements);
        var projectBefore = Worked(solution, 5, lookup, constraints);

        ProductivityHourFillGuard.Enforce(solution, constraints);

        var projectAfter = Worked(solution, 5, lookup, constraints);
        var projectDeficit = 71 - projectAfter;

        Assert.True(projectBefore < 60, $"Setup project deficit expected, got {projectBefore}");
        Assert.True(
            projectDeficit <= ProjectPersonnelProductivityPriority.CrossTierToleranceHours + 2,
            $"Project should reach required hours, deficit={projectDeficit:F1}");
    }

    [Fact]
    public void StripProjectPersonnelOvertime_CapsProjectPersonnelAtRequiredHours()
    {
        var start = new DateTime(2026, 8, 1);
        var project = MakeUser(5, requiredHours: 70);
        project.IsProjectPersonnel = true;
        var nonProject = MakeUser(9, requiredHours: 156);
        nonProject.IsProjectPersonnel = false;
        nonProject.OvertimeConsent = true;

        var constraints = new ShiftConstraints
        {
            StartDate = start,
            EndDate = start.AddDays(25),
            UserConstraints = [project, nonProject],
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
        for (var i = 0; i < 6; i++)
        {
            var day = start.AddDays(22 + i * 2);
            solution.AddAssignment(5, 1, day, ShiftLabel.Morning, false);
            solution.AddAssignment(5, 2, day, ShiftLabel.Evening, false);
        }

        for (var i = 0; i < 21; i++)
        {
            solution.AddAssignment(9, 1, start.AddDays(i), ShiftLabel.Morning, false);
        }

        var lookup = ShiftYar.Application.Common.Utilities.ProductivityWorkedHoursCalculator
            .BuildShiftInfoLookup(constraints.ShiftRequirements);
        var projectBefore = Worked(solution, 5, lookup, constraints);

        ProductivityHourFillGuard.Enforce(solution, constraints);

        var projectAfter = Worked(solution, 5, lookup, constraints);
        Assert.True(projectBefore > 70 + 5, $"Setup should have project overtime, got {projectBefore}");
        Assert.True(
            projectAfter <= 70 + ProjectPersonnelProductivityPriority.CrossTierToleranceHours + 1,
            $"Project personnel should be capped near required hours, got {projectAfter} (required 70)");
    }

    [Fact]
    public void EnforceCrossTierPriorityBalance_FillsSmallNonProjectDeficitBeforeProjectOvertime()
    {
        var start = new DateTime(2026, 8, 1);
        var nonProject = MakeUser(9, requiredHours: 156);
        nonProject.IsProjectPersonnel = false;
        var projectDonor = MakeUser(5, requiredHours: 70);
        projectDonor.IsProjectPersonnel = true;

        var constraints = new ShiftConstraints
        {
            StartDate = start,
            EndDate = start.AddDays(25),
            UserConstraints = [nonProject, projectDonor],
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
            GlobalConstraints = new GlobalConstraints { MaxShiftsPerDay = 2 }
        };

        var solution = new ShiftSolution();
        // طرحی: مازاد روی روزهایی که غیرطرحی در آن‌ها شیفت ندارد
        for (var i = 0; i < 6; i++)
        {
            var day = start.AddDays(22 + i * 2);
            solution.AddAssignment(5, 1, day, ShiftLabel.Morning, false);
            solution.AddAssignment(5, 2, day, ShiftLabel.Evening, false);
        }

        // غیرطرحی: ۲۱ شیفت صبح → کسری موظفی
        for (var i = 0; i < 21; i++)
        {
            solution.AddAssignment(9, 1, start.AddDays(i), ShiftLabel.Morning, false);
        }

        var lookup = ShiftYar.Application.Common.Utilities.ProductivityWorkedHoursCalculator
            .BuildShiftInfoLookup(constraints.ShiftRequirements);
        var nonProjectBefore = Worked(solution, 9, lookup, constraints);
        var projectBefore = Worked(solution, 5, lookup, constraints);

        ProductivityHourFillGuard.Enforce(solution, constraints);

        var nonProjectAfter = Worked(solution, 9, lookup, constraints);
        var projectAfter = Worked(solution, 5, lookup, constraints);

        Assert.True(
            nonProjectAfter > nonProjectBefore + 0.5 || projectAfter < projectBefore - 0.5,
            $"Expected transfer toward non-project. nonProject {nonProjectBefore}->{nonProjectAfter}, project {projectBefore}->{projectAfter}");

        var deficitBefore = 156 - nonProjectBefore;
        var deficitAfter = 156 - nonProjectAfter;
        var excessBefore = projectBefore - 70;
        var excessAfter = projectAfter - 70;
        Assert.True(
            deficitAfter + 0.01 < deficitBefore || excessAfter + 0.01 < excessBefore,
            $"Cross-tier balance should reduce non-project deficit or project excess. deficit {deficitBefore:F1}->{deficitAfter:F1}, excess {excessBefore:F1}->{excessAfter:F1}");
    }

    [Fact]
    public void ProductivityHourFillGuard_DoesNotThrowWhenMixedProjectAndNonProjectPersonnel()
    {
        var start = new DateTime(2026, 8, 1);
        var users = new List<UserConstraint>
        {
            MakeUser(11, requiredHours: 120),
            MakeUser(12, requiredHours: 120)
        };
        users[0].IsProjectPersonnel = false;
        users[1].IsProjectPersonnel = true;

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
        solution.AddAssignment(11, 1, start.AddDays(1), ShiftLabel.Morning, false);
        solution.AddAssignment(12, 1, start.AddDays(2), ShiftLabel.Morning, false);

        var exception = Record.Exception(() => ProductivityHourFillGuard.Enforce(solution, constraints));
        Assert.Null(exception);
    }

    [Fact]
    public void ProductivityHourFillGuard_PrefersNonProjectPersonnelBeforeProjectPersonnel()
    {
        var start = new DateTime(2026, 8, 1);
        var nonProject = MakeUser(1, requiredHours: 120);
        nonProject.IsProjectPersonnel = false;
        var projectReceiver = MakeUser(2, requiredHours: 120);
        projectReceiver.IsProjectPersonnel = true;
        var projectDonor = MakeUser(3, requiredHours: 120);
        projectDonor.IsProjectPersonnel = true;

        var users = new List<UserConstraint> { nonProject, projectReceiver, projectDonor };
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
        for (var i = 0; i < 10; i++)
        {
            solution.AddAssignment(3, 3, start.AddDays(i * 2), ShiftLabel.Night, false);
            solution.AddAssignment(3, 1, start.AddDays(i * 2 + 1), ShiftLabel.Morning, false);
        }

        solution.AddAssignment(1, 1, start.AddDays(1), ShiftLabel.Morning, false);
        solution.AddAssignment(2, 1, start.AddDays(2), ShiftLabel.Morning, false);

        var lookup = ShiftYar.Application.Common.Utilities.ProductivityWorkedHoursCalculator
            .BuildShiftInfoLookup(constraints.ShiftRequirements);

        ProductivityHourFillGuard.Enforce(solution, constraints);

        var nonProjectWorked = Worked(solution, 1, lookup, constraints);
        var projectWorked = Worked(solution, 2, lookup, constraints);
        var nonProjectRatio = nonProjectWorked / 120.0;
        var projectRatio = projectWorked / 120.0;

        Assert.True(
            nonProjectRatio >= projectRatio - 0.05,
            $"Non-project ratio ({nonProjectRatio:P0}) should be at least project ratio ({projectRatio:P0}).");
        Assert.True(
            nonProjectWorked > projectWorked || nonProjectRatio > 0.5,
            "Non-project personnel should receive priority when filling required hours.");
    }

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
