using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;

/// <summary>
/// تضمین حداقل تعداد شیفت شب (و شب‌های تعطیل/آخرهفته) و توزیع یکنواخت آن‌ها در طول بازه.
/// اگر ظرفیت روز پر باشد، شب را از کاربری که هنوز بالای حداقل خودش است می‌گیرد.
/// </summary>
public static class ExactNightQuotaGuard
{
    public static void Enforce(ShiftSolution solution, ShiftConstraints constraints)
    {
        var nightShift = constraints.ShiftRequirements.FirstOrDefault(s => s.ShiftLabel == ShiftLabel.Night);
        if (nightShift == null)
        {
            return;
        }

        // اول کسری‌های شدیدتر (شب تعطیل، سپس کل)، بعد سهمیه‌های کوچک‌تر تا روی صندلی‌های کمیاب گیر نکنند
        for (var pass = 0; pass < 2; pass++)
        {
            foreach (var user in OrderUsersByDeficit(solution, constraints))
            {
                EnforceForUser(solution, constraints, user, nightShift);
            }
        }

        // پخش شب‌ها برای همه سهمیه‌دارها (حتی اگر حداقلشان پر باشد)
        foreach (var user in constraints.UserConstraints
                     .Where(u => u.HasExactNightQuota || u.ExactHolidayWeekendNightShiftCount.HasValue))
        {
            ImproveNightSpread(solution, constraints, user, nightShift);
        }
    }

    private static IEnumerable<UserConstraint> OrderUsersByDeficit(
        ShiftSolution solution,
        ShiftConstraints constraints)
    {
        return constraints.UserConstraints
            .Where(u => u.HasExactNightQuota || u.ExactHolidayWeekendNightShiftCount.HasValue)
            .Select(u =>
            {
                var nights = GetNights(solution, u.UserId);
                var holiday = nights.Count(a => constraints.IsHolidayWeekendNight(a.Date));
                var holidayDeficit = Math.Max(0, (u.ExactHolidayWeekendNightShiftCount ?? 0) - holiday);
                var totalDeficit = Math.Max(0, (u.ExactNightShiftCount ?? 0) - nights.Count);
                return (User: u, HolidayDeficit: holidayDeficit, TotalDeficit: totalDeficit);
            })
            .Where(x => x.HolidayDeficit > 0 || x.TotalDeficit > 0)
            .OrderByDescending(x => x.HolidayDeficit)
            .ThenByDescending(x => x.TotalDeficit)
            .ThenBy(x => x.User.ExactNightShiftCount ?? 0)
            .Select(x => x.User);
    }

    private static void EnforceForUser(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        ShiftRequirement nightShift)
    {
        var targetTotal = user.ExactNightShiftCount;
        var targetHoliday = user.ExactHolidayWeekendNightShiftCount;

        var nights = GetNights(solution, user.UserId);
        var holidayNights = nights.Where(a => constraints.IsHolidayWeekendNight(a.Date)).ToList();

        if (targetHoliday.HasValue)
        {
            TryFillNights(solution, constraints, user, nightShift, targetHoliday.Value - holidayNights.Count, holidayOnly: true);
            nights = GetNights(solution, user.UserId);
            holidayNights = nights.Where(a => constraints.IsHolidayWeekendNight(a.Date)).ToList();
        }

        if (targetTotal.HasValue)
        {
            var remaining = targetTotal.Value - nights.Count;
            if (remaining > 0)
            {
                var stillNeedHoliday = targetHoliday.HasValue
                    ? Math.Max(0, targetHoliday.Value - holidayNights.Count)
                    : 0;
                if (stillNeedHoliday > 0)
                {
                    TryFillNights(solution, constraints, user, nightShift, stillNeedHoliday, holidayOnly: true);
                    remaining = targetTotal.Value - CountNights(solution, user.UserId);
                }

                if (remaining > 0)
                {
                    TryFillNights(solution, constraints, user, nightShift, remaining, holidayOnly: false);
                }
            }
        }

        // ImproveNightSpread در Enforce سراسری بعد از رفع کسری‌ها اجرا می‌شود
    }

