using ShiftYar.Application.Common.Utilities;
using Xunit;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.Tests;

public class ShiftEligibilityResolverTests
{
    [Theory]
    [InlineData(ShiftTypes.FixedShift, ShiftSubTypes.FixedMorning, null, new[] { ShiftLabel.Morning })]
    [InlineData(ShiftTypes.FixedShift, ShiftSubTypes.FixedEvening, null, new[] { ShiftLabel.Evening })]
    [InlineData(ShiftTypes.RotatingShift, ShiftSubTypes.ThreeShifts, null,
        new[] { ShiftLabel.Morning, ShiftLabel.Evening, ShiftLabel.Night })]
    [InlineData(ShiftTypes.RotatingShift, ShiftSubTypes.TwoShifts, TwoShiftRotationPattern.MorningEvening,
        new[] { ShiftLabel.Morning, ShiftLabel.Evening })]
    [InlineData(ShiftTypes.RotatingShift, ShiftSubTypes.TwoShifts, TwoShiftRotationPattern.MorningNight,
        new[] { ShiftLabel.Morning, ShiftLabel.Night })]
    [InlineData(ShiftTypes.RotatingShift, ShiftSubTypes.TwoShifts, TwoShiftRotationPattern.EveningNight,
        new[] { ShiftLabel.Evening, ShiftLabel.Night })]
    public void GetAllowedLabels_MatchesShiftProfile(
        ShiftTypes type,
        ShiftSubTypes subType,
        TwoShiftRotationPattern? pattern,
        ShiftLabel[] expected)
    {
        var allowed = ShiftEligibilityResolver.GetAllowedLabels(type, subType, pattern);
        Assert.Equal(expected, allowed);
    }

    [Fact]
    public void IsLabelAllowed_EmptyList_AllowsAll()
    {
        Assert.True(ShiftEligibilityResolver.IsLabelAllowed(Array.Empty<ShiftLabel>(), ShiftLabel.Night));
    }
}
