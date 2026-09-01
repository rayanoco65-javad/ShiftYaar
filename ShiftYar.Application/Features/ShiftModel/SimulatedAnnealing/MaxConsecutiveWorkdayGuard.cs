using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;
using static ShiftYar.Domain.Enums.UserModel.UserEnums;

namespace ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;

/// <summary>
/// شکستن بازه‌های کار متوالی بیش از سقف با حذف انتساب غیرمحافظت‌شده (جای خالی بعداً با پوشش پر می‌شود).
/// انتساب‌های اسکلت مسئول بدون جایگزین L1 حذف نمی‌شوند.
/// </summary>
public static class MaxConsecutiveWorkdayGuard
{
    public static void Enforce(
        ShiftSolution solution,
        ShiftConstraints constraints,
        Action<ShiftSolution>? repairSkeletonMix = null)
    {
        if (!constraints.HardRules.EnforceMaxConsecutiveShifts)
        {
            return;
        }

        foreach (var user in constraints.UserConstraints.Where(u =>
                     u.IsActive && u.ShiftType != ShiftTypes.FixedShift))
        {
            BreakLongRuns(solution, constraints, user, repairSkeletonMix);
        }
    }

    private static void BreakLongRuns(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        Action<ShiftSolution>? repairSkeletonMix)
    {
        var max = Math.Max(1, user.MaxConsecutiveShifts);
        for (var pass = 0; pass < 16; pass++)
        {
            var workDates = MaxConsecutiveWorkdayRules.GetCountableWorkDates(solution, user)
                .OrderBy(d => d)
                .ToList();
            if (workDates.Count == 0)
            {
                return;
            }

            var restDate = FindRestDateToInsert(solution, constraints, user, workDates, max);
            if (restDate == null)
            {
                return;
            }

            if (!RemoveClearableAssignmentsOnDate(solution, constraints, user, restDate.Value, repairSkeletonMix))
            {
                return;
            }

            if (MaxConsecutiveWorkdayRules.GetWorkDates(solution, user.UserId).Contains(restDate.Value))
            {
                return;
            }
        }
    }

    private static DateTime? FindRestDateToInsert(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        List<DateTime> workDates,
        int max)
    {
        var i = 0;
        while (i < workDates.Count)
        {
            var runStart = i;
            var runEnd = i;
            while (runEnd + 1 < workDates.Count
                   && (workDates[runEnd + 1] - workDates[runEnd]).Days == 1)
            {
                runEnd++;
            }

            var runLength = runEnd - runStart + 1;
            if (runLength > max)
            {
                var kept = 0;
                for (var j = runStart; j <= runEnd; j++)
                {
                    kept++;
                    if (kept > max && CanClearWorkDay(solution, constraints, user, workDates[j]))
                    {
                        return workDates[j];
                    }
                }
            }

            i = runEnd + 1;
        }

        return null;
    }

    private static bool CanClearWorkDay(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        DateTime date)
    {
        var assignments = solution.GetUserAssignments(user.UserId, date)
            .Where(a => !a.IsOnCall)
            .ToList();
        if (assignments.Count == 0)
        {
            return false;
        }

        if (assignments.Any(a => IsOnProtected(user, a)
                                 || solution.IsLockedSkeleton(a.UserId, a.ShiftId, a.Date)
                                 || a.IsSkeleton))
        {
            return false;
        }

        var nights = assignments.Count(a => a.ShiftLabel == ShiftLabel.Night);
        if (nights > 0 && user.ExactNightShiftCount.HasValue)
        {
            var totalNights = solution.GetUserAllAssignments(user.UserId)
                .Count(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall);
            if (totalNights - nights < user.ExactNightShiftCount.Value)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsOnProtected(UserConstraint user, SaShiftAssignment assignment) =>
        user.RequiredShiftSlots.Any(s =>
            s.Date.Date == assignment.Date.Date && s.ShiftLabel == assignment.ShiftLabel)
        || user.RequiredPresenceDates.Any(d => d.Date == assignment.Date.Date);

    private static bool RemoveClearableAssignmentsOnDate(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        DateTime date,
        Action<ShiftSolution>? repairSkeletonMix)
    {
        var removedSkeletonSlot = false;
        foreach (var assignment in solution.GetUserAssignments(user.UserId, date)
                     .Where(a => !a.IsOnCall)
                     .ToList())
        {
            if (IsOnProtected(user, assignment))
            {
                continue;
            }

            if (solution.IsLockedSkeleton(assignment.UserId, assignment.ShiftId, assignment.Date)
                || assignment.IsSkeleton)
            {
                removedSkeletonSlot = true;
                continue;
            }

            if (assignment.ShiftLabel == ShiftLabel.Night && user.ExactNightShiftCount.HasValue)
            {
                var nights = solution.GetUserAllAssignments(user.UserId)
                    .Count(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall);
                if (nights <= user.ExactNightShiftCount.Value)
                {
                    continue;
                }
            }

            solution.RemoveAssignment(assignment.UserId, assignment.ShiftId, assignment.Date);
        }

        if (removedSkeletonSlot)
        {
            repairSkeletonMix?.Invoke(solution);
        }

        return !solution.GetUserAssignments(user.UserId, date).Any(a => !a.IsOnCall && !IsOnProtected(user, a));
    }
}
