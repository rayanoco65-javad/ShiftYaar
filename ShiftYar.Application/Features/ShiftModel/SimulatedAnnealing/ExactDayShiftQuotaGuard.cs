using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;

/// <summary>
/// تضمین سهمیه دقیق صبح/عصر و حذف مازاد برای کاربران بدون fallback.
/// </summary>
public static class ExactDayShiftQuotaGuard
{
    public static void EnforceAll(ShiftSolution solution, ShiftConstraints constraints)
    {
        Enforce(solution, constraints, ShiftLabel.Morning);
        Enforce(solution, constraints, ShiftLabel.Evening);
    }

    public static void Enforce(ShiftSolution solution, ShiftConstraints constraints, ShiftLabel label)
    {
        if (label != ShiftLabel.Morning && label != ShiftLabel.Evening)
        {
            return;
        }

        var shiftReq = constraints.ShiftRequirements.FirstOrDefault(s => s.ShiftLabel == label);
        if (shiftReq == null)
        {
            return;
        }

        for (var pass = 0; pass < 5; pass++)
        {
            var deficits = OrderUsersByDeficit(solution, constraints, label).ToList();
            if (deficits.Count == 0) break;

            foreach (var user in deficits)
            {
                EnforceForUser(solution, constraints, user, shiftReq, label);
            }
        }

        TrimExcessWithoutFallback(solution, constraints, label);
    }

    public static List<string> GetViolations(ShiftSolution solution, ShiftConstraints constraints, ShiftLabel label)
    {
        var violations = new List<string>();
        foreach (var user in constraints.UserConstraints)
        {
            var exact = label == ShiftLabel.Morning ? user.ExactMorningShiftCount : user.ExactEveningShiftCount;
            var holidayExact = label == ShiftLabel.Morning
                ? user.ExactHolidayMorningShiftCount
                : user.ExactHolidayEveningShiftCount;

            var total = DayShiftQuotaEligibility.CountLabel(solution, user.UserId, label);
            if (exact.HasValue && total < exact.Value)
            {
                violations.Add(
                    $"User {user.UserId} minimum {label} quota not met ({total}/{exact.Value}).");
            }

            if (holidayExact.HasValue)
            {
                var holidayCount = DayShiftQuotaEligibility.CountHolidayLabel(solution, constraints, user.UserId, label);
                if (holidayCount < holidayExact.Value)
                {
                    violations.Add(
                        $"User {user.UserId} minimum holiday {label} quota not met ({holidayCount}/{holidayExact.Value}).");
                }
            }
        }

        return violations;
    }

    public static List<string> GetFallbackPoolWarnings(ShiftSolution solution, ShiftConstraints constraints)
    {
        var warnings = new List<string>();
        foreach (var label in new[] { ShiftLabel.Morning, ShiftLabel.Evening })
        {
            var shiftReq = constraints.ShiftRequirements.FirstOrDefault(s => s.ShiftLabel == label);
            if (shiftReq == null)
            {
                continue;
            }

            var dates = EnumerateDates(constraints).ToList();
            foreach (var date in dates)
            {
                foreach (var specialtyReq in shiftReq.SpecialtyRequirements)
                {
                    var day = specialtyReq.ForDay(constraints.IsHoliday(date));
                    var needed = day.RequiredTotalCount;
                    if (needed <= 0)
                    {
                        continue;
                    }

                    var current = solution.GetShiftAssignments(shiftReq.ShiftId, date)
                        .Count(a => !a.IsOnCall &&
                                    constraints.UserConstraints.FirstOrDefault(u => u.UserId == a.UserId)?.SpecialtyId
                                    == specialtyReq.SpecialtyId);
                    var missing = needed - current;
                    if (missing <= 0)
                    {
                        continue;
                    }

                    var eligible = constraints.UserConstraints.Count(u =>
                        u.IsActive &&
                        u.SpecialtyId == specialtyReq.SpecialtyId &&
                        DayShiftQuotaEligibility.CanAssignInCoverageFill(solution, constraints, u, label, date));
                    if (eligible == 0)
                    {
                        warnings.Add(
                            $"No fallback-eligible users for {label} on {date:yyyy-MM-dd} specialty {specialtyReq.SpecialtyId} ({missing} slot(s) open).");
                    }
                }
            }
        }

        return warnings.Distinct().ToList();
    }

