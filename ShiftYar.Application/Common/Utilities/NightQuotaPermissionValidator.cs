using ShiftYar.Domain.Entities.UserModel;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.Common.Utilities;

public static class NightQuotaPermissionValidator
{
    public static string? Validate(
        User user,
        UserShiftPermission permissions,
        int? exactNightShiftCount,
        bool? nightFallbackParticipation,
        int? exactHolidayWeekendNightShiftCount,
        bool? holidayWeekendNightFallbackParticipation)
    {
        var displayName = string.IsNullOrWhiteSpace(user.FullName)
            ? $"کاربر {user.Id}"
            : user.FullName!;

        var nightConfigured = exactNightShiftCount.HasValue
                              || exactHolidayWeekendNightShiftCount.HasValue
                              || nightFallbackParticipation == true
                              || holidayWeekendNightFallbackParticipation == true;

        // تخصیص سهمیه شب ماهانه توسط سرپرست/مدیر می‌تواند برای هر پرسنلی در دپارتمان تنظیم شود
        // (به‌عنوان مثال پرسنل فیکس صبح/عصر که با ثبت درخواست حضور قصد انجام شیفت شب دارند).
        // بنابراین بررسی اولیه مجوز نوع شیفت کاربر در این بخش مانع تخصیص سهمیه ماهانه نمی‌شود.
        return null;
    }

    public static bool HasAnyConfiguredValue(
        int? exactNightShiftCount,
        bool? nightFallbackParticipation,
        int? exactHolidayWeekendNightShiftCount,
        bool? holidayWeekendNightFallbackParticipation,
        int? exactHolidayShiftCount = null) =>
        exactNightShiftCount.HasValue
        || exactHolidayWeekendNightShiftCount.HasValue
        || nightFallbackParticipation != null
        || holidayWeekendNightFallbackParticipation != null
        || exactHolidayShiftCount.HasValue;

    private static bool MayEverTakeNight(UserShiftPermission permissions) =>
        ShiftEligibilityResolver.GetStandaloneLabels(permissions).Contains(ShiftLabel.Night)
        || permissions.HasFlag(UserShiftPermission.MorningNightSameDay);
}
