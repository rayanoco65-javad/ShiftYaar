using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;
using static ShiftYar.Domain.Enums.UserModel.UserEnums;

namespace ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;

/// <summary>
/// تضمین پوشش ظرفیت اجباری شیفت‌ها (صبح/عصر/شب).
/// عدالت و پخش روزهای کاری نرم هستند؛ جای خالی ظرفیت سخت است و اولویت دارد.
/// </summary>
public static class ShiftCoverageGuard
{
    public static void Enforce(ShiftSolution solution, ShiftConstraints constraints)
    {
        StripExcessCoverage(solution, constraints);
        FillMissingCoverage(solution, constraints);
        StripExcessCoverage(solution, constraints);
    }

    /// <summary>
    /// پس از ForceApply: حذف قطعی مازاد ظرفیت و پر کردن جای خالی — بدون تجاوز از سقف روزانه.
    /// </summary>
    public static void EnforceCapacityCeiling(ShiftSolution solution, ShiftConstraints constraints)
    {
        for (var pass = 0; pass < 8; pass++)
        {
            StripExcessCoverage(solution, constraints, removeProtectedIfNeeded: true);
            FillMissingCoverage(solution, constraints);
            if (!HasAnyOverCapacity(solution, constraints))
            {
                break;
            }
        }

        StripExcessCoverage(solution, constraints, removeProtectedIfNeeded: true);
    }

    private static void FillMissingCoverage(ShiftSolution solution, ShiftConstraints constraints)
    {
        var dates = Enumerable.Range(0, (constraints.EndDate.Date - constraints.StartDate.Date).Days + 1)
            .Select(i => constraints.StartDate.Date.AddDays(i))
            .ToList();

        foreach (var label in new[] { ShiftLabel.Night, ShiftLabel.Morning, ShiftLabel.Evening })
        {
            foreach (var date in dates)
            {
                foreach (var shiftReq in constraints.ShiftRequirements.Where(s => s.ShiftLabel == label))
                {
                    foreach (var specialtyReq in shiftReq.SpecialtyRequirements)
                    {
                        FillSpecialty(solution, constraints, shiftReq, date, specialtyReq);
                    }
                }
            }
        }
    }

    /// <summary>
    /// حذف انتساب‌های بیش از ظرفیت روزانه هر شیفت/تخصص (مثلاً دو شب در یک روز وقتی فقط یک صندلی تعریف شده).
    /// </summary>
    public static void StripExcessCoverage(
        ShiftSolution solution,
        ShiftConstraints constraints,
        bool removeProtectedIfNeeded = false)
    {
        var dates = Enumerable.Range(0, (constraints.EndDate.Date - constraints.StartDate.Date).Days + 1)
            .Select(i => constraints.StartDate.Date.AddDays(i))
            .ToList();

        foreach (var date in dates)
        {
            foreach (var shiftReq in constraints.ShiftRequirements)
            {
                foreach (var specialtyReq in shiftReq.SpecialtyRequirements)
                {
                    StripSpecialtyExcess(solution, constraints, shiftReq, date, specialtyReq, removeProtectedIfNeeded);
                }
            }
        }
    }

    public static bool HasAnyOverCapacity(ShiftSolution solution, ShiftConstraints constraints) =>
        GetOverCapacityViolations(solution, constraints).Count > 0;

    public static List<string> GetOverCapacityViolations(ShiftSolution solution, ShiftConstraints constraints)
    {
        var violations = new List<string>();

        var dates = Enumerable.Range(0, (constraints.EndDate.Date - constraints.StartDate.Date).Days + 1)
            .Select(i => constraints.StartDate.Date.AddDays(i))
            .ToList();

        foreach (var date in dates)
        {
            foreach (var shiftReq in constraints.ShiftRequirements)
            {
                foreach (var specialtyReq in shiftReq.SpecialtyRequirements)
                {
                    var day = specialtyReq.ForDay(constraints.IsHoliday(date));
                    var regular = GetSpecialtyAssignments(
                        solution, constraints, shiftReq, date, specialtyReq.SpecialtyId, isOnCall: false);
                    var onCall = GetSpecialtyAssignments(
                        solution, constraints, shiftReq, date, specialtyReq.SpecialtyId, isOnCall: true);

                    if (regular.Count > day.RequiredTotalCount)
                    {
                        violations.Add(
                            $"Over capacity on {date:yyyy-MM-dd} shift {shiftReq.ShiftLabel} specialty {specialtyReq.SpecialtyId}: " +
                            $"{regular.Count}/{day.RequiredTotalCount} regular.");
                    }

                    if (onCall.Count > day.OnCallTotalCount)
                    {
                        violations.Add(
                            $"Over capacity on {date:yyyy-MM-dd} shift {shiftReq.ShiftLabel} specialty {specialtyReq.SpecialtyId}: " +
                            $"{onCall.Count}/{day.OnCallTotalCount} on-call.");
                    }
                }
            }
        }

        return violations;
    }

