using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using Xunit;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.Tests;

public class UserShiftPermissionTests
{
    private static UserConstraint MinaLikeUser() => new()
    {
        UserId = 1,
        AllowedShiftPermissions =
            UserShiftPermission.Morning
            | UserShiftPermission.Evening
            | UserShiftPermission.MorningNightSameDay
    };

    [Fact]
    public void Mina_MorningAndEveningStandalone_Allowed()
    {
        var user = MinaLikeUser();
        Assert.True(ShiftEligibilityResolver.CanAssignShift(
            user.AllowedShiftPermissions, [], ShiftLabel.Morning, 2));
        Assert.True(ShiftEligibilityResolver.CanAssignShift(
            user.AllowedShiftPermissions, [], ShiftLabel.Evening, 2));
    }

    [Fact]
    public void Mina_NightStandalone_NotAllowed()
    {
        var user = MinaLikeUser();
        Assert.False(ShiftEligibilityResolver.CanAssignShift(
            user.AllowedShiftPermissions, [], ShiftLabel.Night, 2));
    }

    [Fact]
    public void Mina_MorningEveningSameDay_NotAllowed()
    {
        var user = MinaLikeUser();
        Assert.False(ShiftEligibilityResolver.CanAssignShift(
            user.AllowedShiftPermissions, [ShiftLabel.Morning], ShiftLabel.Evening, 2));
    }

    [Fact]
    public void Mina_MorningNightSameDay_Allowed()
    {
        var user = MinaLikeUser();
        Assert.True(ShiftEligibilityResolver.CanAssignShift(
            user.AllowedShiftPermissions, [ShiftLabel.Morning], ShiftLabel.Night, 2));
    }

    [Fact]
    public void MayEverTakeLabel_NightTrueWhenMorningNightComboWithoutStandaloneNight()
    {
        var user = MinaLikeUser();
        Assert.True(ShiftEligibilityResolver.MayEverTakeLabel(user, ShiftLabel.Night));
    }

    [Fact]
    public void NormalizePermissions_AddsSinglesForCombos()
    {
        var normalized = ShiftEligibilityResolver.NormalizePermissions(
            UserShiftPermission.MorningEveningSameDay);
        Assert.True(normalized.HasFlag(UserShiftPermission.Morning));
        Assert.True(normalized.HasFlag(UserShiftPermission.Evening));
    }

    [Fact]
    public void MapLegacy_ThreeShifts_IncludesAllPermissions()
    {
        var mapped = ShiftEligibilityResolver.MapLegacyToPermissions(
            ShiftTypes.RotatingShift, ShiftSubTypes.ThreeShifts, null);
        Assert.Equal(ShiftEligibilityResolver.AllPermissions, mapped);
    }

    [Theory]
    [InlineData(ShiftTypes.FixedShift, ShiftSubTypes.FixedMorning, null, UserShiftPermission.Morning)]
    [InlineData(ShiftTypes.RotatingShift, ShiftSubTypes.TwoShifts, TwoShiftRotationPattern.MorningEvening,
        UserShiftPermission.Morning | UserShiftPermission.Evening | UserShiftPermission.MorningEveningSameDay)]
    public void MapLegacy_MatchesExpected(
        ShiftTypes type,
        ShiftSubTypes subType,
        TwoShiftRotationPattern? pattern,
        UserShiftPermission expected)
    {
        Assert.Equal(expected, ShiftEligibilityResolver.MapLegacyToPermissions(type, subType, pattern));
    }
}