    private static List<SaShiftAssignment> GetNights(ShiftSolution solution, int userId) =>
        solution.GetUserAllAssignments(userId)
            .Where(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall)
            .OrderBy(a => a.Date)
            .ToList();

    private static int CountNights(ShiftSolution solution, int userId) =>
        GetNights(solution, userId).Count;

    private static void TryFillNights(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        ShiftRequirement nightShift,
        int needed,
        bool holidayOnly)
    {
        if (needed <= 0)
        {
            return;
        }

        var minGap = Math.Max(1, user.MinDaysBetweenNightShifts);
        var filled = 0;

        // ۱) صندلی‌های خالی
        filled += FillIntoOpenCapacity(solution, constraints, user, nightShift, needed, holidayOnly, minGap);
        needed -= filled;
        if (needed <= 0)
        {
            return;
        }

        // ۲) گرفتن شب از اهداکننده‌ای که بالای حداقل خودش است
        ClaimNightsFromDonors(solution, constraints, user, nightShift, needed, holidayOnly, minGap);
    }

    private static int FillIntoOpenCapacity(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        ShiftRequirement nightShift,
        int needed,
        bool holidayOnly,
        int minGap)
    {
        var existingNightDates = GetNights(solution, user.UserId).Select(a => a.Date.Date).ToList();
        var candidates = AllCandidateDates(constraints, holidayOnly)
            .Where(d => IsFeasibleNightDate(solution, constraints, user, nightShift, d, holidayOnly, ignoreUserNightOnDate: false))
            .ToList();

        var picks = PickSpreadDates(candidates, existingNightDates, needed, minGap);
        var added = 0;
        foreach (var date in picks)
        {
            if (added >= needed)
            {
                break;
            }

            if (!IsFeasibleNightDate(solution, constraints, user, nightShift, date, holidayOnly, ignoreUserNightOnDate: false))
            {
                continue;
            }

            if (ViolatesNightSpacing(solution, user, date, minGap))
            {
                continue;
            }

            ClearConflictingDayShifts(solution, constraints, user, date);
            solution.AddAssignment(user.UserId, nightShift.ShiftId, date, ShiftLabel.Night, isOnCall: false);
            added++;
        }

        return added;
    }

    private static void ClaimNightsFromDonors(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        ShiftRequirement nightShift,
        int needed,
        bool holidayOnly,
        int minGap)
    {
        var claimed = 0;
        var candidateDates = PickSpreadDates(
            AllCandidateDates(constraints, holidayOnly).ToList(),
            GetNights(solution, user.UserId).Select(a => a.Date.Date).ToList(),
            needed * 4,
            minGap);

        // اگر spread کافی نبود، همه تاریخ‌های مجاز را هم امتحان کن
        var dates = candidateDates
            .Concat(AllCandidateDates(constraints, holidayOnly))
            .Distinct()
            .ToList();

        foreach (var date in dates)
        {
            if (claimed >= needed)
            {
                break;
            }

            if (!IsPersonallyFeasibleNightDate(solution, constraints, user, nightShift, date, holidayOnly))
            {
                continue;
            }

            if (ViolatesNightSpacing(solution, user, date, minGap))
            {
                continue;
            }

            if (HasSpecialtyCapacity(solution, constraints, nightShift, date, user.SpecialtyId))
            {
                ClearConflictingDayShifts(solution, constraints, user, date);
                solution.AddAssignment(user.UserId, nightShift.ShiftId, date, ShiftLabel.Night, isOnCall: false);
                claimed++;
                continue;
            }

            var donorAssignment = FindBestDonorAssignment(solution, constraints, nightShift, date, user.UserId, holidayOnly);
            if (donorAssignment == null)
            {
                continue;
            }

            if (!CanAcceptNightAfterClearing(solution, constraints, user, nightShift, date))
            {
                continue;
            }

            ClearConflictingDayShifts(solution, constraints, user, date);
            solution.RemoveAssignment(donorAssignment.UserId, donorAssignment.ShiftId, donorAssignment.Date);
            solution.AddAssignment(user.UserId, nightShift.ShiftId, date, ShiftLabel.Night, isOnCall: false);
            claimed++;
        }
    }

