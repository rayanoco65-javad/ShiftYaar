using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.Features.UserModel.Services;
using ShiftYar.Domain.Entities.ShiftModel;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Xunit;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.Tests;

public class DepartmentNightQuotaCapacityCalculatorTests
{
    [Fact]
    public void Calculate_WithFourPediatriciansPerNightAnd31Days_Returns124TotalSlots()
    {
        var (start, _, days) = PersianMonthNightCalendar.GetMonthBounds(1405, 6);
        var monthDates = Enumerable.Range(0, days).Select(i => start.AddDays(i)).ToList();
        var holidays = new HashSet<DateTime>();

        var nightShift = new Shift
        {
            Id = 1,
            DepartmentId = 10,
            Label = ShiftLabel.Night,
            RequiredSpecialties = new List<ShiftRequiredSpecialty>
            {
                new()
                {
                    SpecialtyId = 1,
                    RequiredTottalCount = 4
                }
            }
        };

        var (totalSlots, _, headcountRegular, _) =
            DepartmentNightQuotaCapacityCalculator.Calculate(new[] { nightShift }, monthDates, holidays);

        Assert.Equal(4, headcountRegular);
        Assert.Equal(31 * 4, totalSlots);
        Assert.Equal(124, totalSlots);
    }

    [Fact]
    public void Calculate_WithoutSpecialtyRequirements_FallsBackToOnePersonPerNight()
    {
        var (start, _, days) = PersianMonthNightCalendar.GetMonthBounds(1405, 6);
        var monthDates = Enumerable.Range(0, days).Select(i => start.AddDays(i)).ToList();

        var nightShift = new Shift
        {
            Id = 1,
            DepartmentId = 10,
            Label = ShiftLabel.Night,
            RequiredSpecialties = new List<ShiftRequiredSpecialty>()
        };

        var (totalSlots, _, headcountRegular, _) =
            DepartmentNightQuotaCapacityCalculator.Calculate(new[] { nightShift }, monthDates, new HashSet<DateTime>());

        Assert.Equal(1, headcountRegular);
        Assert.Equal(days, totalSlots);
    }

    [Fact]
    public void Calculate_SumsMultipleSpecialtyRowsOnNightShift()
    {
        var monthDates = new[] { new DateTime(2026, 8, 17) };
        var nightShift = new Shift
        {
            Label = ShiftLabel.Night,
            RequiredSpecialties = new List<ShiftRequiredSpecialty>
            {
                new() { RequiredTottalCount = 2 },
                new() { RequiredTottalCount = 4 }
            }
        };

        var (totalSlots, _, headcountRegular, _) =
            DepartmentNightQuotaCapacityCalculator.Calculate(new[] { nightShift }, monthDates, new HashSet<DateTime>());

        Assert.Equal(6, headcountRegular);
        Assert.Equal(6, totalSlots);
    }

    [Fact]
    public void Calculate_HolidayWeekendNightSlots_UseHolidayHeadcountWhenDefined()
    {
        var pc = new PersianCalendar();
        var year = 1405;
        var month = 4;
        var (start, end, days) = PersianMonthNightCalendar.GetMonthBounds(year, month);
        var monthDates = Enumerable.Range(0, days).Select(i => start.AddDays(i)).ToList();
        var holidays = monthDates
            .Append(end.AddDays(1))
            .Where(d => d.DayOfWeek == DayOfWeek.Friday)
            .ToHashSet();

        var nightShift = new Shift
        {
            Label = ShiftLabel.Night,
            RequiredSpecialties = new List<ShiftRequiredSpecialty>
            {
                new()
                {
                    RequiredTottalCount = 4,
                    HolidayRequiredTottalCount = 5
                }
            }
        };

        var (_, holidayWeekendSlots, _, headcountHoliday) =
            DepartmentNightQuotaCapacityCalculator.Calculate(new[] { nightShift }, monthDates, holidays);

        var holidayWeekendNightDays = monthDates.Count(d =>
            HolidayWeekendNightRules.IsHolidayWeekendNight(d, holidays));

        var expectedHolidayWeekendSlots = monthDates
            .Where(d => HolidayWeekendNightRules.IsHolidayWeekendNight(d, holidays))
            .Sum(d => holidays.Contains(d) ? headcountHoliday : 4);

        Assert.Equal(5, headcountHoliday);
        Assert.Equal(holidayWeekendNightDays, monthDates.Count(d =>
            HolidayWeekendNightRules.IsHolidayWeekendNight(d, holidays)));
        Assert.Equal(expectedHolidayWeekendSlots, holidayWeekendSlots);
    }
}
