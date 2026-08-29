using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using Xunit;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;
using static ShiftYar.Domain.Enums.UserModel.UserEnums;

namespace ShiftYar.Application.Tests;

public class AdjacentShiftRestRulesTests
{
    [Fact]
    public void EveningThenNight_SameDay_IsForbidden()
    {
        var d = new DateTime(2026, 8, 25);
        Assert.True(AdjacentShiftRestRules.IsForbiddenBackToBack(
            ShiftLabel.Evening, d, ShiftLabel.Night, d));
    }

    [Fact]
    public void NightThenMorning_NextDay_IsForbidden()
    {
        var d = new DateTime(2026, 8, 25);
        Assert.True(AdjacentShiftRestRules.IsForbiddenBackToBack(
            ShiftLabel.Night, d, ShiftLabel.Morning, d.AddDays(1)));
    }

    [Fact]
    public void MorningThenNight_SameDay_IsAllowed()
    {
        var d = new DateTime(2026, 8, 25);
        Assert.False(AdjacentShiftRestRules.IsForbiddenBackToBack(
            ShiftLabel.Morning, d, ShiftLabel.Night, d));
    }

    [Fact]
    public void NightThenEvening_NextDay_AllowedWhenSettingEnabled()
    {
        var d = new DateTime(2026, 8, 25);
        Assert.False(AdjacentShiftRestRules.IsForbiddenBackToBack(
            ShiftLabel.Night, d, ShiftLabel.Evening, d.AddDays(1), allowEveningAfterNightShift: true));
    }

    [Fact]
    public void NightThenEvening_NextDay_ForbiddenWhenSettingDisabled()
    {
        var d = new DateTime(2026, 8, 25);
        Assert.True(AdjacentShiftRestRules.IsForbiddenBackToBack(
            ShiftLabel.Night, d, ShiftLabel.Evening, d.AddDays(1), allowEveningAfterNightShift: false));
    }

    [Fact]
    public void NightThenNight_NextDay_ForbiddenWhenNightSettingDisabled()
    {
        var d = new DateTime(2026, 8, 25);
        Assert.True(AdjacentShiftRestRules.IsForbiddenBackToBack(
            ShiftLabel.Night, d, ShiftLabel.Night, d.AddDays(1),
            allowEveningAfterNightShift: true,
            allowNightShiftAfterNightShift: false));
        Assert.False(AdjacentShiftRestRules.IsForbiddenBackToBack(
            ShiftLabel.Night, d, ShiftLabel.Night, d.AddDays(1),
            allowEveningAfterNightShift: true,
            allowNightShiftAfterNightShift: true));
    }

    [Fact]
    public void NightThenEveningAndNight_NextDay_RespectSeparateSettings()
    {
        var d = new DateTime(2026, 8, 25);
        Assert.True(AdjacentShiftRestRules.IsForbiddenBackToBack(
            ShiftLabel.Night, d, ShiftLabel.Evening, d.AddDays(1),
            allowEveningAfterNightShift: false,
            allowNightShiftAfterNightShift: true));
        Assert.False(AdjacentShiftRestRules.IsForbiddenBackToBack(
            ShiftLabel.Night, d, ShiftLabel.Night, d.AddDays(1),
            allowEveningAfterNightShift: false,
            allowNightShiftAfterNightShift: true));
    }

    [Fact]
    public void NightThenMorning_NextDay_AlwaysForbidden_EvenWhenOtherSettingsEnabled()
    {
        var d = new DateTime(2026, 8, 25);
        Assert.True(AdjacentShiftRestRules.IsForbiddenBackToBack(
            ShiftLabel.Night, d, ShiftLabel.Morning, d.AddDays(1),
            allowEveningAfterNightShift: true,
            allowNightShiftAfterNightShift: true));
    }

    [Fact]
    public void NightThenAnyNextDay_ForbiddenWhenBothSettingsDisabled()
    {
        var d = new DateTime(2026, 8, 25);
        Assert.True(AdjacentShiftRestRules.IsForbiddenBackToBack(
            ShiftLabel.Night, d, ShiftLabel.Morning, d.AddDays(1),
            allowEveningAfterNightShift: false,
            allowNightShiftAfterNightShift: false));
        Assert.True(AdjacentShiftRestRules.IsForbiddenBackToBack(
            ShiftLabel.Night, d, ShiftLabel.Evening, d.AddDays(1),
            allowEveningAfterNightShift: false,
            allowNightShiftAfterNightShift: false));
        Assert.True(AdjacentShiftRestRules.IsForbiddenBackToBack(
            ShiftLabel.Night, d, ShiftLabel.Night, d.AddDays(1),
            allowEveningAfterNightShift: false,
            allowNightShiftAfterNightShift: false));
    }