    private static SaShiftAssignment? FindBestDonorAssignment(
        ShiftSolution solution,
        ShiftConstraints constraints,
        ShiftRequirement nightShift,
        DateTime date,
        int receiverUserId,
        bool holidayClaim)
    {
        var occupants = solution.GetShiftAssignments(nightShift.ShiftId, date)
            .Where(a => !a.IsOnCall && a.UserId != receiverUserId)
            .ToList();

        return occupants
            .Select(a =>
            {
                var donor = constraints.UserConstraints.FirstOrDefault(u => u.UserId == a.UserId);
                return (Assignment: a, Donor: donor);
            })
            .Where(x => x.Donor != null && CanDonateNight(solution, constraints, x.Donor!, x.Assignment))
            .OrderBy(x => DonorPriority(solution, constraints, x.Donor!, x.Assignment, holidayClaim))
            .Select(x => x.Assignment)
            .FirstOrDefault();
    }

    private static int DonorPriority(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint donor,
        SaShiftAssignment assignment,
        bool holidayClaim)
    {
        var nights = GetNights(solution, donor.UserId);
        var holiday = nights.Count(a => constraints.IsHolidayWeekendNight(a.Date));
        var totalSurplus = nights.Count - (donor.ExactNightShiftCount ?? 0);
        var holidaySurplus = holiday - (donor.ExactHolidayWeekendNightShiftCount ?? 0);

        // بدون سهمیه = بهترین اهداکننده
        if (!donor.HasExactNightQuota && !donor.ExactHolidayWeekendNightShiftCount.HasValue)
        {
            return -10000 - nights.Count;
        }

        if (holidayClaim)
        {
            return -(holidaySurplus * 100 + totalSurplus * 10 + nights.Count);
        }

        return -(totalSurplus * 100 + holidaySurplus * 10 + nights.Count);
    }

    /// <summary>
    /// آیا اهداکننده می‌تواند این شب را از دست بدهد بدون افت زیر حداقل؟
    /// </summary>
    public static bool CanDonateNight(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        SaShiftAssignment assignment)
    {
        if (assignment.ShiftLabel != ShiftLabel.Night || assignment.IsOnCall)
        {
            return true;
        }

        if (IsProtected(constraints, user.UserId, assignment))
        {
            return false;
        }

        var nights = GetNights(solution, user.UserId);
        var remainingTotal = nights.Count - 1;
        if (user.ExactNightShiftCount.HasValue && remainingTotal < user.ExactNightShiftCount.Value)
        {
            return false;
        }

        if (user.ExactHolidayWeekendNightShiftCount.HasValue && constraints.IsHolidayWeekendNight(assignment.Date))
        {
            var holidayNights = nights.Count(a => constraints.IsHolidayWeekendNight(a.Date));
            if (holidayNights - 1 < user.ExactHolidayWeekendNightShiftCount.Value)
            {
                return false;
            }
        }

        return true;
    }

    private static void ClearConflictingDayShifts(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        DateTime date)
    {
        var dayAssignments = solution.GetUserAssignments(user.UserId, date)
            .Where(a => !a.IsOnCall)
            .Where(a => a.ShiftLabel == ShiftLabel.Morning || a.ShiftLabel == ShiftLabel.Evening)
            .Where(a => !IsProtected(constraints, user.UserId, a))
            .ToList();

        foreach (var a in dayAssignments)
        {
            solution.RemoveAssignment(a.UserId, a.ShiftId, a.Date);
        }
    }

