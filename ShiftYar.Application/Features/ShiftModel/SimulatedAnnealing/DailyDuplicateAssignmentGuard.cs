using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;

/// <summary>
/// حذف ترکیب‌های غیرمجاز روزانه.
/// صبح+عصر و صبح+شب مجاز؛ عصر+شب و تکرار همان لیبل ممنوع.
/// </summary>
public static class DailyDuplicateAssignmentGuard
{
    public static void StripDuplicates(ShiftSolution solution, ShiftConstraints constraints)
    {
        var maxPerDay = constraints.HardRules.EnforceMaxShiftsPerDay
            ? Math.Max(1, constraints.GlobalConstraints.MaxShiftsPerDay)
            : 2;
        var forbidDup = constraints.HardRules.ForbidDuplicateDailyAssignments;

        var groups = solution.Assignments.Values
            .GroupBy(a => new { a.UserId, Date = a.Date.Date })
            .Where(g => g.Count() > 1)
            .ToList();

        foreach (var group in groups)
        {
            var items = group.ToList();
            var labels = items.Select(a => a.ShiftLabel).ToList();
            if (DailyAssignmentRules.IsValidDaySet(labels, maxPerDay, forbidDup))
            {
                continue;
            }

            var user = constraints.UserConstraints.FirstOrDefault(u => u.UserId == group.Key.UserId);
            var keepers = SelectKeepers(solution, items, user, maxPerDay, forbidDup);

            foreach (var extra in items.Where(a => !keepers.Contains(a)))
            {
                if (IsProtected(solution, user, extra))
                {
                    continue;
                }

                solution.UnlockSkeletonAssignment(extra.UserId, extra.ShiftId, extra.Date);
                solution.RemoveAssignment(extra.UserId, extra.ShiftId, extra.Date, force: true);
            }
        }
    }

    public static List<string> GetViolations(ShiftSolution solution, ShiftConstraints? constraints = null)
    {
        var maxPerDay = constraints?.HardRules.EnforceMaxShiftsPerDay == true
            ? Math.Max(1, constraints.GlobalConstraints.MaxShiftsPerDay)
            : 2;
        var forbidDup = constraints?.HardRules.ForbidDuplicateDailyAssignments ?? true;

        return solution.Assignments.Values
            .GroupBy(a => new { a.UserId, Date = a.Date.Date })
            .Where(g => !DailyAssignmentRules.IsValidDaySet(g.Select(a => a.ShiftLabel), maxPerDay, forbidDup))
            .Select(g =>
            {
                var labels = string.Join("+", g.Select(a => a.ShiftLabel));
                return $"User {g.Key.UserId} has invalid same-day shifts on {g.Key.Date:yyyy-MM-dd}: {labels}.";
            })
            .ToList();
    }

    private static HashSet<SaShiftAssignment> SelectKeepers(
        ShiftSolution solution,
        List<SaShiftAssignment> items,
        UserConstraint? user,
        int maxPerDay,
        bool forbidDup)
    {
        // اولویت: اسلات اجباری، سپس شب (سهمیه)، سپس اسکلت، سپس صبح+عصر
        var ordered = items
            .OrderByDescending(a => IsProtected(solution, user, a))
            .ThenByDescending(a => a.ShiftLabel == ShiftLabel.Night && user?.HasExactNightQuota == true)
            .ThenByDescending(a => solution.IsLockedSkeleton(a.UserId, a.ShiftId, a.Date) || a.IsSkeleton)
            .ThenBy(a => AdjacentShiftRestRules.LabelOrder(a.ShiftLabel))
            .ThenBy(a => a.ShiftId)
            .ToList();

        var keepers = new List<SaShiftAssignment>();
        foreach (var candidate in ordered)
        {
            var trial = keepers.Select(k => k.ShiftLabel).Append(candidate.ShiftLabel);
            if (DailyAssignmentRules.IsValidDaySet(trial, maxPerDay, forbidDup))
            {
                keepers.Add(candidate);
            }
        }

        // اگر هنوز صبح+عصر هر دو قابل نگه‌داشتن‌اند ولی فقط یکی مانده، سعی کن جفت را کامل کن
        if (keepers.Count == 1 && maxPerDay >= 2)
        {
            var kept = keepers[0];
            if (kept.ShiftLabel is ShiftLabel.Morning or ShiftLabel.Evening)
            {
                var other = items.FirstOrDefault(a =>
                    a.ShiftLabel == (kept.ShiftLabel == ShiftLabel.Morning ? ShiftLabel.Evening : ShiftLabel.Morning));
                if (other != null)
                {
                    keepers.Add(other);
                }
            }
        }

        return keepers.ToHashSet();
    }

    private static bool IsProtected(ShiftSolution solution, UserConstraint? user, SaShiftAssignment assignment)
    {
        if (solution.IsLockedSkeleton(assignment.UserId, assignment.ShiftId, assignment.Date))
        {
            return true;
        }

        if (user == null)
        {
            return false;
        }

        if (user.RequiredShiftSlots.Any(s =>
                s.Date.Date == assignment.Date.Date && s.ShiftLabel == assignment.ShiftLabel))
        {
            return true;
        }

        return !assignment.IsOnCall &&
               user.RequiredPresenceDates.Any(d => d.Date == assignment.Date.Date);
    }
}