    private static IEnumerable<UserConstraint> OrderUsersByDeficit(
        ShiftSolution solution,
        ShiftConstraints constraints,
        ShiftLabel label) =>
        constraints.UserConstraints
            .Where(u => HasAnyQuota(u, label))
            .Select(u =>
            {
                var total = DayShiftQuotaEligibility.CountLabel(solution, u.UserId, label);
                var holiday = DayShiftQuotaEligibility.CountHolidayLabel(solution, constraints, u.UserId, label);
                var holidayDeficit = Math.Max(0, (GetHolidayExact(u, label) ?? 0) - holiday);
                var totalDeficit = Math.Max(0, (GetExactTotal(u, label) ?? 0) - total);
                return (User: u, HolidayDeficit: holidayDeficit, TotalDeficit: totalDeficit);
            })
            .Where(x => x.HolidayDeficit > 0 || x.TotalDeficit > 0)
            .OrderByDescending(x => x.HolidayDeficit)
            .ThenByDescending(x => x.TotalDeficit)
            .Select(x => x.User);

    private static void EnforceForUser(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        ShiftRequirement shiftReq,
        ShiftLabel label)
    {
        var targetTotal = GetExactTotal(user, label);
        var targetHoliday = GetHolidayExact(user, label);
        var total = DayShiftQuotaEligibility.CountLabel(solution, user.UserId, label);

        if (targetHoliday.HasValue)
        {
            var holidayCount = DayShiftQuotaEligibility.CountHolidayLabel(solution, constraints, user.UserId, label);
            var holidayNeed = targetHoliday.Value - holidayCount;
            if (holidayNeed > 0)
            {
                var room = targetTotal.HasValue ? Math.Max(0, targetTotal.Value - total) : holidayNeed;
                TryFill(solution, constraints, user, shiftReq, label, Math.Min(holidayNeed, room), holidayOnly: true);
            }
        }

        total = DayShiftQuotaEligibility.CountLabel(solution, user.UserId, label);
        if (targetTotal.HasValue)
        {
            var need = targetTotal.Value - total;
            if (need > 0)
            {
                TryFill(solution, constraints, user, shiftReq, label, need, holidayOnly: false);
            }
        }
    }

    private static void TryFill(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        ShiftRequirement shiftReq,
        ShiftLabel label,
        int needed,
        bool holidayOnly)
    {
        if (needed <= 0)
        {
            return;
        }

        foreach (var date in EnumerateDates(constraints))
        {
            if (holidayOnly && !constraints.IsHoliday(date))
            {
                continue;
            }

            if (!holidayOnly && constraints.IsHoliday(date) && GetHolidayExact(user, label).HasValue)
            {
                var holidayCount = DayShiftQuotaEligibility.CountHolidayLabel(solution, constraints, user.UserId, label);
                if (holidayCount >= GetHolidayExact(user, label)!.Value)
                {
                    continue;
                }
            }

            if (solution.HasAssignment(user.UserId, shiftReq.ShiftId, date))
            {
                continue;
            }

            if (!CanPlace(solution, constraints, user, shiftReq, label, date))
            {
                continue;
            }

            var specialtyReq = shiftReq.SpecialtyRequirements.FirstOrDefault(s => s.SpecialtyId == user.SpecialtyId);
            if (specialtyReq == null)
            {
                continue;
            }

            var day = specialtyReq.ForDay(constraints.IsHoliday(date));
            if (day.RequiredTotalCount <= 0)
            {
                continue;
            }

            var current = solution.GetShiftAssignments(shiftReq.ShiftId, date)
                .Count(a => !a.IsOnCall &&
                            constraints.UserConstraints.FirstOrDefault(u => u.UserId == a.UserId)?.SpecialtyId
                            == user.SpecialtyId);
            if (current >= day.RequiredTotalCount)
            {
                continue;
            }

            solution.AddAssignment(user.UserId, shiftReq.ShiftId, date, label, isOnCall: false);
            needed--;
            if (needed <= 0)
            {
                break;
            }
        }
    }

