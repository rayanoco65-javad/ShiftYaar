namespace ShiftYar.Application.Common.Utilities;

/// <summary>
/// سقف سهمیه هر کاربر بر اساس تعداد شب‌های همان ماه در تقویم ShiftDates.
/// </summary>
public static class UserMonthlyNightQuotaLimits
{
    public static string? Validate(
        int? exactNightShiftCount,
        int? exactHolidayWeekendNightShiftCount,
        int nightDaysInMonth,
        int holidayWeekendNightDaysInMonth,
        int persianYear,
        int persianMonth)
    {
        if (exactNightShiftCount.HasValue && exactNightShiftCount.Value > nightDaysInMonth)
        {
            return
                $"تعداد شیفت شب ({exactNightShiftCount.Value}) نمی‌تواند بیشتر از تعداد شب‌های ماه " +
                $"{persianYear}/{persianMonth:00} ({nightDaysInMonth} شب) باشد.";
        }

        if (exactHolidayWeekendNightShiftCount.HasValue &&
            exactHolidayWeekendNightShiftCount.Value > holidayWeekendNightDaysInMonth)
        {
            return
                $"تعداد شب تعطیل/آخرهفته ({exactHolidayWeekendNightShiftCount.Value}) نمی‌تواند بیشتر از " +
                $"تعداد شب‌های تعطیل/آخرهفته ماه {persianYear}/{persianMonth:00} " +
                $"({holidayWeekendNightDaysInMonth} شب) باشد.";
        }

        return null;
    }
}
