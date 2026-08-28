using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using Xunit;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;
using static ShiftYar.Domain.Enums.UserModel.UserEnums;

namespace ShiftYar.Application.Tests;

public class ShiftManagerRulesTests
{
    [Fact]
    public void NormalizeUserDto_LevelTakesPrecedenceOverBool()
    {
        ShiftManagerRules.NormalizeUserDto(2, false, out var level, out var canBe);
        Assert.Equal((byte)2, level);
        Assert.True(canBe);
    }

    [Fact]
    public void NormalizeUserDto_LegacyTrueBecomesLevel1()
    {
        ShiftManagerRules.NormalizeUserDto(null, true, out var level, out var canBe);
        Assert.Equal(ShiftManagerRules.Level1, level);
        Assert.True(canBe);
    }

    [Fact]
    public void GetRequirement_ReadsFromShiftOnly()
    {
        var shift = new ShiftRequirement
        {
            ShiftId = 1,
            ShiftLabel = ShiftLabel.Evening,
            ManagerRequiredCount = 2,
            ManagerMinLevel1Count = 1
        };

        var (required, minL1) = ShiftManagerRules.GetRequirement(shift);
        Assert.Equal(2, required);
        Assert.Equal(1, minL1);
    }

    [Fact]
    public void GetRequirement_ZeroMeansNoRequirement()
    {
        var shift = new ShiftRequirement
        {
            ShiftId = 1,
            ShiftLabel = ShiftLabel.Night,
            ManagerRequiredCount = 0,
            ManagerMinLevel1Count = 0
        };

        var (required, minL1) = ShiftManagerRules.GetRequirement(shift);
        Assert.Equal(0, required);
        Assert.Equal(0, minL1);
        Assert.False(ShiftManagerRules.RequiresAnyManager(shift));
    }

    [Fact]
    public void IsSatisfied_RequiresOneLevel1AndTwoManagers()
    {
        var users = new List<UserConstraint>
        {
            Make(1, level: 1),
            Make(2, level: 2),
            Make(3, level: null)
        };

        Assert.True(ShiftManagerRules.IsSatisfied(users, requiredTotal: 2, minLevel1: 1));
        Assert.False(ShiftManagerRules.IsSatisfied(users.Where(u => u.UserId != 1), requiredTotal: 2, minLevel1: 1));
        Assert.False(ShiftManagerRules.IsSatisfied(users.Take(1), requiredTotal: 2, minLevel1: 1));
    }

    [Fact]
    public void EnsureShiftManagers_UsesShiftLevelCounts()
    {
        var start = new DateTime(2026, 9, 1);
        var l2 = Make(1, level: 2);
        var l1 = Make(2, level: 1);
        var filler = Make(3, level: null);

        var constraints = new ShiftConstraints
        {
            StartDate = start,
            EndDate = start,
            UserConstraints = [l2, l1, filler],
            ShiftRequirements =
            [
                new ShiftRequirement
                {
                    ShiftId = 3,
                    ShiftLabel = ShiftLabel.Night,
                    DepartmentId = 1,
                    DurationHours = 12,
                    ManagerRequiredCount = 2,
                    ManagerMinLevel1Count = 1,
                    SpecialtyRequirements =
                    [
                        new SpecialtyRequirement { SpecialtyId = 10, RequiredTotalCount = 2 }
                    ]
                }
            ],
            GlobalConstraints = new GlobalConstraints
            {
                MaxShiftsPerDay = 1
            },
            HardRules = new HardRuleSet
            {
                EnforceSpecialtyCapacity = true,
                EnforceMaxShiftsPerDay = true
            }
        };

        var solution = new ShiftSolution();
        solution.AddAssignment(l2.UserId, 3, start, ShiftLabel.Night, false);
        solution.AddAssignment(filler.UserId, 3, start, ShiftLabel.Night, false);

        var scheduler = new SimulatedAnnealingScheduler(constraints, new SimulatedAnnealingParameters
        {
            MaxIterations = 1,
            InitialTemperature = 1,
            FinalTemperature = 0.1
        });
        scheduler.ApplyMandatoryConstraints(solution);

        var nightUsers = solution.GetShiftAssignments(3, start)
            .Where(a => !a.IsOnCall)
            .Select(a => constraints.UserConstraints.First(u => u.UserId == a.UserId))
            .ToList();

        Assert.True(ShiftManagerRules.IsSatisfied(nightUsers, 2, 1));
        Assert.Contains(nightUsers, ShiftManagerRules.IsLevel1);
    }

