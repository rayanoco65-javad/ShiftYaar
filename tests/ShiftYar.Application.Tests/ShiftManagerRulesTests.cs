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
    public void ValidateShiftManagerCounts_RejectsMinLevel1AboveRequired()
    {
        var error = ShiftManagerRules.ValidateShiftManagerCounts(1, 2);
        Assert.NotNull(error);
    }

    private static UserConstraint Make(int id, byte? level) => new()
    {
        UserId = id,
        UserName = $"u{id}",
        Gender = UserGender.Female,
        SpecialtyId = 10,
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
