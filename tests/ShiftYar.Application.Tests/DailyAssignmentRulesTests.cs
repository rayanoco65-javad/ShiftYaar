using ShiftYar.Application.Common.Utilities;
using Xunit;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.Tests;

public class DailyAssignmentRulesTests
{
    [Fact]
    public void MorningThenEvening_SameDay_IsAllowed()
    {
        Assert.True(DailyAssignmentRules.CanAddShift(
            new[] { ShiftLabel.Morning }, ShiftLabel.Evening, maxShiftsPerDay: 2));
        Assert.True(DailyAssignmentRules.IsValidDaySet(
            new[] { ShiftLabel.Morning, ShiftLabel.Evening }, maxShiftsPerDay: 2));
    }

    [Fact]
    public void DuplicateMorning_IsForbidden()
    {
        Assert.False(DailyAssignmentRules.CanAddShift(
            new[] { ShiftLabel.Morning }, ShiftLabel.Morning, maxShiftsPerDay: 2));
    }

    [Fact]
    public void NightWithMorning_IsForbidden()
    {
        Assert.False(DailyAssignmentRules.CanAddShift(
            new[] { ShiftLabel.Morning }, ShiftLabel.Night, maxShiftsPerDay: 2));
        Assert.False(DailyAssignmentRules.IsValidDaySet(
            new[] { ShiftLabel.Morning, ShiftLabel.Night }, maxShiftsPerDay: 2));
    }

    [Fact]
    public void NightAlone_IsAllowed()
    {
        Assert.True(DailyAssignmentRules.CanAddShift(
            Array.Empty<ShiftLabel>(), ShiftLabel.Night, maxShiftsPerDay: 2));
        Assert.True(DailyAssignmentRules.IsValidDaySet(
            new[] { ShiftLabel.Night }, maxShiftsPerDay: 2));
    }
}
