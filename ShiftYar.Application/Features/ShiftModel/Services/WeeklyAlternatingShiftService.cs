using ShiftYar.Application.Features.UserModel.Services;
using System;
using System.Globalization;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.Features.ShiftModel.Services;

/// <summary>
/// سرویس منطق محاسباتی و اعتبارسنجی تناوب هفتگی شیفت‌های روزانه (صبح و عصر).
/// این قانون منحصراً برای شیفت‌های صبح و عصر اعمال شده و شیفت‌های شب و روزهای آف از آن مستثنی هستند.
/// </summary>
public static class WeeklyAlternatingShiftService
{
    private static readonly PersianCalendar PersianCalendar = new();

    /// <summary>
    /// آیا شیفت مورد نظر از شیفت‌های روزانه تحت پوشش تناوب هفتگی (صبح یا عصر) است؟
    /// </summary>
    public static bool IsDayShift(ShiftLabel label) =>
        label == ShiftLabel.Morning || label == ShiftLabel.Evening;

    /// <summary>
    /// محاسبه شیفت متضاد روزانه (صبح ↔ عصر).
    /// </summary>
    public static ShiftLabel GetOppositeDayShift(ShiftLabel label) =>
        label switch
        {
            ShiftLabel.Morning => ShiftLabel.Evening,
            ShiftLabel.Evening => ShiftLabel.Morning,
            _ => throw new ArgumentException("فقط شیفت‌های صبح و عصر دارای شیفت متضاد روزانه هستند.", nameof(label))
        };

    /// <summary>
    /// عنوان فارسی شیفت جهت نمایش در پیام‌های خطا و لاگ.
    /// </summary>
    public static string ToPersianTitle(ShiftLabel label) =>
        label switch
        {
            ShiftLabel.Morning => "صبح",
            ShiftLabel.Evening => "عصر",
            ShiftLabel.Night => "شب",
            _ => label.ToString()
        };

    /// <summary>
    /// محاسبه شماره هفته تقویمی (۱-پایه) بر اساس هفته استاندارد تقویم شمسی (شنبه تا جمعه) نسبت به تاریخ شروع بازه.
    /// در تقویم ایران شنبه آغاز هفته و جمعه پایان هفته است.
    /// </summary>
    public static int GetWeekNumber(DateTime targetDate, DateTime rangeStartDate)
    {
        var target = targetDate.Date;
        var start = rangeStartDate.Date;

        // شنبه مربوط به هفته تاریخ شروع (یا خود تاریخ اگر شنبه باشد)
        var startOffset = ((int)start.DayOfWeek - (int)DayOfWeek.Saturday + 7) % 7;
        var startWeekSaturday = start.AddDays(-startOffset);

        // شنبه مربوط به هفته تاریخ هدف
        var targetOffset = ((int)target.DayOfWeek - (int)DayOfWeek.Saturday + 7) % 7;
        var targetWeekSaturday = target.AddDays(-targetOffset);

        var weekIndex = (int)Math.Round((targetWeekSaturday - startWeekSaturday).TotalDays / 7.0);
        return weekIndex + 1;
    }

    /// <summary>
    /// محاسبه شماره هفته تقویمی در یک ماه شمسی مشخص.
    /// </summary>
    public static int GetPersianMonthWeekNumber(DateTime targetDate, int? persianYear = null, int? persianMonth = null)
    {
        var y = persianYear ?? PersianCalendar.GetYear(targetDate.Date);
        var m = persianMonth ?? PersianCalendar.GetMonth(targetDate.Date);
        var (monthStart, _, _) = PersianMonthDayCalendar.GetMonthBounds(y, m);
        return GetWeekNumber(targetDate, monthStart);
    }

    /// <summary>
    /// استخراج تاریخ شروع ماه شمسی مربوط به یک تاریخ میلادی.
    /// </summary>
    public static DateTime GetPersianMonthStart(DateTime date)
    {
        var y = PersianCalendar.GetYear(date.Date);
        var m = PersianCalendar.GetMonth(date.Date);
        return PersianCalendar.ToDateTime(y, m, 1, 0, 0, 0, 0).Date;
    }

