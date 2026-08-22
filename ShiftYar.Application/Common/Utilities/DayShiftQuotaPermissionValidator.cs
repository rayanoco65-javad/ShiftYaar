using ShiftYar.Domain.Entities.UserModel;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.Common.Utilities;

/// <summary>
/// اعتبارسنجی سازگاری سهمیه صبح/عصر با مجوز نوع شیفت کاربر.
/// </summary>
public static class DayShiftQuotaPermissionValidator
{
    public static string? Validate(
        User user,
        UserShiftPermission permissions,
        int? exactMorningShiftCount,
        bool? morningFallbackParticipation,
        int? exactHolidayMorningShiftCount,
        bool? morningHolidayFallbackParticipation,
        int? exactEveningShiftCount,
        bool? eveningFallbackParticipation,
        int? exactHolidayEveningShiftCount,
        bool? eveningHolidayFallbackParticipation)
    {
        var displayName = string.IsNullOrWhiteSpace(user.FullName)
            ? $"کاربر {user.Id}"
            : user.FullName!;

        var morningConfigured = exactMorningShiftCount.HasValue
                                || exactHolidayMorningShiftCount.HasValue
                                || morningFallbackParticipation == true
                                || morningHolidayFallbackParticipation == true;
        if (morningConfigured
            && !GetStandaloneLabels(permissions).Contains(ShiftLabel.Morning))
        {
            return
                $"{displayName} مجوز شیفت صبح ندارد؛ امکان ثبت سهمیه یا ترجیح توزیع صبح وجود ندارد. " +
                "ابتدا مجوز نوع شیفت کاربر را اصلاح کنید.";
        }

        if (morningConfigured
            && user.ShiftType == ShiftTypes.FixedShift
            && user.ShiftSubType == ShiftSubTypes.FixedEvening)
        {
            return $"{displayName} نوع شیفت ثابت عصر دارد؛ امکان ثبت سهمیه صبح وجود ندارد.";
        }

        var eveningConfigured = exactEveningShiftCount.HasValue
                                || exactHolidayEveningShiftCount.HasValue
                                || eveningFallbackParticipation == true
                                || eveningHolidayFallbackParticipation == true;
        if (eveningConfigured
            && !GetStandaloneLabels(permissions).Contains(ShiftLabel.Evening))
        {
            return
                $"{displayName} مجوز شیفت عصر ندارد؛ امکان ثبت سهمیه یا ترجیح توزیع عصر وجود ندارد. " +
                "ابتدا مجوز نوع شیفت کاربر را اصلاح کنید.";
        }

        if (eveningConfigured
            && user.ShiftType == ShiftTypes.FixedShift
            && user.ShiftSubType == ShiftSubTypes.FixedMorning)
        {
            return $"{displayName} نوع شیفت ثابت صبح دارد؛ امکان ثبت سهمیه عصر وجود ندارد.";
        }

        return null;
    }

    private static IReadOnlyList<ShiftLabel> GetStandaloneLabels(UserShiftPermission permissions) =>
        ShiftEligibilityResolver.GetStandaloneLabels(permissions);

    public static bool HasAnyConfiguredValue(
        int? exactMorningShiftCount,
        bool? morningFallbackParticipation,
        int? exactHolidayMorningShiftCount,
        bool? morningHolidayFallbackParticipation,
        int? exactEveningShiftCount,
        bool? eveningFallbackParticipation,
        int? exactHolidayEveningShiftCount,
        bool? eveningHolidayFallbackParticipation) =>
        exactMorningShiftCount.HasValue
        || exactHolidayMorningShiftCount.HasValue
        || morningFallbackParticipation != null
        || morningHolidayFallbackParticipation != null
        || exactEveningShiftCount.HasValue
        || exactHolidayEveningShiftCount.HasValue
        || eveningFallbackParticipation != null
        || eveningHolidayFallbackParticipation != null;
}
