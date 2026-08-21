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

        if (nightConfigured && !MayEverTakeNight(permissions))
        {
            return
                $"{displayName} مجوز شیفت شب ندارد؛ امکان ثبت سهمیه یا ترجیح توزیع شب وجود ندارد. " +
                "ابتدا مجوز نوع شیفت کاربر را اصلاح کنید.";
        }

        return null;
    }

    public static bool HasAnyConfiguredValue(
        int? exactNightShiftCount,
        bool? nightFallbackParticipation,
        int? exactHolidayWeekendNightShiftCount,
        bool? holidayWeekendNightFallbackParticipation) =>
        exactNightShiftCount.HasValue
        || exactHolidayWeekendNightShiftCount.HasValue
        || nightFallbackParticipation != null
        || holidayWeekendNightFallbackParticipation != null;

    private static bool MayEverTakeNight(UserShiftPermission permissions) =>
        ShiftEligibilityResolver.GetStandaloneLabels(permissions).Contains(ShiftLabel.Night)
        || permissions.HasFlag(UserShiftPermission.MorningNightSameDay);
}