    private static bool CanAcceptNightAfterClearing(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        ShiftRequirement nightShift,
        DateTime date)
    {
        var protectedConflict = solution.GetUserAssignments(user.UserId, date)
            .Where(a => !a.IsOnCall)
            .Where(a => a.ShiftLabel == ShiftLabel.Morning || a.ShiftLabel == ShiftLabel.Evening)
            .Any(a => IsProtected(constraints, user.UserId, a));
        if (protectedConflict)
        {
            return false;
        }

        if (AdjacentShiftRestRules.WouldConflict(
                solution.GetUserAllAssignments(user.UserId)
                    .Where(a => !(a.Date.Date == date.Date &&
                                  (a.ShiftLabel == ShiftLabel.Morning || a.ShiftLabel == ShiftLabel.Evening))),
                date,
                ShiftLabel.Night))
        {
            return false;
        }

        return ShiftEligibilityResolver.IsLabelAllowed(user.AllowedShiftLabels, ShiftLabel.Night)
               && !user.UnavailableDates.Any(d => d.Date == date.Date)
               && !user.UnavailableShiftSlots.Any(s => s.Date.Date == date.Date && s.ShiftLabel == ShiftLabel.Night)
               && !solution.HasAssignment(user.UserId, nightShift.ShiftId, date);
    }

    private static IEnumerable<DateTime> AllCandidateDates(ShiftConstraints constraints, bool holidayOnly) =>
        Enumerable.Range(0, (constraints.EndDate.Date - constraints.StartDate.Date).Days + 1)
            .Select(offset => constraints.StartDate.Date.AddDays(offset))
            .Where(d => !holidayOnly || constraints.IsHolidayWeekendNight(d));

    /// <summary>
    /// جابه‌جایی شب‌های موجود به تاریخ‌های خلوت‌تر بدون خالی گذاشتن ظرفیت (در صورت نیاز swap).
    /// </summary>
    private static void ImproveNightSpread(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        ShiftRequirement nightShift)
    {
        var minGap = Math.Max(1, user.MinDaysBetweenNightShifts);

        for (var iter = 0; iter < 24; iter++)
        {
            var nights = GetNights(solution, user.UserId)
                .Where(a => !IsProtected(constraints, user.UserId, a))
                .ToList();
            if (nights.Count < 2)
            {
                return;
            }

            var currentDates = GetNights(solution, user.UserId).Select(a => a.Date.Date).ToList();
            var currentPenalty = CalculateSpreadPenalty(currentDates, constraints.StartDate, constraints.EndDate);

            var allDates = Enumerable.Range(0, (constraints.EndDate.Date - constraints.StartDate.Date).Days + 1)
                .Select(offset => constraints.StartDate.Date.AddDays(offset))
                .ToList();

            SaShiftAssignment? bestFrom = null;
            DateTime? bestTo = null;
            int? swapUserId = null;
            var bestPenalty = currentPenalty;

            foreach (var night in nights)
            {
                foreach (var target in allDates)
                {
                    if (target == night.Date.Date || currentDates.Contains(target))
                    {
                        continue;
                    }

                    if (user.ExactHolidayWeekendNightShiftCount.HasValue)
                    {
                        var fromHol = constraints.IsHolidayWeekendNight(night.Date);
                        var toHol = constraints.IsHolidayWeekendNight(target);
                        if (fromHol != toHol)
                        {
                            var holidayCount = currentDates.Count(d => constraints.IsHolidayWeekendNight(d));
                            var projected = holidayCount - (fromHol ? 1 : 0) + (toHol ? 1 : 0);
                            if (projected < user.ExactHolidayWeekendNightShiftCount.Value)
                            {
                                continue;
                            }
                        }
                    }

                    var projectedDates = currentDates.Where(d => d != night.Date.Date).Append(target).ToList();
                    if (projectedDates.Where(d => d != target).Any(d => Math.Abs((d - target).Days) <= minGap))
                    {
                        continue;
                    }

                    var projectedPenalty = CalculateSpreadPenalty(projectedDates, constraints.StartDate, constraints.EndDate);
                    if (projectedPenalty >= bestPenalty - 0.01)
                    {
                        continue;
                    }

                    if (IsFeasibleNightDate(solution, constraints, user, nightShift, target, holidayOnly: false, ignoreUserNightOnDate: false)
                        && HasSpecialtyCapacity(solution, constraints, nightShift, target, user.SpecialtyId))
                    {
                        bestPenalty = projectedPenalty;
                        bestFrom = night;
                        bestTo = target;
                        swapUserId = null;
                        continue;
                    }

                    var occupant = solution.GetShiftAssignments(nightShift.ShiftId, target)
                        .FirstOrDefault(a => !a.IsOnCall && a.UserId != user.UserId);
                    if (occupant == null)
                    {
                        continue;
                    }

                    var other = constraints.UserConstraints.FirstOrDefault(u => u.UserId == occupant.UserId);
                    if (other == null || !CanDonateNight(solution, constraints, other, occupant))
                    {
                        continue;
                    }

                    if (!CanSwapNight(solution, constraints, user, other, night.Date.Date, target, nightShift, minGap))
                    {
                        continue;
                    }

                    bestPenalty = projectedPenalty;
                    bestFrom = night;
                    bestTo = target;
                    swapUserId = other.UserId;
                }
            }

            if (bestFrom == null || bestTo == null)
            {
                return;
            }

            var fromDate = bestFrom.Date.Date;
            var toDate = bestTo.Value.Date;

            if (swapUserId.HasValue)
            {
                solution.RemoveAssignment(user.UserId, nightShift.ShiftId, fromDate);
                solution.RemoveAssignment(swapUserId.Value, nightShift.ShiftId, toDate);
                solution.AddAssignment(user.UserId, nightShift.ShiftId, toDate, ShiftLabel.Night, false);
                solution.AddAssignment(swapUserId.Value, nightShift.ShiftId, fromDate, ShiftLabel.Night, false);
            }
            else
            {
                solution.RemoveAssignment(user.UserId, nightShift.ShiftId, fromDate);
                solution.AddAssignment(user.UserId, nightShift.ShiftId, toDate, ShiftLabel.Night, false);
            }
        }
    }

