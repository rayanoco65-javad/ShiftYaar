namespace ShiftYar.Application.Common.Utilities;

/// <summary>
/// قوانین و پیام‌های مربوط به سقف شیفت روزانه دپارتمان.
/// </summary>
public static class MaxShiftsPerDayRules
{
    public const int MinAllowed = 1;
    public const int MaxAllowed = 2;

    public static bool IsValidSetting(int? maxShiftsPerDay) =>
        !maxShiftsPerDay.HasValue || maxShiftsPerDay is MinAllowed or MaxAllowed;

    public static string InvalidSettingMessage =>
        "حداکثر شیفت در روز فقط می‌تواند ۱ یا ۲ باشد.";

    public static string SecondShiftBlockedMessage =>
        "تنظیمات دپارتمان «اعمال حداکثر شیفت در روز» فعال است و سقف روی ۱ قرار دارد؛ " +
        "هر کاربر در یک روز تقویمی حداکثر یک شیفت می‌گیرد. " +
        "برای تخصیص دومین شیفت در همان روز (مثلاً صبح+عصر یا صبح+شب)، ابتدا «حداکثر شیفت در روز» را روی ۲ تنظیم کنید.";

    public static bool WouldExceedDailyLimit(int existingCountOnDay, int maxShiftsPerDay, bool enforce) =>
        enforce && existingCountOnDay >= Math.Clamp(maxShiftsPerDay, MinAllowed, MaxAllowed);
}
