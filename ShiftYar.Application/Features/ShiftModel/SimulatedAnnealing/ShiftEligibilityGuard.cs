using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;

/// <summary>
/// حذف انتساب‌هایی که با مجوز نوع شیفت کاربر سازگار نیستند.
/// </summary>
public static class ShiftEligibilityGuard
{
    public static void StripIneligibleAssignments(ShiftSolution solution, ShiftConstraints constraints)
    {
        foreach (var assignment in solution.Assignments.Values.ToList())
        {
            if (!IsAssignmentEligible(solution, constraints, assignment))
            {
                if (solution.IsLockedSkeleton(assignment.UserId, assignment.ShiftId, assignment.Date)
                    || assignment.IsSkeleton)
                {
                    continue;
                }

                solution.RemoveAssignment(assignment.UserId, assignment.ShiftId, assignment.Date);
            }
        }
    }

    public static List<string> GetViolations(ShiftSolution solution, ShiftConstraints constraints)
    {
        var violations = new List<string>();

        foreach (var assignment in solution.Assignments.Values)
        {
            var user = constraints.UserConstraints.FirstOrDefault(u => u.UserId == assignment.UserId);
            if (user == null)
            {
                continue;
            }

            var maxPerDay = constraints.HardRules.EnforceMaxShiftsPerDay
                ? Math.Max(1, constraints.GlobalConstraints.MaxShiftsPerDay)
                : 2;
            var dayAssignments = solution.GetUserAssignments(user.UserId, assignment.Date).ToList();
            if (MaxShiftsPerDayRules.WouldExceedDailyLimit(dayAssignments.Count - 1, maxPerDay, constraints.HardRules.EnforceMaxShiftsPerDay)
                && dayAssignments.Count > maxPerDay)
            {
                violations.Add(
                    $"سقف شیفت روزانه: کاربر {user.UserId} ({user.UserName}) در {assignment.Date:yyyy-MM-dd} بیش از {maxPerDay} شیفت دارد. {MaxShiftsPerDayRules.SecondShiftBlockedMessage}");
                continue;
            }

            if (!IsAssignmentEligible(solution, constraints, assignment))
            {
                violations.Add(
                    $"نوع شیفت نقض شد: کاربر {user.UserId} ({user.UserName}) نوع شیفتش اجازهٔ {assignment.ShiftLabel} در {assignment.Date:yyyy-MM-dd} را نمی‌دهد.");
            }
        }

        return violations;
    }

    private static bool IsAssignmentEligible(
        ShiftSolution solution,
        ShiftConstraints constraints,
        SaShiftAssignment assignment)
    {
        var user = constraints.UserConstraints.FirstOrDefault(u => u.UserId == assignment.UserId);
        if (user == null)
        {
            return true;
        }

        // درخواست تأییدشده بر مجوزهای اولیه اولویت دارد
        if (ApprovedRequestGuard.IsApprovedRequiredSlot(user, assignment.Date, assignment.ShiftLabel, assignment.ShiftId))
        {
            return true;
        }

        var maxPerDay = constraints.HardRules.EnforceMaxShiftsPerDay
            ? System.Math.Max(1, constraints.GlobalConstraints.MaxShiftsPerDay)
            : 2;
        var existing = solution.GetUserAssignments(assignment.UserId, assignment.Date)
            .Where(a => a.ShiftId != assignment.ShiftId || a.ShiftLabel != assignment.ShiftLabel)
            .Select(a => a.ShiftLabel)
            .ToList();

        return ShiftEligibilityResolver.IsAssignmentAllowed(
            user,
            existing,
            assignment.ShiftLabel,
            maxPerDay,
            constraints.HardRules.ForbidDuplicateDailyAssignments,
            assignment.Date);
    }
}