    private static bool CanSwapNight(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        UserConstraint other,
        DateTime userFrom,
        DateTime userTo,
        ShiftRequirement nightShift,
        int minGap)
    {
        if (other.UnavailableDates.Any(d => d.Date == userFrom) ||
            user.UnavailableDates.Any(d => d.Date == userTo))
        {
            return false;
        }

        if (AdjacentShiftRestRules.WouldConflict(
                solution.GetUserAllAssignments(user.UserId).Where(a => !(a.ShiftLabel == ShiftLabel.Night && a.Date.Date == userFrom)),
                userTo, ShiftLabel.Night))
        {
            return false;
        }

        if (AdjacentShiftRestRules.WouldConflict(
                solution.GetUserAllAssignments(other.UserId).Where(a => !(a.ShiftLabel == ShiftLabel.Night && a.Date.Date == userTo)),
                userFrom, ShiftLabel.Night))
        {
            return false;
        }

        var userOtherNights = GetNights(solution, user.UserId)
            .Where(a => a.Date.Date != userFrom)
            .Select(a => a.Date.Date);
        if (userOtherNights.Any(d => Math.Abs((d - userTo).Days) <= minGap))
        {
            return false;
        }

        var otherLabelsAtFrom = solution.GetUserAssignments(other.UserId, userFrom)
            .Where(a => !(a.ShiftLabel == ShiftLabel.Night && a.Date.Date == userFrom))
            .Select(a => a.ShiftLabel);
        if (!DailyAssignmentRules.CanAddShift(otherLabelsAtFrom, ShiftLabel.Night, 2))
        {
            return false;
        }

        var userLabelsAtTo = solution.GetUserAssignments(user.UserId, userTo)
            .Where(a => !(a.ShiftId == nightShift.ShiftId && a.Date.Date == userTo))
            .Select(a => a.ShiftLabel);
        if (!DailyAssignmentRules.CanAddShift(userLabelsAtTo, ShiftLabel.Night, 2))
        {
            return false;
        }

        return ShiftEligibilityResolver.IsLabelAllowed(other.AllowedShiftLabels, ShiftLabel.Night)
               && ShiftEligibilityResolver.IsLabelAllowed(user.AllowedShiftLabels, ShiftLabel.Night);
    }

