using System;
using System.Collections.Generic;

namespace ShiftYar.Application.Common.Utilities;

/// <summary>
/// شبِ «آخر هفته / تعطیل» برای سهمیه دقیق:
/// - شب همان روز تعطیل، یا
/// - شب روز غیرتعطیلِ بلافاصله قبل از تعطیل (مثلاً پنجشنبه قبل از جمعه؛
///   اگر پنجشنبه هم تعطیل باشد، چهارشنبه قبل از زنجیرهٔ تعطیل).
/// </summary>
public static class HolidayWeekendNightRules
{
    public static bool IsHolidayWeekendNight(DateTime nightDate, Func<DateTime, bool> isHoliday)
    {
        var d = nightDate.Date;
        return isHoliday(d) || isHoliday(d.AddDays(1));
    }

    public static bool IsHolidayWeekendNight(DateTime nightDate, ISet<DateTime> holidayDates)
    {
        var d = nightDate.Date;
        return holidayDates.Contains(d) || holidayDates.Contains(d.AddDays(1));
    }
}
