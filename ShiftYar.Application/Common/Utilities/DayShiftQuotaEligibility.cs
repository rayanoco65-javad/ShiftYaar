using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using System.Linq;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.Common.Utilities;

/// <summary>
/// تعیین اینکه آیا کاربر می‌تواند در پر کردن خودکار ظرفیت صبح/عصر شرکت کند.
/// fallback=null (پیش‌فرض) = مشارکت در مازاد؛ fallback=false = فقط سهمیه قطعی، بدون مازاد.
/// </summary>
public static class DayShiftQuotaEligibility
{
    public static bool CanAssignInCoverageFill(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        ShiftLabel label,
        DateTime date)
    {
        if (label != ShiftLabel.Morning && label != ShiftLabel.Evening)
        {
            return true;
        }

        if (!ShiftEligibilityResolver.MayEverTakeLabel(user, label))
        {
            return false;
        }

        var total = CountLabel(solution, user.UserId, label);
        var exact = GetExactTotal(user, label);
        var fallback = GetFallback(user, label);
        var holidayExact = GetHolidayExact(user, label);
        var holidayFallback = GetHolidayFallback(user, label);
        var isHoliday = constraints.IsHoliday(date);

        if (exact.HasValue && total < exact.Value)
        {
            return true;
        }

        if (isHoliday && holidayExact.HasValue)
        {
            var holidayCount = CountHolidayLabel(solution, constraints, user.UserId, label);
            if (holidayCount < holidayExact.Value)
            {
                return true;
            }
        }

        return AllowsSurplusForLabel(isHoliday, fallback, holidayFallback);
    }

    public static int GetMaxAllowedTotal(UserConstraint user, ShiftLabel label)
    {
        if (label != ShiftLabel.Morning && label != ShiftLabel.Evening)
        {
            return int.MaxValue;
        }

        var exact = GetExactTotal(user, label);
        var fallback = GetFallback(user, label);
        if (exact.HasValue && fallback == false)
        {
            return exact.Value;
        }

        return int.MaxValue;
    }

    /// <summary>null یا true = مشارکت در مازاد؛ false = بدون مازاد.</summary>
    public static bool AllowsSurplus(bool? fallback) => fallback != false;

    public static bool AllowsHolidaySurplus(bool? generalFallback, bool? holidayFallback)
    {
        if (holidayFallback == false)
        {
            return false;
        }

        if (holidayFallback == true)
        {
            return true;
        }

        return AllowsSurplus(generalFallback);
    }

    private static bool AllowsSurplusForLabel(
        bool isHoliday,
        bool? fallback,
        bool? holidayFallback)
    {
        if (isHoliday)
        {
            return AllowsHolidaySurplus(fallback, holidayFallback);
        }

        return AllowsSurplus(fallback);
    }

    public static int CountLabel(ShiftSolution solution, int userId, ShiftLabel label) =>
        solution.GetUserAllAssignments(userId).Count(a => a.ShiftLabel == label && !a.IsOnCall);

    public static int CountHolidayLabel(
        ShiftSolution solution,
        ShiftConstraints constraints,
        int userId,
        ShiftLabel label) =>
        solution.GetUserAllAssignments(userId)
            .Count(a => a.ShiftLabel == label && !a.IsOnCall && constraints.IsHoliday(a.Date));

    private static int? GetExactTotal(UserConstraint user, ShiftLabel label) =>
        label == ShiftLabel.Morning ? user.ExactMorningShiftCount : user.ExactEveningShiftCount;

    private static bool? GetFallback(UserConstraint user, ShiftLabel label) =>
        label == ShiftLabel.Morning ? user.MorningFallbackParticipation : user.EveningFallbackParticipation;

    private static int? GetHolidayExact(UserConstraint user, ShiftLabel label) =>
        label == ShiftLabel.Morning ? user.ExactHolidayMorningShiftCount : user.ExactHolidayEveningShiftCount;

    private static bool? GetHolidayFallback(UserConstraint user, ShiftLabel label) =>
        label == ShiftLabel.Morning
            ? user.MorningHolidayFallbackParticipation
            : user.EveningHolidayFallbackParticipation;
}