    public static List<DateTime> PickSpreadDates(
        IReadOnlyList<DateTime> candidates,
        IReadOnlyList<DateTime> alreadyOccupied,
        int needed,
        int minGapDays)
    {
        var result = new List<DateTime>();
        if (needed <= 0 || candidates.Count == 0)
        {
            return result;
        }

        var sorted = candidates.Select(d => d.Date).Distinct().OrderBy(d => d).ToList();
        var occupied = alreadyOccupied.Select(d => d.Date).ToHashSet();

        for (var i = 0; i < needed; i++)
        {
            var idealIdx = needed == 1
                ? (sorted.Count - 1) / 2.0
                : i * (sorted.Count - 1.0) / Math.Max(1, needed - 1);

            var pick = sorted
                .Select((d, idx) => (d, idx))
                .Where(x => !occupied.Contains(x.d) && !result.Contains(x.d))
                .Where(x => occupied.Concat(result).All(o => Math.Abs((x.d - o).Days) > minGapDays))
                .OrderBy(x => Math.Abs(x.idx - idealIdx))
                .ThenByDescending(x =>
                {
                    var others = occupied.Concat(result).ToList();
                    return others.Count == 0 ? 1000 : others.Min(o => Math.Abs((x.d - o).Days));
                })
                .Select(x => (DateTime?)x.d)
                .FirstOrDefault();

            if (pick == null)
            {
                pick = sorted
                    .Select((d, idx) => (d, idx))
                    .Where(x => !occupied.Contains(x.d) && !result.Contains(x.d))
                    .OrderBy(x => Math.Abs(x.idx - idealIdx))
                    .Select(x => (DateTime?)x.d)
                    .FirstOrDefault();
            }

            if (pick == null)
            {
                break;
            }

            result.Add(pick.Value);
        }

        return result;
    }

    private static bool IsFeasibleNightDate(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        ShiftRequirement nightShift,
        DateTime date,
        bool holidayOnly,
        bool ignoreUserNightOnDate)
    {
        if (!IsPersonallyFeasibleNightDate(solution, constraints, user, nightShift, date, holidayOnly))
        {
            return false;
        }

        if (!ignoreUserNightOnDate && solution.HasAssignment(user.UserId, nightShift.ShiftId, date))
        {
            return false;
        }

        var existingLabels = solution.GetUserAssignments(user.UserId, date)
            .Where(a => !(ignoreUserNightOnDate && a.ShiftLabel == ShiftLabel.Night))
            .Select(a => a.ShiftLabel);
        if (!DailyAssignmentRules.CanAddShift(existingLabels, ShiftLabel.Night, maxShiftsPerDay: 2))
        {
            return false;
        }

        if (AdjacentShiftRestRules.WouldConflict(solution.GetUserAllAssignments(user.UserId), date, ShiftLabel.Night))
        {
            return false;
        }

        return HasSpecialtyCapacity(solution, constraints, nightShift, date, user.SpecialtyId);
    }

