using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.Features.UserModel.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Xunit;

namespace ShiftYar.Application.Tests;

public class PersianMonthNightCalendarTests
{
    [Fact]
    public void CountNightCapacities_CountsAllDaysAndHolidayWeekendNights()
    {
        var pc = new PersianCalendar();
        var year = 1405;
        var month = 4;
        var (start, end, days) = PersianMonthNightCalendar.GetMonthBounds(year, month);

        var monthDates = Enumerable.Range(0, days).Select(i => start.AddDays(i)).ToList();
        Assert.Equal(end, monthDates[^1]);

        // جمعه‌ها را تعطیل فرض می‌کنیم + یک روز اضافی بعد از ماه برای مرز
        var holidays = monthDates
            .Append(end.AddDays(1))
            .Where(d => d.DayOfWeek == DayOfWeek.Friday)
            .ToHashSet();

        var (nightDays, hwn) = PersianMonthNightCalendar.CountNightCapacities(monthDates, holidays);

        Assert.Equal(days, nightDays);
        Assert.True(hwn > 0);
        Assert.True(hwn <= nightDays);

        // هر جمعه و پنجشنبه قبلش باید شمرده شوند
        var expected = monthDates.Count(d => HolidayWeekendNightRules.IsHolidayWeekendNight(d, holidays));
        Assert.Equal(expected, hwn);
    }

    [Fact]
    public void GetMonthBounds_Tir1405_Has31Days()
    {
        var (_, _, days) = PersianMonthNightCalendar.GetMonthBounds(1405, 4);
        Assert.Equal(31, days);
    }
}