    [Fact]
    public void EnsureShiftManagers_SwapMorningL1ToEvening_WhenMaxShiftsPerDayIsOne()
    {
        var start = new DateTime(2026, 8, 25);
        var l1Morning = Make(12, level: 1);
        var l2Evening = Make(22, level: 2);
        var fillerEvening = Make(30, level: null);
        var fillerEvening2 = Make(32, level: null);
        var l1Free = Make(21, level: 1);
        var l1Night = Make(19, level: 1);
        var l2Night = Make(23, level: 2);
        var fillerNight = Make(29, level: null);

        var constraints = new ShiftConstraints
        {
            StartDate = start,
            EndDate = start,
            UserConstraints =
            [
                l1Morning, l2Evening, fillerEvening, fillerEvening2, l1Free, l1Night, l2Night, fillerNight
            ],
            ShiftRequirements =
            [
                new ShiftRequirement
                {
                    ShiftId = 4,
                    ShiftLabel = ShiftLabel.Morning,
                    DepartmentId = 2,
                    DurationHours = 12,
                    ManagerRequiredCount = 0,
                    SpecialtyRequirements =
                    [
                        new SpecialtyRequirement { SpecialtyId = 10, RequiredTotalCount = 1 }
                    ]
                },
                new ShiftRequirement
                {
                    ShiftId = 5,
                    ShiftLabel = ShiftLabel.Evening,
                    DepartmentId = 2,
                    DurationHours = 12,
                    ManagerRequiredCount = 2,
                    ManagerMinLevel1Count = 1,
                    SpecialtyRequirements =
                    [
                        new SpecialtyRequirement { SpecialtyId = 10, RequiredTotalCount = 3 }
                    ]
                },
                new ShiftRequirement
                {
                    ShiftId = 6,
                    ShiftLabel = ShiftLabel.Night,
                    DepartmentId = 2,
                    DurationHours = 12,
                    ManagerRequiredCount = 2,
                    ManagerMinLevel1Count = 1,
                    SpecialtyRequirements =
                    [
                        new SpecialtyRequirement { SpecialtyId = 10, RequiredTotalCount = 3 }
                    ]
                }
            ],
            GlobalConstraints = new GlobalConstraints
            {
                MaxShiftsPerDay = 1
            },
            HardRules = new HardRuleSet
            {
                EnforceSpecialtyCapacity = true,
                EnforceMaxShiftsPerDay = true
            }
        };

        var solution = new ShiftSolution();
        solution.AddAssignment(l1Morning.UserId, 4, start, ShiftLabel.Morning, false);
        solution.AddAssignment(l2Evening.UserId, 5, start, ShiftLabel.Evening, false);
        solution.AddAssignment(fillerEvening.UserId, 5, start, ShiftLabel.Evening, false);
        solution.AddAssignment(fillerEvening2.UserId, 5, start, ShiftLabel.Evening, false);
        solution.AddAssignment(l1Night.UserId, 6, start, ShiftLabel.Night, false);
        solution.AddAssignment(l2Night.UserId, 6, start, ShiftLabel.Night, false);
        solution.AddAssignment(fillerNight.UserId, 6, start, ShiftLabel.Night, false);

        var scheduler = new SimulatedAnnealingScheduler(constraints, new SimulatedAnnealingParameters
        {
            MaxIterations = 1,
            InitialTemperature = 1,
            FinalTemperature = 0.1
        });
        scheduler.ApplyMandatoryConstraints(solution);

        var eveningUsers = solution.GetShiftAssignments(5, start)
            .Where(a => !a.IsOnCall)
            .Select(a => constraints.UserConstraints.First(u => u.UserId == a.UserId))
            .ToList();

        Assert.True(ShiftManagerRules.IsSatisfied(eveningUsers, 2, 1));
        Assert.Contains(eveningUsers, ShiftManagerRules.IsLevel1);
        Assert.DoesNotContain(
            solution.Violations,
            v => v.Contains("Shift manager mix unmet", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EnsureShiftManagers_SwapMorningL1ToNight_WithNonManagerBackfill()
    {
        var start = new DateTime(2026, 8, 23);
        var l1Morning = Make(14, level: 1);
        var l1Evening = Make(15, level: 1);
        var l2Evening = Make(22, level: 2);
        var fillerEvening = Make(30, level: null);
        var l2Free = Make(23, level: 2);
        var fillerNight1 = Make(25, level: null);
        var fillerNight2 = Make(26, level: null);
        var fillerNight3 = Make(29, level: null);
        var fillerNight4 = Make(32, level: null);

        var constraints = new ShiftConstraints
        {
            StartDate = start,
            EndDate = start,
            UserConstraints =
            [
                l1Morning, l1Evening, l2Evening, fillerEvening, l2Free,
                fillerNight1, fillerNight2, fillerNight3, fillerNight4
            ],
            ShiftRequirements =
            [
                new ShiftRequirement
                {
                    ShiftId = 4,
                    ShiftLabel = ShiftLabel.Morning,
                    DepartmentId = 2,
                    DurationHours = 12,
                    ManagerRequiredCount = 0,
                    SpecialtyRequirements =
                    [
                        new SpecialtyRequirement { SpecialtyId = 10, RequiredTotalCount = 1 }
                    ]
                },
                new ShiftRequirement
                {
                    ShiftId = 5,
                    ShiftLabel = ShiftLabel.Evening,
                    DepartmentId = 2,
                    DurationHours = 12,
                    ManagerRequiredCount = 2,
                    ManagerMinLevel1Count = 1,
                    SpecialtyRequirements =
                    [
                        new SpecialtyRequirement { SpecialtyId = 10, RequiredTotalCount = 3 }
                    ]
                },
                new ShiftRequirement
                {
                    ShiftId = 6,
                    ShiftLabel = ShiftLabel.Night,
                    DepartmentId = 2,
                    DurationHours = 12,
                    ManagerRequiredCount = 2,
                    ManagerMinLevel1Count = 1,
                    SpecialtyRequirements =
                    [
                        new SpecialtyRequirement { SpecialtyId = 10, RequiredTotalCount = 4 }
                    ]
                }
            ],
            GlobalConstraints = new GlobalConstraints
            {
                MaxShiftsPerDay = 1
            },
            HardRules = new HardRuleSet
            {
                EnforceSpecialtyCapacity = true,
                EnforceMaxShiftsPerDay = true
            }
        };

        var solution = new ShiftSolution();
        solution.AddAssignment(l1Morning.UserId, 4, start, ShiftLabel.Morning, false);
        solution.AddAssignment(l1Evening.UserId, 5, start, ShiftLabel.Evening, false);
        solution.AddAssignment(l2Evening.UserId, 5, start, ShiftLabel.Evening, false);
        solution.AddAssignment(fillerEvening.UserId, 5, start, ShiftLabel.Evening, false);
        solution.AddAssignment(fillerNight1.UserId, 6, start, ShiftLabel.Night, false);
        solution.AddAssignment(fillerNight2.UserId, 6, start, ShiftLabel.Night, false);
        solution.AddAssignment(fillerNight3.UserId, 6, start, ShiftLabel.Night, false);
        solution.AddAssignment(fillerNight4.UserId, 6, start, ShiftLabel.Night, false);

        var scheduler = new SimulatedAnnealingScheduler(constraints, new SimulatedAnnealingParameters
        {
            MaxIterations = 1,
            InitialTemperature = 1,
            FinalTemperature = 0.1
        });
        scheduler.ApplyMandatoryConstraints(solution);

        var nightUsers = solution.GetShiftAssignments(6, start)
            .Where(a => !a.IsOnCall)
            .Select(a => constraints.UserConstraints.First(u => u.UserId == a.UserId))
            .ToList();

        Assert.True(ShiftManagerRules.IsSatisfied(nightUsers, 2, 1));
        Assert.Contains(nightUsers, ShiftManagerRules.IsLevel1);
        Assert.DoesNotContain(
            solution.Violations,
            v => v.Contains("Shift manager mix unmet", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EnsureShiftManagers_FixesAug23Night_TwoL2Only()
    {
        var start = new DateTime(2026, 8, 23);
        var u12 = MakeFixedMorning(12, level: 1);
        var u14 = Make(14, level: 1);
        u14.RequiredShiftSlots.Add(new ShiftSlotConstraint { Date = start, ShiftLabel = ShiftLabel.Morning, ShiftId = 4 });
        var u15 = MakeFixedMorning(15, level: 1);
        var u18 = Make(18, level: 1);
        u18.UnavailableShiftSlots.Add(new ShiftSlotConstraint { Date = start, ShiftLabel = ShiftLabel.Evening, ShiftId = 5 });
        u18.UnavailableShiftSlots.Add(new ShiftSlotConstraint { Date = start, ShiftLabel = ShiftLabel.Night, ShiftId = 6 });
        var u17 = Make(17, level: 1);
        var u19 = Make(19, level: 1);
        var u22 = Make(22, level: 2);
        var u23 = Make(23, level: 2);
        var u25 = Make(25, level: 2);
        var u27 = Make(27, level: null);
        var u29 = Make(29, level: null);
        var u30 = Make(30, level: null);

        var constraints = BuildPediatricsConstraints(start);
        constraints.UserConstraints = [u12, u14, u15, u18, u17, u19, u22, u23, u25, u27, u29, u30];

        var solution = new ShiftSolution();
        solution.AddAssignment(12, 4, start, ShiftLabel.Morning, false);
        solution.AddAssignment(14, 4, start, ShiftLabel.Morning, false);
        solution.AddAssignment(15, 4, start, ShiftLabel.Morning, false);
        solution.AddAssignment(18, 4, start, ShiftLabel.Morning, false);
        solution.AddAssignment(17, 5, start, ShiftLabel.Evening, false);
        solution.AddAssignment(25, 5, start, ShiftLabel.Evening, false);
        solution.AddAssignment(29, 5, start, ShiftLabel.Evening, false);
        solution.AddAssignment(22, 6, start, ShiftLabel.Night, false);
        solution.AddAssignment(23, 6, start, ShiftLabel.Night, false);
        solution.AddAssignment(27, 6, start, ShiftLabel.Night, false);
        solution.AddAssignment(30, 6, start, ShiftLabel.Night, false);

        ApplyManagers(constraints, solution);

        var nightUsers = GetAssignees(constraints, solution, 6, start);
        Assert.True(ShiftManagerRules.IsSatisfied(nightUsers, 2, 1));
        Assert.DoesNotContain(solution.Violations, v => v.Contains("Shift manager mix unmet"));
    }

    [Fact]
    public void EnsureShiftManagers_FixesAug28Night_OneL2Only()
    {
        var start = new DateTime(2026, 8, 28);
        var u17 = Make(17, level: 1);
        var u19 = Make(19, level: 1);
        var u21 = Make(21, level: 1);
        var u20 = Make(20, level: 1);
        u20.RequiredShiftSlots.Add(new ShiftSlotConstraint { Date = start, ShiftLabel = ShiftLabel.Evening, ShiftId = 5 });
        var u22 = Make(22, level: 2);
        var u23 = Make(23, level: 2);
        var u24 = Make(24, level: 2);
        var u27 = Make(27, level: null);
        var u28 = Make(28, level: null);
        var u31 = Make(31, level: null);

        var constraints = BuildPediatricsConstraints(start);
        constraints.UserConstraints = [u17, u19, u21, u20, u22, u23, u24, u27, u28, u31];

        var solution = new ShiftSolution();
        solution.AddAssignment(17, 4, start, ShiftLabel.Morning, false);
        solution.AddAssignment(19, 4, start, ShiftLabel.Morning, false);
        solution.AddAssignment(21, 4, start, ShiftLabel.Morning, false);
        solution.AddAssignment(20, 5, start, ShiftLabel.Evening, false);
        solution.AddAssignment(22, 5, start, ShiftLabel.Evening, false);
        solution.AddAssignment(24, 5, start, ShiftLabel.Evening, false);
        solution.AddAssignment(23, 6, start, ShiftLabel.Night, false);
        solution.AddAssignment(27, 6, start, ShiftLabel.Night, false);
        solution.AddAssignment(28, 6, start, ShiftLabel.Night, false);
        solution.AddAssignment(31, 6, start, ShiftLabel.Night, false);

        ApplyManagers(constraints, solution);

        var nightUsers = GetAssignees(constraints, solution, 6, start);
        Assert.True(ShiftManagerRules.IsSatisfied(nightUsers, 2, 1));
        Assert.DoesNotContain(solution.Violations, v => v.Contains("Shift manager mix unmet"));
    }

    [Fact]
    public void RepairShiftManagers_ReplacesOccupantAtExactNightQuotaCap()
    {
        var start = new DateTime(2026, 8, 23);
        var u19 = Make(19, level: 1);
        u19.ExactNightShiftCount = 5;
        var u22 = Make(22, level: 2);
        u22.ExactNightShiftCount = 8;
        var u23 = Make(23, level: 2);
        u23.ExactNightShiftCount = 8;
        var u27 = Make(27, level: null);
        u27.ExactNightShiftCount = 8;
        var u30 = Make(30, level: null);
        u30.ExactNightShiftCount = 9;

        var constraints = BuildPediatricsConstraints(start);
        constraints.UserConstraints = [u19, u22, u23, u27, u30];

        var solution = new ShiftSolution();
        // همه روی سقف سهمیه شب — بدون forManagerInstall قابل جابجایی نیستند
        for (var i = 0; i < 8; i++)
        {
            var d = start.AddDays(i + 1);
            solution.AddAssignment(22, 6, d, ShiftLabel.Night, false);
        }

        for (var i = 0; i < 8; i++)
        {
            var d = start.AddDays(i + 2);
            solution.AddAssignment(23, 6, d, ShiftLabel.Night, false);
        }

        for (var i = 0; i < 8; i++)
        {
            solution.AddAssignment(27, 6, start.AddDays(i), ShiftLabel.Night, false);
        }

        solution.AddAssignment(22, 6, start, ShiftLabel.Night, false);
        solution.AddAssignment(23, 6, start, ShiftLabel.Night, false);
        solution.AddAssignment(27, 6, start, ShiftLabel.Night, false);
        solution.AddAssignment(30, 6, start, ShiftLabel.Night, false);

        var scheduler = new SimulatedAnnealingScheduler(constraints, new SimulatedAnnealingParameters
        {
            MaxIterations = 1,
            InitialTemperature = 1,
            FinalTemperature = 0.1
        });
        var warnings = scheduler.RepairShiftManagers(solution);

        var nightUsers = GetAssignees(constraints, solution, 6, start);
        Assert.True(ShiftManagerRules.IsSatisfied(nightUsers, 2, 1));
        Assert.DoesNotContain(warnings, w => w.Contains("Shift manager mix unmet"));
    }

    [Fact]
    public void RepairShiftManagers_ReconcilesManagerMixWithoutBreakingNightQuota()
    {
        var start = new DateTime(2026, 8, 23);
        var u19 = Make(19, level: 1);
        var u22 = Make(22, level: 2);
        u22.ExactNightShiftCount = 8;
        var u23 = Make(23, level: 2);
        u23.ExactNightShiftCount = 8;
        var u27 = Make(27, level: null);
        u27.ExactNightShiftCount = 8;
        var u30 = Make(30, level: null);
        u30.ExactNightShiftCount = 9;

        var constraints = BuildPediatricsConstraints(start);
        constraints.UserConstraints = [u19, u22, u23, u27, u30];

        var solution = new ShiftSolution();
        for (var i = 0; i < 8; i++)
        {
            solution.AddAssignment(22, 6, start.AddDays(i + 1), ShiftLabel.Night, false);
        }

        for (var i = 0; i < 8; i++)
        {
            solution.AddAssignment(23, 6, start.AddDays(i + 2), ShiftLabel.Night, false);
        }

        for (var i = 0; i < 8; i++)
        {
            solution.AddAssignment(27, 6, start.AddDays(i), ShiftLabel.Night, false);
        }

        for (var i = 0; i < 9; i++)
        {
            solution.AddAssignment(30, 6, start.AddDays(i), ShiftLabel.Night, false);
        }

        solution.AddAssignment(22, 6, start, ShiftLabel.Night, false);
        solution.AddAssignment(23, 6, start, ShiftLabel.Night, false);
        solution.AddAssignment(27, 6, start, ShiftLabel.Night, false);
        solution.AddAssignment(30, 6, start, ShiftLabel.Night, false);

        var scheduler = new SimulatedAnnealingScheduler(constraints, new SimulatedAnnealingParameters
        {
            MaxIterations = 1,
            InitialTemperature = 1,
            FinalTemperature = 0.1
        });

        var warnings = scheduler.RepairShiftManagers(solution);

        var nightUsers = GetAssignees(constraints, solution, 6, start);
        Assert.True(ShiftManagerRules.IsSatisfied(nightUsers, 2, 1));
        Assert.DoesNotContain(warnings, w => w.Contains("Shift manager mix unmet"));

        ExactNightQuotaGuard.Enforce(solution, constraints);
        scheduler.RepairShiftManagers(solution);

        nightUsers = GetAssignees(constraints, solution, 6, start);
        Assert.True(ShiftManagerRules.IsSatisfied(nightUsers, 2, 1));
        Assert.True(CountNights(solution, 22) >= 8);
        Assert.True(CountNights(solution, 27) >= 8);
        Assert.True(CountNights(solution, 30) >= 9);
    }

    private static int CountNights(ShiftSolution solution, int userId) =>
        solution.GetUserAllAssignments(userId).Count(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall);

    [Fact]
    public void ApplyMandatoryConstraints_RestoresMissingNightQuotaAfterReconcile()
    {
        var start = new DateTime(2026, 8, 1);
        var u19 = Make(19, level: 1);
        var u22 = Make(22, level: 2);
        u22.ExactNightShiftCount = 8;
        var u23 = Make(23, level: 2);
        u23.ExactNightShiftCount = 8;
        var u27 = Make(27, level: null);
        u27.ExactNightShiftCount = 8;
        var u30 = Make(30, level: null);

        var constraints = BuildPediatricsConstraints(start);
        constraints.EndDate = start.AddDays(30);
        constraints.UserConstraints = [u19, u22, u23, u27, u30];

        var solution = new ShiftSolution();
        foreach (var offset in new[] { 0, 3, 6, 9, 12, 15, 18 })
        {
            solution.AddAssignment(22, 6, start.AddDays(offset), ShiftLabel.Night, false);
        }

        solution.AddAssignment(23, 6, start.AddDays(1), ShiftLabel.Night, false);
        solution.AddAssignment(23, 6, start.AddDays(4), ShiftLabel.Night, false);
        solution.AddAssignment(27, 6, start.AddDays(2), ShiftLabel.Night, false);
        solution.AddAssignment(30, 6, start.AddDays(5), ShiftLabel.Night, false);
        solution.AddAssignment(22, 6, start.AddDays(22), ShiftLabel.Night, false);
        solution.AddAssignment(23, 6, start.AddDays(23), ShiftLabel.Night, false);
        solution.AddAssignment(27, 6, start.AddDays(24), ShiftLabel.Night, false);
        solution.AddAssignment(30, 6, start.AddDays(25), ShiftLabel.Night, false);

        ApplyManagers(constraints, solution);

        Assert.True(CountNights(solution, 22) >= 8);
        Assert.DoesNotContain(
            solution.Violations,
            v => v.Contains("سهمیه حداقل شیفت شب", StringComparison.OrdinalIgnoreCase)
                 && v.Contains("شناسه 22", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EnsureShiftManagers_SwapEveningL1ToNight_WithFreeL1BackfillEvening()
    {
        var start = new DateTime(2026, 8, 23);
        var u17 = Make(17, level: 1);
        var u19 = Make(19, level: 1);
        var u22 = Make(22, level: 2);
        var u23 = Make(23, level: 2);
        var u25 = Make(25, level: 2);
        var u27 = Make(27, level: null);
        var u29 = Make(29, level: null);
        var u30 = Make(30, level: null);

        var constraints = BuildPediatricsConstraints(start);
        constraints.UserConstraints = [u17, u19, u22, u23, u25, u27, u29, u30];

        var solution = new ShiftSolution();
        solution.AddAssignment(17, 5, start, ShiftLabel.Evening, false);
        solution.AddAssignment(25, 5, start, ShiftLabel.Evening, false);
        solution.AddAssignment(29, 5, start, ShiftLabel.Evening, false);
        solution.AddAssignment(22, 6, start, ShiftLabel.Night, false);
        solution.AddAssignment(23, 6, start, ShiftLabel.Night, false);
        solution.AddAssignment(27, 6, start, ShiftLabel.Night, false);
        solution.AddAssignment(30, 6, start, ShiftLabel.Night, false);

        ApplyManagers(constraints, solution);

        var nightUsers = GetAssignees(constraints, solution, 6, start);
        var eveningUsers = GetAssignees(constraints, solution, 5, start);
        Assert.True(ShiftManagerRules.IsSatisfied(nightUsers, 2, 1));
        Assert.True(ShiftManagerRules.IsSatisfied(eveningUsers, 2, 1));
    }

    [Fact]
    public void ApplyMandatoryConstraints_FixesAllReportedNightManagerMixDates_InFullMonth()
    {
        var start = new DateTime(2026, 8, 23);
        var end = new DateTime(2026, 9, 22);
        var failingDates = new[]
        {
            new DateTime(2026, 8, 23),
            new DateTime(2026, 8, 28),
            new DateTime(2026, 9, 1),
            new DateTime(2026, 9, 7),
            new DateTime(2026, 9, 11),
            new DateTime(2026, 9, 13)
        };

        var l1Ids = new[] { 12, 14, 15, 17, 18, 19, 20, 21 };
        var l2Ids = new[] { 22, 23, 24, 25, 26 };
        var fillerIds = new[] { 27, 28, 29, 30, 31 };

        var users = new List<UserConstraint>();
        foreach (var id in l1Ids)
        {
            users.Add(id is 12 or 15 ? MakeFixedMorning(id, 1) : Make(id, 1));
        }

        foreach (var id in l2Ids)
        {
            var u = Make(id, 2);
            if (id is 22 or 23)
            {
                u.ExactNightShiftCount = 8;
            }

            users.Add(u);
        }

        foreach (var id in fillerIds)
        {
            var u = Make(id, null);
            u.ExactNightShiftCount = id == 31 ? 9 : 8;
            users.Add(u);
        }

        var constraints = BuildPediatricsConstraints(start);
        constraints.EndDate = end;
        constraints.UserConstraints = users;

        var solution = new ShiftSolution();
        foreach (var date in failingDates)
        {
            solution.AddAssignment(22, 6, date, ShiftLabel.Night, false);
            solution.AddAssignment(23, 6, date, ShiftLabel.Night, false);
            solution.AddAssignment(27, 6, date, ShiftLabel.Night, false);
            solution.AddAssignment(30, 6, date, ShiftLabel.Night, false);
        }

        // پر کردن سهمیه شب تا Enforce نتواند با جابجایی، L1 را از شب بردارد
        for (var d = start; d <= end; d = d.AddDays(1))
        {
            if (failingDates.Contains(d))
            {
                continue;
            }

            var dayIndex = (d - start).Days;
            if (dayIndex % 4 == 0)
            {
                solution.AddAssignment(22, 6, d, ShiftLabel.Night, false);
            }

            if (dayIndex % 4 == 1)
            {
                solution.AddAssignment(23, 6, d, ShiftLabel.Night, false);
            }

            if (dayIndex % 4 == 2)
            {
                solution.AddAssignment(27, 6, d, ShiftLabel.Night, false);
            }

            if (dayIndex % 4 == 3)
            {
                solution.AddAssignment(31, 6, d, ShiftLabel.Night, false);
            }
        }

        ApplyManagers(constraints, solution);

        foreach (var date in failingDates)
        {
            var nightUsers = GetAssignees(constraints, solution, 6, date);
            Assert.True(
                ShiftManagerRules.IsSatisfied(nightUsers, 2, 1),
                $"Night manager mix still broken on {date:yyyy-MM-dd}");
        }

        Assert.DoesNotContain(
            solution.Violations,
            v => v.Contains("Shift manager mix unmet", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ExactNightQuotaGuard_DoesNotRemoveCriticalManagerFromNight()
    {
        var start = new DateTime(2026, 8, 23);
        var l1 = Make(19, 1);
        var l2a = Make(22, 2);
        l2a.ExactNightShiftCount = 8;
        var l2b = Make(23, 2);
        l2b.ExactNightShiftCount = 8;
        var filler = Make(27, null);
        filler.ExactNightShiftCount = 8;

        var constraints = BuildPediatricsConstraints(start);
        constraints.UserConstraints = [l1, l2a, l2b, filler];

        var solution = new ShiftSolution();
        solution.AddAssignment(19, 6, start, ShiftLabel.Night, false);
        solution.AddAssignment(22, 6, start, ShiftLabel.Night, false);
        solution.AddAssignment(23, 6, start, ShiftLabel.Night, false);
        solution.AddAssignment(27, 6, start, ShiftLabel.Night, false);

        Assert.True(ShiftManagerRules.IsCriticalForManagerMix(constraints, solution, solution.GetShiftAssignments(6, start).First(a => a.UserId == 19)));
        Assert.False(ExactNightQuotaGuard.CanDonateNight(solution, constraints, l1, solution.GetShiftAssignments(6, start).First(a => a.UserId == 19)));
    }

    [Fact]
    public void ValidateShiftManagerCounts_RejectsMinLevel1AboveRequired()
    {
        var error = ShiftManagerRules.ValidateShiftManagerCounts(1, 2);
        Assert.NotNull(error);
    }

    private static void ApplyManagers(ShiftConstraints constraints, ShiftSolution solution)
    {
        var scheduler = new SimulatedAnnealingScheduler(constraints, new SimulatedAnnealingParameters
        {
            MaxIterations = 1,
            InitialTemperature = 1,
            FinalTemperature = 0.1
        });
        scheduler.ApplyMandatoryConstraints(solution);
    }

    private static List<UserConstraint> GetAssignees(
        ShiftConstraints constraints, ShiftSolution solution, int shiftId, DateTime date) =>
        solution.GetShiftAssignments(shiftId, date)
            .Where(a => !a.IsOnCall)
            .Select(a => constraints.UserConstraints.First(u => u.UserId == a.UserId))
            .ToList();

    private static ShiftConstraints BuildPediatricsConstraints(DateTime start) => new()
    {
        StartDate = start,
        EndDate = start,
        ShiftRequirements =
        [
            new ShiftRequirement
            {
                ShiftId = 4,
                ShiftLabel = ShiftLabel.Morning,
                DepartmentId = 2,
                DurationHours = 12,
                ManagerRequiredCount = 0,
                SpecialtyRequirements =
                [
                    new SpecialtyRequirement { SpecialtyId = 2, RequiredTotalCount = 4 }
                ]
            },
            new ShiftRequirement
            {
                ShiftId = 5,
                ShiftLabel = ShiftLabel.Evening,
                DepartmentId = 2,
                DurationHours = 12,
                ManagerRequiredCount = 2,
                ManagerMinLevel1Count = 1,
                SpecialtyRequirements =
                [
                    new SpecialtyRequirement { SpecialtyId = 2, RequiredTotalCount = 3 }
                ]
            },
            new ShiftRequirement
            {
                ShiftId = 6,
                ShiftLabel = ShiftLabel.Night,
                DepartmentId = 2,
                DurationHours = 12,
                ManagerRequiredCount = 2,
                ManagerMinLevel1Count = 1,
                SpecialtyRequirements =
                [
                    new SpecialtyRequirement { SpecialtyId = 2, RequiredTotalCount = 4 }
                ]
            }
        ],
        GlobalConstraints = new GlobalConstraints { MaxShiftsPerDay = 1 },
        HardRules = new HardRuleSet
        {
            EnforceSpecialtyCapacity = true,
            EnforceMaxShiftsPerDay = true,
            AllowEveningAfterNightShift = false,
            AllowNightShiftAfterNightShift = false
        }
    };

    private static UserConstraint MakeFixedMorning(int id, byte? level)
    {
        var u = Make(id, level);
        u.ShiftType = ShiftTypes.FixedShift;
        u.ShiftSubType = ShiftSubTypes.FixedMorning;
        u.AllowedShiftPermissions = UserShiftPermission.Morning;
        u.AllowedShiftLabels = [ShiftLabel.Morning];
        return u;
    }

    private static UserConstraint Make(int id, byte? level) => new()
    {
        UserId = id,
        UserName = $"u{id}",
        Gender = UserGender.Female,
        SpecialtyId = 2,
        IsActive = true,
        ShiftType = ShiftTypes.RotatingShift,
        ShiftSubType = ShiftSubTypes.ThreeShifts,
        AllowedShiftLabels = [ShiftLabel.Morning, ShiftLabel.Evening, ShiftLabel.Night],
        ShiftManagerLevel = level,
        CanBeShiftManager = level.HasValue,
        MaxConsecutiveShifts = 30,
        MinRestDaysBetweenShifts = 0,
        MaxShiftsPerWeek = 7,
        MinDaysBetweenNightShifts = 1
    };
}