    [Fact]
    public void EveningThenMorning_NextDay_IsAllowed()
    {
        var d = new DateTime(2026, 8, 25);
        Assert.False(AdjacentShiftRestRules.IsForbiddenBackToBack(
            ShiftLabel.Evening, d, ShiftLabel.Morning, d.AddDays(1)));
    }

    [Fact]
    public void ApprovedConsecutiveNights_OverrideDisabledNightAfterNightSetting()
    {
        var d0 = new DateTime(2026, 8, 25);
        var d1 = d0.AddDays(1);
        var user = new UserConstraint
        {
            UserId = 1,
            UserName = "u1",
            SpecialtyId = 10,
            Gender = UserGender.Female,
            RequiredShiftSlots =
            {
                new ShiftSlotConstraint { Date = d0, ShiftLabel = ShiftLabel.Night },
                new ShiftSlotConstraint { Date = d1, ShiftLabel = ShiftLabel.Night }
            }
        };

        var constraints = new ShiftConstraints
        {
            StartDate = d0,
            EndDate = d1,
            UserConstraints = { user },
            ShiftRequirements =
            {
                new ShiftRequirement
                {
                    ShiftId = 3,
                    ShiftLabel = ShiftLabel.Night,
                    DepartmentId = 1,
                    DurationHours = 12,
                    SpecialtyRequirements =
                    {
                        new SpecialtyRequirement { SpecialtyId = 10, RequiredTotalCount = 1 }
                    }
                }
            },
            HardRules = new HardRuleSet
            {
                AllowEveningAfterNightShift = false,
                AllowNightShiftAfterNightShift = false,
                EnforceSpecialtyCapacity = true
            }
        };

        var solution = new ShiftSolution();
        ApprovedRequestGuard.ForceApply(solution, constraints);

        Assert.True(solution.HasAssignment(1, 3, d0));
        Assert.True(solution.HasAssignment(1, 3, d1));
        Assert.Empty(AdjacentShiftRestGuard.GetViolations(solution, constraints));
        Assert.Empty(ApprovedRequestGuard.GetUnmetViolations(solution, constraints));
        Assert.False(AdjacentShiftRestGuard.HasReportableForbiddenPair(
            user, solution.GetUserAllAssignments(1), constraints.HardRules));
    }

    [Fact]
    public void ApprovedEveningAfterNight_OverridesDisabledEveningSetting()
    {
        var d0 = new DateTime(2026, 8, 25);
        var d1 = d0.AddDays(1);
        var user = new UserConstraint
        {
            UserId = 2,
            UserName = "u2",
            SpecialtyId = 10,
            Gender = UserGender.Male,
            RequiredShiftSlots =
            {
                new ShiftSlotConstraint { Date = d0, ShiftLabel = ShiftLabel.Night },
                new ShiftSlotConstraint { Date = d1, ShiftLabel = ShiftLabel.Evening }
            }
        };

        var constraints = new ShiftConstraints
        {
            StartDate = d0,
            EndDate = d1,
            UserConstraints = { user },
            ShiftRequirements =
            {
                new ShiftRequirement
                {
                    ShiftId = 2,
                    ShiftLabel = ShiftLabel.Evening,
                    DepartmentId = 1,
                    DurationHours = 7,
                    SpecialtyRequirements =
                    {
                        new SpecialtyRequirement { SpecialtyId = 10, RequiredTotalCount = 1 }
                    }
                },
                new ShiftRequirement
                {
                    ShiftId = 3,
                    ShiftLabel = ShiftLabel.Night,
                    DepartmentId = 1,
                    DurationHours = 12,
                    SpecialtyRequirements =
                    {
                        new SpecialtyRequirement { SpecialtyId = 10, RequiredTotalCount = 1 }
                    }
                }
            },
            HardRules = new HardRuleSet
            {
                AllowEveningAfterNightShift = false,
                AllowNightShiftAfterNightShift = false,
                EnforceSpecialtyCapacity = true
            }
        };

        var solution = new ShiftSolution();
        ApprovedRequestGuard.ForceApply(solution, constraints);

        Assert.True(solution.HasAssignment(2, 3, d0));
        Assert.True(solution.HasAssignment(2, 2, d1));
        Assert.Empty(AdjacentShiftRestGuard.GetViolations(solution, constraints));
        Assert.Empty(ApprovedRequestGuard.GetUnmetViolations(solution, constraints));
    }

