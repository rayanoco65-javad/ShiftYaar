using ShiftYar.Application.Common.Utilities;
using Xunit;

namespace ShiftYar.Application.Tests;

public class UserMonthlyNightQuotaLimitsTests
{
    [Fact]
    public void Validate_RejectsNightQuotaAboveMonthNightDays()
    {
        var error = UserMonthlyNightQuotaLimits.Validate(
            exactNightShiftCount: 32,
            exactHolidayWeekendNightShiftCount: null,
            nightDaysInMonth: 31,
            holidayWeekendNightDaysInMonth: 10,
            persianYear: 1405,
            persianMonth: 6);

        Assert.NotNull(error);
        Assert.Contains("31", error);
        Assert.Contains("32", error);
    }

    [Fact]
    public void Validate_AllowsNightQuotaEqualToMonthNightDays()
    {
        var error = UserMonthlyNightQuotaLimits.Validate(
            exactNightShiftCount: 31,
            exactHolidayWeekendNightShiftCount: 5,
            nightDaysInMonth: 31,
            holidayWeekendNightDaysInMonth: 10,
            persianYear: 1405,
            persianMonth: 6);

        Assert.Null(error);
    }

    [Fact]
    public void Validate_RejectsHolidayQuotaAboveMonthHolidayWeekendNights()
    {
        var error = UserMonthlyNightQuotaLimits.Validate(
            exactNightShiftCount: 5,
            exactHolidayWeekendNightShiftCount: 11,
            nightDaysInMonth: 31,
            holidayWeekendNightDaysInMonth: 10,
            persianYear: 1405,
            persianMonth: 6);

        Assert.NotNull(error);
        Assert.Contains("تعطیل", error);
    }

    [Fact]
    public void Validate_AllowsNullQuotas()
    {
        var error = UserMonthlyNightQuotaLimits.Validate(
            null,
            null,
            nightDaysInMonth: 31,
            holidayWeekendNightDaysInMonth: 10,
            persianYear: 1405,
            persianMonth: 6);

        Assert.Null(error);
    }
}
