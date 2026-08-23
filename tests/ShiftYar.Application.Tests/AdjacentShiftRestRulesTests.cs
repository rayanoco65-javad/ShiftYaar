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
