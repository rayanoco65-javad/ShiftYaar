namespace ShiftYar.Application.Common.Utilities;

public static class UserMonthlyComboShiftQuotaLimits
{
    public static string? Validate(
        int? morningEveningShiftCount,
        int? morningEveningHolidayCount,
        int? morningNightShiftCount,
        int? morningNightHolidayCount,
        int monthDays,
        int holidayDays,
        int persianYear,
        int persianMonth)
    {
        if (morningEveningShiftCount.HasValue && morningEveningShiftCount.Value > monthDays * 2)
        {
            return $"سهمیه ترکیبی صبح/عصر ({morningEveningShiftCount}) از حداکثر مجاز ({monthDays * 2}) در {persianYear}/{persianMonth} بیشتر است.";
        }

        if (morningEveningHolidayCount.HasValue && morningEveningHolidayCount.Value > holidayDays * 2)
        {
            return $"سهمیه تعطیل صبح/عصر ({morningEveningHolidayCount}) از تعداد روزهای تعطیل×۲ ({holidayDays * 2}) بیشتر است.";
        }

        if (morningEveningShiftCount.HasValue && morningEveningHolidayCount.HasValue
            && morningEveningHolidayCount.Value > morningEveningShiftCount.Value)
        {
            return "سهمیه تعطیل صبح/عصر نمی‌تواند بیشتر از سهمیه کل صبح/عصر باشد.";
        }

        if (morningNightShiftCount.HasValue && morningNightShiftCount.Value > monthDays * 2)
        {
            return $"سهمیه ترکیبی صبح/شب ({morningNightShiftCount}) از حداکثر مجاز ({monthDays * 2}) در {persianYear}/{persianMonth} بیشتر است.";
        }

        if (morningNightHolidayCount.HasValue && morningNightHolidayCount.Value > holidayDays * 2)
        {
            return $"سهمیه تعطیل صبح/شب ({morningNightHolidayCount}) از تعداد روزهای تعطیل×۲ ({holidayDays * 2}) بیشتر است.";
        }

        if (morningNightShiftCount.HasValue && morningNightHolidayCount.HasValue
            && morningNightHolidayCount.Value > morningNightShiftCount.Value)
        {
            return "سهمیه تعطیل صبح/شب نمی‌تواند بیشتر از سهمیه کل صبح/شب باشد.";
        }

        return null;
    }
}
