using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;
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
        var isHoliday = constraints.IsHoliday(date);

        if (isHolidayWeekendNight || isHoliday)
        {
            if (HolidayQuotaRules.IsHolidayShiftForbidden(user, constraints, date, ShiftLabel.Night))
            {
                return false;
            }

            if (user.ExactHolidayShiftCount.HasValue)
            {
                var totalHoliday = HolidayQuotaRules.CountTotalHolidayShifts(solution, constraints, user.UserId);
                if (totalHoliday >= user.ExactHolidayShiftCount.Value && !DayShiftQuotaEligibility.AllowsHolidaySurplus(fallback, holidayFallback))
                {
                    return false;
                }
            }

            if (isHolidayWeekendNight && holidayExact.HasValue && holidayExact.Value == 0)
            {
                return false;
            }

            if (isHolidayWeekendNight && holidayExact.HasValue)
            {
                var holidayCount = CountHolidayWeekendNights(solution, constraints, user.UserId);
                if (holidayCount < holidayExact.Value)
                {
                    return true;
                }
            }
        }

        if (exact.HasValue && total < exact.Value)
        {
            return true;
        }

        var totalExactQuotas = constraints.UserConstraints
            .Where(u => u.HasExactNightQuota)
            .Sum(u => u.ExactNightShiftCount ?? 0);
        var totalNightDemand = constraints.ShiftRequirements
            .Where(s => s.ShiftLabel == ShiftLabel.Night)
            .Sum(s => s.SpecialtyRequirements.Sum(sr =>
            {
                var days = (constraints.EndDate.Date - constraints.StartDate.Date).Days + 1;
                var holCount = constraints.HolidayDates.Count(h => h.Date >= constraints.StartDate.Date && h.Date <= constraints.EndDate.Date);
                var regCount = days - holCount;
                return sr.ForDay(false).RequiredTotalCount * regCount + sr.ForDay(true).RequiredTotalCount * holCount;
            }));

        if (totalNightDemand > 0 && totalExactQuotas > 0 && totalExactQuotas >= totalNightDemand)
        {
            if (exact.HasValue && total >= exact.Value)
            {
                return false;
            }
            if (!exact.HasValue)
            {
                return false;
            }
        }

        if (!exact.HasValue)
        {
            if (totalExactQuotas > 0 && totalExactQuotas >= totalNightDemand)
            {
                return false;
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
        if (user.ExactNightShiftCount.HasValue)
        {
            return user.NightFallbackParticipation == false
                ? user.ExactNightShiftCount.Value
                : int.MaxValue;
        }

        return DayShiftQuotaEligibility.AllowsSurplus(user.NightFallbackParticipation)
            ? int.MaxValue
            : 0;
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

    public static void ClampInfeasibleQuotas(ShiftConstraints constraints)
    {
        foreach (var user in constraints.UserConstraints)
        {
            if (user.ExactNightShiftCount.HasValue)
            {
                var maxNights = CalculateMaxFeasibleNightShifts(user, constraints);
                if (user.ExactNightShiftCount.Value > maxNights)
                {
                    user.ExactNightShiftCount = maxNights;
                }
            }

            if (user.ExactHolidayWeekendNightShiftCount.HasValue &&
                user.ExactHolidayWeekendNightShiftCount.Value > (user.ExactNightShiftCount ?? 0))
            {
                user.ExactHolidayWeekendNightShiftCount = user.ExactNightShiftCount;
            }
        }
    }

    public static int CalculateMaxFeasibleNightShifts(UserConstraint user, ShiftConstraints constraints)
    {
        if (!ShiftEligibilityResolver.MayEverTakeLabel(user, ShiftLabel.Night))
        {
            return 0;
        }

        var maxPerDay = constraints.HardRules.EnforceMaxShiftsPerDay
            ? Math.Max(1, constraints.GlobalConstraints.MaxShiftsPerDay)
            : 2;

        var start = constraints.StartDate.Date;
        var end = constraints.EndDate.Date;
        var daysCount = (end - start).Days + 1;
        var candidateDates = new List<DateTime>();

        for (var i = 0; i < daysCount; i++)
        {
            var d = start.AddDays(i);

            if (user.UnavailableDates.Any(x => x.Date == d))
            {
                continue;
            }

            if (user.UnavailableShiftSlots.Any(x => x.Date == d && x.ShiftLabel == ShiftLabel.Night))
            {
                continue;
            }

            var reqOnD = user.RequiredShiftSlots.Where(x => x.Date == d).ToList();
            if (reqOnD.Any(x => x.ShiftLabel == ShiftLabel.Night))
            {
                candidateDates.Add(d);
                continue;
            }

            if (reqOnD.Any(x => x.ShiftLabel == ShiftLabel.Evening))
            {
                continue;
            }

            if (reqOnD.Any(x => x.ShiftLabel == ShiftLabel.Morning))
            {
                if (maxPerDay < 2 || !ShiftEligibilityResolver.SupportsMorningNightCombo(user))
                {
                    continue;
                }
            }

            var nextD = d.AddDays(1);
            var reqOnNext = user.RequiredShiftSlots.Where(x => x.Date == nextD).ToList();
            if (reqOnNext.Any(x => x.ShiftLabel == ShiftLabel.Morning))
            {
                continue;
            }

            if (reqOnNext.Any(x => x.ShiftLabel == ShiftLabel.Evening))
            {
                var allowEvening = user.ResolveAllowEveningAfterNightShift(constraints.HardRules.AllowEveningAfterNightShift);
                if (!allowEvening)
                {
                    continue;
                }
            }

            if (reqOnNext.Any(x => x.ShiftLabel == ShiftLabel.Night))
            {
                var allowNight = user.ResolveAllowNightShiftAfterNightShift(constraints.HardRules.AllowNightShiftAfterNightShift);
                if (!allowNight)
                {
                    continue;
                }
            }

            var prevD = d.AddDays(-1);
            var reqOnPrev = user.RequiredShiftSlots.Where(x => x.Date == prevD).ToList();
            if (reqOnPrev.Any(x => x.ShiftLabel == ShiftLabel.Night))
            {
                var allowNight = user.ResolveAllowNightShiftAfterNightShift(constraints.HardRules.AllowNightShiftAfterNightShift);
                if (!allowNight)
                {
                    continue;
                }
            }

            candidateDates.Add(d);
        }

        var allowConsecutiveNight = user.ResolveAllowNightShiftAfterNightShift(constraints.HardRules.AllowNightShiftAfterNightShift);
        if (!allowConsecutiveNight && candidateDates.Count > 1)
        {
            var nonConsecutiveCount = 0;
            DateTime? lastAssigned = null;
            foreach (var cd in candidateDates)
            {
                if (!lastAssigned.HasValue || (cd - lastAssigned.Value).TotalDays > 1)
                {
                    nonConsecutiveCount++;
                    lastAssigned = cd;
                }
            }
            return nonConsecutiveCount;
        }

        return candidateDates.Count;
    }
}