    private static void TrimExcessWithoutFallback(
        ShiftSolution solution,
        ShiftConstraints constraints,
        ShiftLabel label)
    {
        foreach (var user in constraints.UserConstraints.Where(u => HasAnyQuota(u, label)))
        {
            var max = DayShiftQuotaEligibility.GetMaxAllowedTotal(user, label);
            if (max == int.MaxValue)
            {
                continue;
            }

            while (DayShiftQuotaEligibility.CountLabel(solution, user.UserId, label) > max)
            {
                var removable = solution.GetUserAllAssignments(user.UserId)
                    .Where(a => a.ShiftLabel == label && !a.IsOnCall)
                    .Where(a => !IsProtected(solution, constraints, user, a) && !solution.IsLockedSkeleton(a.UserId, a.ShiftId, a.Date))
                    .OrderByDescending(a => constraints.IsHoliday(a.Date) ? 0 : 1)
                    .ThenByDescending(a => a.Date)
                    .FirstOrDefault();
                if (removable == null)
                {
                    break;
                }

                var removed = solution.RemoveAssignment(removable.UserId, removable.ShiftId, removable.Date);
                if (!removed)
                {
                    break;
                }
            }
        }
    }

    private static bool CanPlace(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        ShiftRequirement shiftReq,
        ShiftLabel label,
        DateTime date)
    {
        if (!ShiftEligibilityResolver.MayTakeLabelOnDate(user, label, date))
        {
            return false;
        }

        if (user.UnavailableDates.Any(d => d.Date == date.Date))
        {
            return false;
        }

        if (user.UnavailableShiftSlots.Any(s => s.Date.Date == date.Date && s.ShiftLabel == label))
        {
            return false;
        }

        var existing = solution.GetUserAssignments(user.UserId, date).Select(a => a.ShiftLabel);
        var maxPerDay = constraints.HardRules.EnforceMaxShiftsPerDay
            ? Math.Max(1, constraints.GlobalConstraints.MaxShiftsPerDay)
            : 2;
        if (!DailyAssignmentRules.CanAddShift(
                existing, label, maxPerDay, constraints.HardRules.ForbidDuplicateDailyAssignments))
        {
            return false;
        }

        return !AdjacentShiftRestRules.WouldConflict(
            solution.GetUserAllAssignments(user.UserId), date, label, constraints)
               && !MaxConsecutiveWorkdayRules.WouldExceedMaxConsecutiveWorkdays(
                   solution, constraints, user, date);
    }

    private static bool IsProtected(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        SaShiftAssignment assignment) =>
        solution.IsLockedSkeleton(assignment.UserId, assignment.ShiftId, assignment.Date)
        || assignment.IsSkeleton
        || ApprovedRequestGuard.IsApprovedRequiredSlot(
            user, assignment.Date, assignment.ShiftLabel, assignment.ShiftId);

    private static bool HasAnyQuota(UserConstraint user, ShiftLabel label) =>
        label == ShiftLabel.Morning
            ? user.HasExactMorningQuota || user.ExactHolidayMorningShiftCount.HasValue
            : user.HasExactEveningQuota || user.ExactHolidayEveningShiftCount.HasValue;

    private static int? GetExactTotal(UserConstraint user, ShiftLabel label) =>
        label == ShiftLabel.Morning ? user.ExactMorningShiftCount : user.ExactEveningShiftCount;

    private static int? GetHolidayExact(UserConstraint user, ShiftLabel label) =>
        label == ShiftLabel.Morning ? user.ExactHolidayMorningShiftCount : user.ExactHolidayEveningShiftCount;

    private static IEnumerable<DateTime> EnumerateDates(ShiftConstraints constraints) =>
        Enumerable.Range(0, (constraints.EndDate.Date - constraints.StartDate.Date).Days + 1)
            .Select(i => constraints.StartDate.Date.AddDays(i));
}
