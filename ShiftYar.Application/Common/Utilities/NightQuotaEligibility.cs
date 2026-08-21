using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.Common.Utilities;

/// <summary>
/// تعیین مشارکت کاربر در توزیع مازاد شیفت شب.
/// fallback=null (پیش‌فرض) = مشارکت در مازاد؛ fallback=false = فقط سهمیه قطعی.
/// </summary>
public static class NightQuotaEligibility
{
    public static bool CanAssignInCoverageFill(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        DateTime date)
    {
        if (!ShiftEligibilityResolver.MayEverTakeLabel(user, ShiftLabel.Night))
        {
            return false;
        }

        var total = CountNights(solution, user.UserId);
        var exact = user.ExactNightShiftCount;
        var fallback = user.NightFallbackParticipation;
        var holidayExact = user.ExactHolidayWeekendNightShiftCount;
        var holidayFallback = user.HolidayWeekendNightFallbackParticipation;
        var isHolidayWeekendNight = constraints.IsHolidayWeekendNight(date);

        if (exact.HasValue && total < exact.Value)
        {
            return true;
        }

        if (isHolidayWeekendNight && holidayExact.HasValue)
        {
            var holidayCount = CountHolidayWeekendNights(solution, constraints, user.UserId);
            if (holidayCount < holidayExact.Value)
            {
                return true;
            }
        }

        if (isHolidayWeekendNight)
        {
            return DayShiftQuotaEligibility.AllowsHolidaySurplus(fallback, holidayFallback);
        }

        return DayShiftQuotaEligibility.AllowsSurplus(fallback);
    }

    public static int GetMaxAllowedTotal(UserConstraint user)
    {
        if (user.ExactNightShiftCount.HasValue && user.NightFallbackParticipation == false)
        {
            return user.ExactNightShiftCount.Value;
        }

        return int.MaxValue;
    }

    public static int CountNights(ShiftSolution solution, int userId) =>
        solution.GetUserAllAssignments(userId).Count(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall);

    public static int CountHolidayWeekendNights(
        ShiftSolution solution,
        ShiftConstraints constraints,
        int userId) =>
        solution.GetUserAllAssignments(userId)
            .Count(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall &&
                        constraints.IsHolidayWeekendNight(a.Date));
}