    /// <summary>
    /// تعیین شیفت روزانه مجاز برای یک شماره هفته معین بر اساس شیفت اولیه هفته اول.
    /// هفته‌های فرد (۱، ۳، ۵): شیفت اولیه.
    /// هفته‌های زوج (۲، ۴): شیفت متضاد.
    /// </summary>
    public static ShiftLabel GetAllowedDayShift(int weekNumber, ShiftLabel firstWeekShift)
    {
        if (!IsDayShift(firstWeekShift))
        {
            throw new ArgumentException("شیفت اولیه هفته اول فقط می‌تواند صبح یا عصر باشد.", nameof(firstWeekShift));
        }

        var isOddWeek = (weekNumber % 2 != 0);
        return isOddWeek ? firstWeekShift : GetOppositeDayShift(firstWeekShift);
    }

    /// <summary>
    /// تعیین شیفت روزانه مجاز در تاریخ مشخص نسبت به تاریخ مبدأ بازه.
    /// </summary>
    public static ShiftLabel GetAllowedDayShiftForDate(DateTime date, DateTime rangeStartDate, ShiftLabel firstWeekShift)
    {
        var weekNumber = GetWeekNumber(date, rangeStartDate);
        return GetAllowedDayShift(weekNumber, firstWeekShift);
    }

    /// <summary>
    /// تعیین شیفت روزانه مجاز در تاریخ مشخص برای یک ماه شمسی.
    /// </summary>
    public static ShiftLabel GetAllowedDayShiftForPersianMonth(DateTime date, int persianYear, int persianMonth, ShiftLabel firstWeekShift)
    {
        var weekNumber = GetPersianMonthWeekNumber(date, persianYear, persianMonth);
        return GetAllowedDayShift(weekNumber, firstWeekShift);
    }

    /// <summary>
    /// اعتبارسنجی انتساب شیفت روزانه بر اساس قانون تناوب هفتگی.
    /// - اگر قانون غیرفعال باشد، انتساب مجاز است.
    /// - اگر شیفت شب باشد، کاملاً مجاز و بدون مداخله است.
    /// - اگر صبح یا عصر باشد، با شیفت مجاز آن هفته تطبیق داده می‌شود.
    /// </summary>
    public static (bool IsValid, string? ErrorMessage, ShiftLabel? AllowedDayShift, int WeekNumber)
        ValidateDailyShift(
            DateTime date,
            ShiftLabel shiftToAssign,
            DateTime rangeStartDate,
            bool isWeeklyAlternatingActive,
            ShiftLabel? firstWeekShift)
    {
        if (!isWeeklyAlternatingActive || !firstWeekShift.HasValue)
        {
            return (true, null, null, 0);
        }

        // شیفت‌های شب و مرخصی از قاعده مستثنی هستند
        if (shiftToAssign == ShiftLabel.Night)
        {
            return (true, null, null, 0);
        }

        if (!IsDayShift(firstWeekShift.Value))
        {
            return (false, "شیفت آغازین در تناوب هفتگی باید منحصراً صبح یا عصر باشد.", null, 0);
        }

        var weekNumber = GetWeekNumber(date, rangeStartDate);
        var allowedDayShift = GetAllowedDayShift(weekNumber, firstWeekShift.Value);

        if (shiftToAssign == allowedDayShift)
        {
            return (true, null, allowedDayShift, weekNumber);
        }

        var error = $"ثبت شیفت {ToPersianTitle(shiftToAssign)} در هفته {weekNumber} مجاز نیست. طبق قانون تناوب هفتگی، شیفت مجاز این هفته تنها {ToPersianTitle(allowedDayShift)} است.";
        return (false, error, allowedDayShift, weekNumber);
    }
}