    private static bool IsPersonallyFeasibleNightDate(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        ShiftRequirement nightShift,
        DateTime date,
        bool holidayOnly)
    {
        if (holidayOnly && !constraints.IsHolidayWeekendNight(date))
        {
            return false;
        }

        // رزرو ظرفیت باقی‌مانده برای تکمیل حداقل شب تعطیل
        if (!holidayOnly && user.ExactHolidayWeekendNightShiftCount.HasValue)
        {
            var holidayCount = GetNights(solution, user.UserId).Count(a => constraints.IsHolidayWeekendNight(a.Date));
            var totalCount = CountNights(solution, user.UserId);
            var remainingTotal = (user.ExactNightShiftCount ?? int.MaxValue) - totalCount;
            var remainingHoliday = user.ExactHolidayWeekendNightShiftCount.Value - holidayCount;
            if (!constraints.IsHolidayWeekendNight(date) && remainingHoliday > 0 && remainingTotal <= remainingHoliday)
            {
                return false;
            }
        }

        if (solution.HasAssignment(user.UserId, nightShift.ShiftId, date))
        {
            return false;
        }

        if (user.UnavailableDates.Any(d => d.Date == date.Date))
        {
            return false;
        }

        if (user.UnavailableShiftSlots.Any(s => s.Date.Date == date.Date && s.ShiftLabel == ShiftLabel.Night))
        {
            return false;
        }

        return ShiftEligibilityResolver.IsLabelAllowed(user.AllowedShiftLabels, ShiftLabel.Night);
    }

    private static bool HasSpecialtyCapacity(
        ShiftSolution solution,
        ShiftConstraints constraints,
        ShiftRequirement nightShift,
        DateTime date,
        int specialtyId)
    {
        var specialtyReq = nightShift.SpecialtyRequirements.FirstOrDefault(r => r.SpecialtyId == specialtyId)
                           ?? nightShift.SpecialtyRequirements.FirstOrDefault();
        if (specialtyReq == null)
        {
            return true;
        }

        var day = specialtyReq.ForDay(constraints.IsHoliday(date));
        var current = solution.GetShiftAssignments(nightShift.ShiftId, date)
            .Count(a => !a.IsOnCall && GetSpecialty(constraints, a.UserId) == specialtyReq.SpecialtyId);
        return current < Math.Max(day.RequiredTotalCount, 1);
    }

    private static int GetSpecialty(ShiftConstraints constraints, int userId) =>
        constraints.UserConstraints.FirstOrDefault(u => u.UserId == userId)?.SpecialtyId ?? 0;

    private static bool ViolatesNightSpacing(
        ShiftSolution solution,
        UserConstraint user,
        DateTime candidateDate,
        int minGap)
    {
        foreach (var nightDate in GetNights(solution, user.UserId).Select(a => a.Date.Date))
        {
            if (Math.Abs((candidateDate.Date - nightDate).Days) <= minGap)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsProtected(ShiftConstraints constraints, int userId, SaShiftAssignment assignment)
    {
        var user = constraints.UserConstraints.FirstOrDefault(u => u.UserId == userId);
        if (user == null)
        {
            return false;
        }

        return user.RequiredShiftSlots.Any(s =>
            s.Date.Date == assignment.Date.Date && s.ShiftLabel == assignment.ShiftLabel);
    }

    public static double CalculateSpreadPenalty(
        IReadOnlyList<DateTime> nightDates,
        DateTime rangeStart,
        DateTime rangeEnd)
    {
        if (nightDates.Count < 2)
        {
            return 0;
        }

        var ordered = nightDates.Select(d => d.Date).Distinct().OrderBy(d => d).ToList();
        var spanDays = Math.Max(1, (rangeEnd.Date - rangeStart.Date).Days);
        var idealGap = spanDays / (double)(ordered.Count - 1);
        double penalty = 0;
        for (var i = 1; i < ordered.Count; i++)
        {
            var gap = (ordered[i] - ordered[i - 1]).Days;
            var deficit = idealGap - gap;
            if (deficit > 0)
            {
                penalty += deficit * deficit;
            }
        }

        var mid = rangeStart.Date.AddDays(spanDays / 2.0);
        var inFirstHalf = ordered.Count(d => d < mid);
        var imbalance = Math.Abs(inFirstHalf - (ordered.Count - inFirstHalf));
        penalty += imbalance * 8;

        return penalty;
    }
}