    [Fact]
    public void StripForbiddenAdjacencies_RemovesEveningWhenNightAndEveningBothManagerCritical()
    {
        var nightDate = new DateTime(2026, 9, 8);
        var eveDate = nightDate.AddDays(1);
        var u21 = new UserConstraint
        {
            UserId = 21,
            UserName = "عاطفه رحیمی منفرد",
            SpecialtyId = 2,
            Gender = UserGender.Female,
            ShiftManagerLevel = 1,
            CanBeShiftManager = true,
            IsActive = true
        };
        var u22 = new UserConstraint
        {
            UserId = 22,
            SpecialtyId = 2,
            Gender = UserGender.Female,
            ShiftManagerLevel = 2,
            CanBeShiftManager = true,
            IsActive = true
        };

        var constraints = new ShiftConstraints
        {
            StartDate = nightDate,
            EndDate = eveDate,
            UserConstraints = { u21, u22 },
            ShiftRequirements =
            {
                new ShiftRequirement
                {
                    ShiftId = 5,
                    ShiftLabel = ShiftLabel.Evening,
                    DepartmentId = 2,
                    DurationHours = 12,
                    ManagerRequiredCount = 2,
                    ManagerMinLevel1Count = 1,
                    SpecialtyRequirements =
                    {
                        new SpecialtyRequirement { SpecialtyId = 2, RequiredTotalCount = 3 }
                    }
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
                    {
                        new SpecialtyRequirement { SpecialtyId = 2, RequiredTotalCount = 4 }
                    }
                }
            },
            HardRules = new HardRuleSet
            {
                AllowEveningAfterNightShift = false,
                AllowNightShiftAfterNightShift = false,
                EnforceSpecialtyCapacity = true,
                EnforceMaxShiftsPerDay = true
            },
            GlobalConstraints = new GlobalConstraints { MaxShiftsPerDay = 1 }
        };

        var solution = new ShiftSolution();
        solution.AddAssignment(21, 6, nightDate, ShiftLabel.Night, false);
        solution.AddAssignment(22, 6, nightDate, ShiftLabel.Night, false);
        solution.AddAssignment(21, 5, eveDate, ShiftLabel.Evening, false);
        solution.AddAssignment(22, 5, eveDate, ShiftLabel.Evening, false);

        Assert.True(ShiftManagerRules.IsCriticalForManagerMix(constraints, solution,
            solution.GetShiftAssignments(6, nightDate).First(a => a.UserId == 21)));
        Assert.True(ShiftManagerRules.IsCriticalForManagerMix(constraints, solution,
            solution.GetShiftAssignments(5, eveDate).First(a => a.UserId == 21)));

        AdjacentShiftRestGuard.StripForbiddenAdjacencies(solution, constraints);

        Assert.True(solution.HasAssignment(21, 6, nightDate));
        Assert.False(solution.HasAssignment(21, 5, eveDate));
        Assert.Empty(AdjacentShiftRestGuard.GetViolations(solution, constraints));
    }

    [Fact]
    public void StripForbiddenAdjacencies_BreaksQuotaNightThenNightThenEvening_User21()
    {
        var n0 = new DateTime(2026, 9, 4);
        var n1 = new DateTime(2026, 9, 5);
        var eve = new DateTime(2026, 9, 6);
        var u21 = new UserConstraint
        {
            UserId = 21,
            UserName = "عاطفه رحیمی منفرد",
            SpecialtyId = 2,
            Gender = UserGender.Female,
            ShiftManagerLevel = 1,
            CanBeShiftManager = true,
            IsActive = true,
            ExactNightShiftCount = 2
        };

        var constraints = new ShiftConstraints
        {
            StartDate = n0,
            EndDate = eve,
            UserConstraints = { u21 },
            ShiftRequirements =
            {
                new ShiftRequirement
                {
                    ShiftId = 5,
                    ShiftLabel = ShiftLabel.Evening,
                    DepartmentId = 2,
                    DurationHours = 12,
                    SpecialtyRequirements =
                    {
                        new SpecialtyRequirement { SpecialtyId = 2, RequiredTotalCount = 1 }
                    }
                },
                new ShiftRequirement
                {
                    ShiftId = 6,
                    ShiftLabel = ShiftLabel.Night,
                    DepartmentId = 2,
                    DurationHours = 12,
                    SpecialtyRequirements =
                    {
                        new SpecialtyRequirement { SpecialtyId = 2, RequiredTotalCount = 1 }
                    }
                }
            },
            HardRules = new HardRuleSet
            {
                AllowEveningAfterNightShift = false,
                AllowNightShiftAfterNightShift = false,
                EnforceSpecialtyCapacity = true
            }
        };

        var solution = new ShiftSolution();
        solution.AddAssignment(21, 6, n0, ShiftLabel.Night, false);
        solution.AddAssignment(21, 6, n1, ShiftLabel.Night, false);
        solution.AddAssignment(21, 5, eve, ShiftLabel.Evening, false);

        AdjacentShiftRestGuard.StripForbiddenAdjacencies(solution, constraints);

        Assert.Empty(AdjacentShiftRestGuard.GetViolations(solution, constraints));
        Assert.False(
            AdjacentShiftRestRules.HasForbiddenAdjacentPair(
                solution.GetUserAllAssignments(21), constraints.HardRules));
    }

