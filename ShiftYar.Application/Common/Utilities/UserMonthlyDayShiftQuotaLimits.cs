namespace ShiftYar.Application.Common.Utilities;

public static class UserMonthlyDayShiftQuotaLimits
{
    public static string? ValidateMorning(
        int? exactMorningShiftCount,
        int? exactHolidayMorningShiftCount,
        int daysInMonth,
        int holidayDaysInMonth,
        int persianYear,
        int persianMonth)
    {
        if (exactMorningShiftCount.HasValue && exactMorningShiftCount.Value > daysInMonth)
        {
            return
                $"تعداد شیفت صبح ({exactMorningShiftCount.Value}) نمی‌تواند بیشتر از تعداد روزهای ماه " +
                $"{persianYear}/{persianMonth:00} ({daysInMonth} روز) باشد.";
        }

        if (exactHolidayMorningShiftCount.HasValue &&
            exactHolidayMorningShiftCount.Value > holidayDaysInMonth)
        {
            return
                $"تعداد شیفت صبح تعطیل ({exactHolidayMorningShiftCount.Value}) نمی‌تواند بیشتر از " +
                $"تعداد روزهای تعطیل ماه {persianYear}/{persianMonth:00} ({holidayDaysInMonth} روز) باشد.";
        }

        return null;
    }

    public static string? ValidateEvening(
        int? exactEveningShiftCount,
        int? exactHolidayEveningShiftCount,
        int daysInMonth,
        int holidayDaysInMonth,
        int persianYear,
        int persianMonth)
    {
        if (exactEveningShiftCount.HasValue && exactEveningShiftCount.Value > daysInMonth)
        {
            return
                $"تعداد شیفت عصر ({exactEveningShiftCount.Value}) نمی‌تواند بیشتر از تعداد روزهای ماه " +
                $"{persianYear}/{persianMonth:00} ({daysInMonth} روز) باشد.";
        }

        if (exactHolidayEveningShiftCount.HasValue &&
            exactHolidayEveningShiftCount.Value > holidayDaysInMonth)
        {
            return
                $"تعداد شیفت عصر تعطیل ({exactHolidayEveningShiftCount.Value}) نمی‌تواند بیشتر از " +
                $"تعداد روزهای تعطیل ماه {persianYear}/{persianMonth:00} ({holidayDaysInMonth} روز) باشد.";
        }

        return null;
    }
}
