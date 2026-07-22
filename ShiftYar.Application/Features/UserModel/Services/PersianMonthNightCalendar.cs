using ShiftYar.Application.Common.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ShiftYar.Application.Features.UserModel.Services;

/// <summary>
/// محاسبه ظرفیت شب و شبِ تعطیل/آخرهفته یک ماه شمسی از روی تقویم ShiftDates.
/// </summary>
public static class PersianMonthNightCalendar
{
    private static readonly PersianCalendar PersianCalendar = new();

    public static (DateTime MonthStart, DateTime MonthEnd, int DaysInMonth) GetMonthBounds(int persianYear, int persianMonth)
    {
        var daysInMonth = PersianCalendar.GetDaysInMonth(persianYear, persianMonth);
        var monthStart = PersianCalendar.ToDateTime(persianYear, persianMonth, 1, 0, 0, 0, 0).Date;
        var monthEnd = PersianCalendar.ToDateTime(persianYear, persianMonth, daysInMonth, 0, 0, 0, 0).Date;
        return (monthStart, monthEnd, daysInMonth);
    }

    /// <summary>
    /// تعداد شب‌های ماه و تعداد شب‌های تعطیل/آخرهفته را از روزهای موجود در تقویم می‌شمارد.
    /// برای تشخیص شب قبل از تعطیل، مجموعه تعطیل باید حداقل یک روز بعد از ماه را هم پوشش دهد.
    /// </summary>
    public static (int NightDays, int HolidayWeekendNightDays) CountNightCapacities(
        IReadOnlyCollection<DateTime> monthDates,
        ISet<DateTime> holidayDates)
    {
        var distinctDays = monthDates.Select(d => d.Date).Distinct().OrderBy(d => d).ToList();
        var nightDays = distinctDays.Count;
        var holidayWeekendNights = distinctDays.Count(d =>
            HolidayWeekendNightRules.IsHolidayWeekendNight(d, holidayDates));
        return (nightDays, holidayWeekendNights);
    }
}
