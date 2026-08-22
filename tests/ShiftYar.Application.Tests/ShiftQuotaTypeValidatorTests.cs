using ShiftYar.Application.Common.Utilities;
using ShiftYar.Domain.Entities.UserModel;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;
using Xunit;

namespace ShiftYar.Application.Tests;

public class ShiftQuotaTypeValidatorTests
{
    [Fact]
    public void RejectsMorningEveningQuotaForFixedMorningUser()
    {
        var user = new User
        {
            Id = 1,
            FullName = "Ali",
            ShiftType = ShiftTypes.FixedShift,
            ShiftSubType = ShiftSubTypes.FixedMorning
        };

        var error = ShiftQuotaTypeValidator.ValidateMorningEveningQuotaAllowed(
            user, UserShiftPermission.Morning);

        Assert.NotNull(error);
        Assert.Contains("صبح/عصر", error);
    }

    [Fact]
    public void AllowsMorningEveningQuotaForTwoShiftRotation()
    {
        var user = new User
        {
            Id = 2,
            ShiftType = ShiftTypes.RotatingShift,
            ShiftSubType = ShiftSubTypes.TwoShifts,
            TwoShiftRotationPattern = TwoShiftRotationPattern.MorningEvening
        };

        var error = ShiftQuotaTypeValidator.ValidateMorningEveningQuotaAllowed(
            user,
            UserShiftPermission.Morning
            | UserShiftPermission.Evening
            | UserShiftPermission.MorningEveningSameDay);

        Assert.Null(error);
    }

    [Fact]
    public void AllowsMorningNightQuotaForThreeShiftUser()
    {
        var user = new User
        {
            Id = 3,
            ShiftType = ShiftTypes.RotatingShift,
            ShiftSubType = ShiftSubTypes.ThreeShifts
        };

        var error = ShiftQuotaTypeValidator.ValidateMorningNightQuotaAllowed(
            user,
            UserShiftPermission.Morning
            | UserShiftPermission.Evening
            | UserShiftPermission.Night
            | UserShiftPermission.MorningEveningSameDay
            | UserShiftPermission.MorningNightSameDay);

        Assert.Null(error);
    }

    [Fact]
    public void AllowsMorningEveningQuotaForThreeShiftUser()
    {
        var user = new User
        {
            Id = 4,
            ShiftType = ShiftTypes.RotatingShift,
            ShiftSubType = ShiftSubTypes.ThreeShifts
        };

        var error = ShiftQuotaTypeValidator.ValidateMorningEveningQuotaAllowed(
            user,
            UserShiftPermission.Morning
            | UserShiftPermission.Evening
            | UserShiftPermission.MorningEveningSameDay);

        Assert.Null(error);
    }
}

public class MaxShiftsPerDayRulesTests
{
    [Theory]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    [InlineData(null, true)]
    public void ValidatesAllowedSettings(int? value, bool expected)
    {
        Assert.Equal(expected, MaxShiftsPerDayRules.IsValidSetting(value));
    }

    [Fact]
    public void BlocksSecondShiftWhenMaxIsOne()
    {
        Assert.True(MaxShiftsPerDayRules.WouldExceedDailyLimit(1, 1, enforce: true));
        Assert.False(MaxShiftsPerDayRules.WouldExceedDailyLimit(1, 2, enforce: true));
    }
}