    private static void StripSpecialtyExcess(
        ShiftSolution solution,
        ShiftConstraints constraints,
        ShiftRequirement shiftReq,
        DateTime date,
        SpecialtyRequirement specialtyReq,
        bool removeProtectedIfNeeded)
    {
        var day = specialtyReq.ForDay(constraints.IsHoliday(date));
        StripExcessOfType(
            solution, constraints, shiftReq, date, specialtyReq, day.RequiredTotalCount, isOnCall: false, removeProtectedIfNeeded);
        StripExcessOfType(
            solution, constraints, shiftReq, date, specialtyReq, day.OnCallTotalCount, isOnCall: true, removeProtectedIfNeeded);
    }

    private static void StripExcessOfType(
        ShiftSolution solution,
        ShiftConstraints constraints,
        ShiftRequirement shiftReq,
        DateTime date,
        SpecialtyRequirement specialtyReq,
        int maxAllowed,
        bool isOnCall,
        bool removeProtectedIfNeeded)
    {
        while (true)
        {
            var assignments = GetSpecialtyAssignments(
                solution, constraints, shiftReq, date, specialtyReq.SpecialtyId, isOnCall);
            if (assignments.Count <= maxAllowed)
            {
                break;
            }

            var excess = assignments.Count - maxAllowed;
            var removable = RankForRemoval(solution, constraints, assignments)
                .Where(a => !IsProtectedAssignment(constraints, a))
                .Take(excess)
                .ToList();

            if (removable.Count < excess && removeProtectedIfNeeded)
            {
                removable = RankForRemoval(solution, constraints, assignments)
                    .Take(excess)
                    .ToList();
            }

            if (removable.Count == 0)
            {
                break;
            }

            foreach (var assignment in removable)
            {
                solution.RemoveAssignment(assignment.UserId, assignment.ShiftId, assignment.Date);
            }
        }
    }

    private static List<SaShiftAssignment> GetSpecialtyAssignments(
        ShiftSolution solution,
        ShiftConstraints constraints,
        ShiftRequirement shiftReq,
        DateTime date,
        int specialtyId,
        bool isOnCall) =>
        solution.GetShiftAssignments(shiftReq.ShiftId, date)
            .Where(a => a.IsOnCall == isOnCall)
            .Where(a => constraints.UserConstraints.FirstOrDefault(u => u.UserId == a.UserId)?.SpecialtyId == specialtyId)
            .ToList();

    private static IEnumerable<SaShiftAssignment> RankForRemoval(
        ShiftSolution solution,
        ShiftConstraints constraints,
        IReadOnlyList<SaShiftAssignment> assignments)
    {
        return assignments
            .Select(a =>
            {
                var user = constraints.UserConstraints.FirstOrDefault(u => u.UserId == a.UserId);
                var labelCount = user == null
                    ? 0
                    : solution.GetUserAllAssignments(user.UserId)
                        .Count(x => x.ShiftLabel == a.ShiftLabel && !x.IsOnCall);
                var nightSurplus = 0;
                if (user != null &&
                    a.ShiftLabel == ShiftLabel.Night &&
                    user.ExactNightShiftCount.HasValue)
                {
                    nightSurplus = labelCount - user.ExactNightShiftCount.Value;
                }

                return (Assignment: a, NightSurplus: nightSurplus, LabelCount: labelCount, Protected: IsProtectedAssignment(constraints, a));
            })
            .OrderByDescending(x => x.NightSurplus)
            .ThenByDescending(x => x.LabelCount)
            .ThenBy(x => x.Protected ? 1 : 0)
            .ThenByDescending(x => x.Assignment.Date)
            .Select(x => x.Assignment);
    }

    private static bool IsProtectedAssignment(ShiftConstraints constraints, SaShiftAssignment assignment)
    {
        var user = constraints.UserConstraints.FirstOrDefault(u => u.UserId == assignment.UserId);
        if (user == null)
        {
            return false;
        }

        return user.RequiredShiftSlots.Any(s =>
            s.Date.Date == assignment.Date.Date &&
            s.ShiftLabel == assignment.ShiftLabel &&
            (!s.ShiftId.HasValue || s.ShiftId.Value == assignment.ShiftId));
    }

