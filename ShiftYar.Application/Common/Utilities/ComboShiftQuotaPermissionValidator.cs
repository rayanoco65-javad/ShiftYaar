using ShiftYar.Domain.Entities.UserModel;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.Common.Utilities;

public static class ComboShiftQuotaPermissionValidator
{
    public static string? Validate(
        User user,
        UserShiftPermission permissions,
        int? morningEveningShiftCount,
        bool? morningEveningFallbackParticipation,
        int? morningEveningHolidayCount,
        bool? morningEveningHolidayFallback,
        int? morningNightShiftCount,
        bool? morningNightFallbackParticipation,
        int? morningNightHolidayCount,
        bool? morningNightHolidayFallback)
    {
        var meConfigured = IsMorningEveningConfigured(
            morningEveningShiftCount,
            morningEveningFallbackParticipation,
            morningEveningHolidayCount,
            morningEveningHolidayFallback);

        var mnConfigured = IsMorningNightConfigured(
            morningNightShiftCount,
            morningNightFallbackParticipation,
            morningNightHolidayCount,
            morningNightHolidayFallback);

        if (meConfigured)
        {
            var error = ShiftQuotaTypeValidator.ValidateMorningEveningQuotaAllowed(user, permissions);
            if (error != null)
            {
                return error;
            }
        }

        if (mnConfigured)
        {
            var error = ShiftQuotaTypeValidator.ValidateMorningNightQuotaAllowed(user, permissions);
            if (error != null)
            {
                return error;
            }
        }

        return null;
    }

    public static bool HasAnyConfiguredValue(
        int? morningEveningShiftCount,
        bool? morningEveningFallbackParticipation,
        int? morningEveningHolidayCount,
        bool? morningEveningHolidayFallback,
        int? morningNightShiftCount,
        bool? morningNightFallbackParticipation,
        int? morningNightHolidayCount,
        bool? morningNightHolidayFallback) =>
        IsMorningEveningConfigured(
            morningEveningShiftCount,
            morningEveningFallbackParticipation,
            morningEveningHolidayCount,
            morningEveningHolidayFallback)
        || IsMorningNightConfigured(
            morningNightShiftCount,
            morningNightFallbackParticipation,
            morningNightHolidayCount,
            morningNightHolidayFallback);

    private static bool IsMorningEveningConfigured(
        int? count,
        bool? fallback,
        int? holidayCount,
        bool? holidayFallback) =>
        count.HasValue || holidayCount.HasValue || fallback != null || holidayFallback != null;

    private static bool IsMorningNightConfigured(
        int? count,
        bool? fallback,
        int? holidayCount,
        bool? holidayFallback) =>
        count.HasValue || holidayCount.HasValue || fallback != null || holidayFallback != null;
}