    [Fact]
    public void Optimize_NeverAssignsForbiddenAdjacencies()
    {
        var start = new DateTime(2026, 8, 23);
        var specialty = new SpecialtyRequirement
        {
            SpecialtyId = 10,
            RequiredTotalCount = 1
        };

        UserConstraint U(int id, UserGender g) => new()
        {
            UserId = id,
            Gender = g,
            SpecialtyId = 10,
            IsActive = true,
            ShiftType = ShiftTypes.RotatingShift,
            ShiftSubType = ShiftSubTypes.ThreeShifts,
            AllowedShiftLabels =
            [
                ShiftLabel.Morning, ShiftLabel.Evening, ShiftLabel.Night
            ],
            MaxConsecutiveShifts = 14,
            MinRestDaysBetweenShifts = 0,
            MaxShiftsPerWeek = 14
        };

        var constraints = new ShiftConstraints
        {
            DepartmentId = 1,
            StartDate = start,
            EndDate = start.AddDays(6),
            UserConstraints = new List<UserConstraint>
            {
                U(1, UserGender.Male), U(2, UserGender.Female),
                U(3, UserGender.Male), U(4, UserGender.Female),
                U(5, UserGender.Male), U(6, UserGender.Female),
            },
            ShiftRequirements = new List<ShiftRequirement>
            {
                new()
                {
                    ShiftId = 1, ShiftLabel = ShiftLabel.Morning, DepartmentId = 1, DurationHours = 8,
                    SpecialtyRequirements = new List<SpecialtyRequirement> { Clone(specialty) }
                },
                new()
                {
                    ShiftId = 2, ShiftLabel = ShiftLabel.Evening, DepartmentId = 1, DurationHours = 8,
                    SpecialtyRequirements = new List<SpecialtyRequirement> { Clone(specialty) }
                },
                new()
                {
                    ShiftId = 3, ShiftLabel = ShiftLabel.Night, DepartmentId = 1, DurationHours = 8,
                    SpecialtyRequirements = new List<SpecialtyRequirement> { Clone(specialty) }
                }
            },
            HardRules = new HardRuleSet
            {
                ForbidDuplicateDailyAssignments = true,
                EnforceMaxShiftsPerDay = true,
                EnforceMinRestDays = false,
                EnforceMaxConsecutiveShifts = false,
                EnforceSpecialtyCapacity = true
            },
            GlobalConstraints = new GlobalConstraints { MaxShiftsPerDay = 1 }
        };

        var parameters = new SimulatedAnnealingParameters
        {
            InitialTemperature = 400,
            FinalTemperature = 0.1,
            CoolingRate = 0.93,
            MaxIterations = 1500,
            MaxIterationsWithoutImprovement = 250,
            PenaltyWeight = 1000
        };

        for (var run = 0; run < 5; run++)
        {
            var solution = new SimulatedAnnealingScheduler(constraints, parameters).Optimize();
            Assert.Empty(AdjacentShiftRestGuard.GetViolations(solution, constraints));
            foreach (var user in constraints.UserConstraints)
            {
                Assert.False(
                    AdjacentShiftRestRules.HasForbiddenAdjacentPair(
                        solution.GetUserAllAssignments(user.UserId)));
            }
        }
    }

    private static SpecialtyRequirement Clone(SpecialtyRequirement s) => new()
    {
        SpecialtyId = s.SpecialtyId,
        RequiredTotalCount = s.RequiredTotalCount
    };
}