    private static void FillSpecialty(
        ShiftSolution solution,
        ShiftConstraints constraints,
        ShiftRequirement shiftReq,
        DateTime date,
        SpecialtyRequirement specialtyReq)
    {
        var day = specialtyReq.ForDay(constraints.IsHoliday(date));
        var needed = day.RequiredTotalCount;
        if (needed <= 0)
        {
            return;
        }

        var current = solution.GetShiftAssignments(shiftReq.ShiftId, date)
            .Count(a => !a.IsOnCall &&
                        constraints.UserConstraints.FirstOrDefault(u => u.UserId == a.UserId)?.SpecialtyId
                        == specialtyReq.SpecialtyId);

        var missing = needed - current;
        if (missing <= 0)
        {
            return;
        }

        var candidates = constraints.UserConstraints
            .Where(u => u.IsActive && u.SpecialtyId == specialtyReq.SpecialtyId)
            .Where(u => ShiftEligibilityResolver.IsLabelAllowed(u.AllowedShiftLabels, shiftReq.ShiftLabel))
            .Where(u => !u.UnavailableDates.Any(d => d.Date == date.Date))
            .Where(u => !u.UnavailableShiftSlots.Any(s => s.Date.Date == date.Date && s.ShiftLabel == shiftReq.ShiftLabel))
            .Where(u => !solution.HasAssignment(u.UserId, shiftReq.ShiftId, date))
            .Where(u => CanAcceptShift(solution, constraints, u, date, shiftReq.ShiftLabel))
            .OrderBy(u => CoveragePriority(solution, constraints, u, date, shiftReq.ShiftLabel))
            .ThenBy(u => solution.GetUserAllAssignments(u.UserId).Count)
            .ToList();

        foreach (var user in candidates)
        {
            if (missing <= 0)
            {
                break;
            }

            // ممکن است صبح/عصر با قوانین روزانه تداخل داشته باشد — دوباره چک
            if (!CanAcceptShift(solution, constraints, user, date, shiftReq.ShiftLabel))
            {
                continue;
            }

            solution.AddAssignment(user.UserId, shiftReq.ShiftId, date, shiftReq.ShiftLabel, isOnCall: false);
            missing--;
        }
    }

    private static int CoveragePriority(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        DateTime date,
        ShiftLabel label)
    {
        var score = 0;

        // کسری سهمیه شب اولویت مطلق برای شب
        if (label == ShiftLabel.Night)
        {
            var nights = solution.GetUserAllAssignments(user.UserId)
                .Count(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall);
            if (user.ExactNightShiftCount.HasValue && nights < user.ExactNightShiftCount.Value)
            {
                score -= 5000 + (user.ExactNightShiftCount.Value - nights) * 100;
            }

            if (user.ExactHolidayWeekendNightShiftCount.HasValue && constraints.IsHolidayWeekendNight(date))
            {
                var holiday = solution.GetUserAllAssignments(user.UserId)
                    .Count(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall &&
                                constraints.IsHolidayWeekendNight(a.Date));
                if (holiday < user.ExactHolidayWeekendNightShiftCount.Value)
                {
                    score -= 8000;
                }
            }
        }
        else
        {
            // تعادل peer برای صبح/عصر — فقط به‌عنوان اولویت نرم داخل پوشش اجباری
            var totalLabel = solution.GetUserAllAssignments(user.UserId)
                .Count(a => a.ShiftLabel == label && !a.IsOnCall);
            score += totalLabel * 10;

            if (constraints.IsHoliday(date))
            {
                score += HolidayMorningEveningFairnessGuard.CountHolidayLabel(
                    solution, constraints, user.UserId, label) * 40;
            }

            var workDates = solution.GetUserAllAssignments(user.UserId)
                .Where(a => !a.IsOnCall)
                .Select(a => a.Date.Date)
                .ToHashSet();
            if (!workDates.Contains(date.Date))
            {
                var run = 1;
                var c = date.Date.AddDays(-1);
                while (workDates.Contains(c))
                {
                    run++;
                    c = c.AddDays(-1);
                }

                c = date.Date.AddDays(1);
                while (workDates.Contains(c))
                {
                    run++;
                    c = c.AddDays(1);
                }

                score += run * 3;
            }
        }

        if (user.ShiftType == ShiftTypes.FixedShift)
        {
            score -= 100; // فیکس‌ها برای پوشش روزانه مناسب‌اند
        }

        return score;
    }

    private static bool CanAcceptShift(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        DateTime date,
        ShiftLabel label)
    {
        // عمداً سقف هفته/متوالی اینجا اعمال نمی‌شود — پوشش ظرفیت اجباری است
        var existing = solution.GetUserAssignments(user.UserId, date).Select(a => a.ShiftLabel);
        var maxPerDay = constraints.HardRules.EnforceMaxShiftsPerDay
            ? Math.Max(1, constraints.GlobalConstraints.MaxShiftsPerDay)
            : 2;
        if (!DailyAssignmentRules.CanAddShift(
                existing,
                label,
                maxPerDay,
                constraints.HardRules.ForbidDuplicateDailyAssignments))
        {
            return false;
        }

        if (AdjacentShiftRestRules.WouldConflict(
                solution.GetUserAllAssignments(user.UserId), date, label, constraints))
        {
            return false;
        }

        if (label == ShiftLabel.Night && user.MinDaysBetweenNightShifts > 0)
        {
            foreach (var n in solution.GetUserAllAssignments(user.UserId)
                         .Where(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall))
            {
                if (Math.Abs((date.Date - n.Date.Date).Days) <= user.MinDaysBetweenNightShifts)
                {
                    return false;
                }
            }
        }

        return true;
    }
}
