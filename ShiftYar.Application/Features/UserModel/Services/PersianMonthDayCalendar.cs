using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ShiftYar.Application.Features.UserModel.Services;

/// <summary>
/// محاسبه تعداد روزها و روزهای تعطیل یک ماه شمسی از روی ShiftDates.
/// </summary>
public static class PersianMonthDayCalendar
{
    private static readonly PersianCalendar PersianCalendar = new();

    public static (DateTime MonthStart, DateTime MonthEnd, int DaysInMonth) GetMonthBounds(int persianYear, int persianMonth)
    {
        var daysInMonth = PersianCalendar.GetDaysInMonth(persianYear, persianMonth);
        var monthStart = PersianCalendar.ToDateTime(persianYear, persianMonth, 1, 0, 0, 0, 0).Date;
        var monthEnd = PersianCalendar.ToDateTime(persianYear, persianMonth, daysInMonth, 0, 0, 0, 0).Date;
        return (monthStart, monthEnd, daysInMonth);
    }

    public static (int DaysInMonth, int HolidayDays) CountDayCapacities(
        IReadOnlyCollection<DateTime> monthDates,
        ISet<DateTime> holidayDates) =>
        (monthDates.Select(d => d.Date).Distinct().Count(),
         monthDates.Select(d => d.Date).Distinct().Count(d => holidayDates.Contains(d)));
}
