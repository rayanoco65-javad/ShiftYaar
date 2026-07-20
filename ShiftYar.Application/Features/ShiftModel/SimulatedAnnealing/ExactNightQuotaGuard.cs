using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;

/// <summary>
/// تضمین تعداد دقیق شیفت شب (و شب‌های تعطیل/آخرهفته) پس از بهینه‌سازی.
/// </summary>
public static class ExactNightQuotaGuard
{
    public static void Enforce(ShiftSolution solution, ShiftConstraints constraints)
    {
        foreach (var user in constraints.UserConstraints.Where(u => u.HasExactNightQuota || u.ExactHolidayWeekendNightShiftCount.HasValue))
        {
            EnforceForUser(solution, constraints, user);
        }
    }

    private static void EnforceForUser(ShiftSolution solution, ShiftConstraints constraints, UserConstraint user)
    {
        var nights = solution.GetUserAllAssignments(user.UserId)
            .Where(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall)
            .OrderBy(a => a.Date)
            .ToList();

        var holidayNights = nights.Where(a => constraints.IsHoliday(a.Date)).ToList();
        var targetTotal = user.ExactNightShiftCount;
        var targetHoliday = user.ExactHolidayWeekendNightShiftCount;

        if (targetHoliday.HasValue)
        {
            while (holidayNights.Count > targetHoliday.Value)
            {
                var remove = holidayNights[^1];
                if (IsProtected(constraints, user.UserId, remove))
                {
                    holidayNights.RemoveAt(holidayNights.Count - 1);
                    continue;
                }

                solution.RemoveAssignment(remove.UserId, remove.ShiftId, remove.Date);
                nights.Remove(remove);
                holidayNights.RemoveAt(holidayNights.Count - 1);
            }
        }

        if (targetTotal.HasValue)
        {
            while (nights.Count > targetTotal.Value)
            {
                // ابتدا شب‌های غیرتعطیل اضافی را بردار تا سهمیه تعطیل حفظ شود
                var remove = nights
                    .Where(a => !IsProtected(constraints, user.UserId, a))
                    .OrderBy(a => constraints.IsHoliday(a.Date) ? 1 : 0)
                    .ThenByDescending(a => a.Date)
                    .FirstOrDefault();
                if (remove == null)
                {
                    break;
                }

                solution.RemoveAssignment(remove.UserId, remove.ShiftId, remove.Date);
                nights.Remove(remove);
                holidayNights.RemoveAll(a => a.ShiftId == remove.ShiftId && a.Date.Date == remove.Date.Date);
            }
        }

        // پر کردن کمبود
        var nightShift = constraints.ShiftRequirements.FirstOrDefault(s => s.ShiftLabel == ShiftLabel.Night);
        if (nightShift == null)
        {
            return;
        }

        nights = solution.GetUserAllAssignments(user.UserId)
            .Where(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall)
            .OrderBy(a => a.Date)
            .ToList();
        holidayNights = nights.Where(a => constraints.IsHoliday(a.Date)).ToList();

        if (targetHoliday.HasValue)
        {
            TryFillNights(solution, constraints, user, nightShift, targetHoliday.Value - holidayNights.Count, holidayOnly: true);
            nights = solution.GetUserAllAssignments(user.UserId)
                .Where(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall)
                .OrderBy(a => a.Date)
                .ToList();
            holidayNights = nights.Where(a => constraints.IsHoliday(a.Date)).ToList();
        }

        if (targetTotal.HasValue)
        {
            var remaining = targetTotal.Value - nights.Count;
            if (remaining > 0)
            {
                // اگر هنوز کمبود تعطیل داریم، اول تعطیل؛ وگرنه هر روز آزاد
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
    }

    private static int CountNights(ShiftSolution solution, int userId) =>
        solution.GetUserAllAssignments(userId).Count(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall);

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

        var dates = Enumerable.Range(0, (constraints.EndDate.Date - constraints.StartDate.Date).Days + 1)
            .Select(offset => constraints.StartDate.Date.AddDays(offset))
            .Where(d => !holidayOnly || constraints.IsHoliday(d))
            .OrderBy(d => holidayOnly ? 0 : (constraints.IsHoliday(d) ? 1 : 0))
            .ThenBy(d => d)
            .ToList();

        foreach (var date in dates)
        {
            if (needed <= 0)
            {
                break;
            }

            if (holidayOnly && !constraints.IsHoliday(date))
            {
                continue;
            }

            if (!holidayOnly && user.ExactHolidayWeekendNightShiftCount.HasValue)
            {
                var holidayCount = solution.GetUserAllAssignments(user.UserId)
                    .Count(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall && constraints.IsHoliday(a.Date));
                var totalCount = CountNights(solution, user.UserId);
                var remainingTotal = (user.ExactNightShiftCount ?? int.MaxValue) - totalCount;
                var remainingHoliday = user.ExactHolidayWeekendNightShiftCount.Value - holidayCount;
                // شب‌های باقی‌مانده را برای تکمیل سهمیه تعطیل نگه دار
                if (!constraints.IsHoliday(date) && remainingHoliday > 0 && remainingTotal <= remainingHoliday)
                {
                    continue;
                }
            }

            if (solution.HasAssignment(user.UserId, nightShift.ShiftId, date))
            {
                continue;
            }

            if (user.UnavailableDates.Any(d => d.Date == date.Date))
            {
                continue;
            }

            if (user.UnavailableShiftSlots.Any(s => s.Date.Date == date.Date && s.ShiftLabel == ShiftLabel.Night))
            {
                continue;
            }

            if (solution.GetUserAllAssignments(user.UserId).Any(a => a.Date.Date == date.Date))
            {
                continue;
            }

            if (AdjacentShiftRestRules.WouldConflict(solution.GetUserAllAssignments(user.UserId), date, ShiftLabel.Night))
            {
                continue;
            }

            if (ViolatesNightSpacing(solution, constraints, user, date))
            {
                continue;
            }

            if (user.ExactNightShiftCount.HasValue && CountNights(solution, user.UserId) >= user.ExactNightShiftCount.Value)
            {
                break;
            }

            if (user.ExactHolidayWeekendNightShiftCount.HasValue &&
                constraints.IsHoliday(date) &&
                solution.GetUserAllAssignments(user.UserId)
                    .Count(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall && constraints.IsHoliday(a.Date))
                    >= user.ExactHolidayWeekendNightShiftCount.Value)
            {
                continue;
            }

            // ظرفیت تخصص را در صورت امکان رعایت کن؛ اگر شیفت پر است رد شو
            if (!HasSpecialtyCapacity(solution, constraints, nightShift, date, user.SpecialtyId))
            {
                continue;
            }

            solution.AddAssignment(user.UserId, nightShift.ShiftId, date, ShiftLabel.Night, isOnCall: false);
            needed--;
        }
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
        ShiftConstraints constraints,
        UserConstraint user,
        DateTime candidateDate)
    {
        var minGap = Math.Max(1, user.MinDaysBetweenNightShifts);
        var nights = solution.GetUserAllAssignments(user.UserId)
            .Where(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall)
            .Select(a => a.Date.Date);
        foreach (var nightDate in nights)
        {
            // MinDaysBetweenNightShifts=2 ⇒ فاصله تقویمی حداکثر ۲ روز (شب‌های خیلی نزدیک) ممنوع
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
}
