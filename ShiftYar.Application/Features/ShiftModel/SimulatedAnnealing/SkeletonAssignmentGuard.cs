using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;

/// <summary>
/// تقویم قفل‌شدهٔ مسئول (Phase 1) — محافظت در برابر گاردهای پس از SA.
/// </summary>
public static class SkeletonAssignmentGuard
{
    public static bool IsLocked(
        ShiftSolution solution,
        SaShiftAssignment assignment) =>
        solution.IsLockedSkeleton(assignment.UserId, assignment.ShiftId, assignment.Date);

    public static bool IsLocked(
        ShiftSolution solution,
        int userId,
        int shiftId,
        DateTime date) =>
        solution.IsLockedSkeleton(userId, shiftId, date);

    /// <summary>
    /// قفل فقط مسئول‌های موردنیاز ترکیب اسلات (تا سقف minLevel1 برای سطح ۱ و requiredTotal در کل).
    /// مسئولین مازاد قفل نمی‌شوند تا سهمیه‌های سخت دیگران مسدود نشود.
    /// </summary>
    public static void LockSlotManagerAssignments(
        ShiftSolution solution,
        ShiftConstraints constraints,
        ShiftRequirement shiftReq,
        DateTime date)
    {
        var (requiredTotal, minLevel1) = ShiftManagerRules.GetRequirement(shiftReq);
        if (requiredTotal <= 0)
        {
            return;
        }

        var assignees = solution.GetShiftAssignments(shiftReq.ShiftId, date)
            .Where(a => !a.IsOnCall)
            .ToList();

        var managerAssignees = assignees
            .Select(a => new { Assignment = a, User = constraints.UserConstraints.FirstOrDefault(u => u.UserId == a.UserId) })
            .Where(x => x.User != null && ShiftManagerRules.IsManager(x.User))
            .ToList();

        var lockedL1 = managerAssignees.Count(x => ShiftManagerRules.IsLevel1(x.User) && solution.IsLockedSkeleton(x.Assignment.UserId, x.Assignment.ShiftId, x.Assignment.Date));
        var lockedTotal = managerAssignees.Count(x => solution.IsLockedSkeleton(x.Assignment.UserId, x.Assignment.ShiftId, x.Assignment.Date));

        // اول فقط تا سقف minLevel1 از مسئولین سطح ۱ قفل می‌شوند
        foreach (var m in managerAssignees.Where(x => ShiftManagerRules.IsLevel1(x.User) && !solution.IsLockedSkeleton(x.Assignment.UserId, x.Assignment.ShiftId, x.Assignment.Date)))
        {
            if (lockedL1 < minLevel1 && lockedTotal < requiredTotal)
            {
                solution.LockSkeletonAssignment(m.Assignment.UserId, m.Assignment.ShiftId, m.Assignment.Date);
                lockedL1++;
                lockedTotal++;
            }
        }

        // سپس بقیه مسئول‌ها تا سقف requiredTotal قفل می‌شوند (ترجیحاً سطح ۲ تا سطح ۱ برای شیفت‌های نیازمند آزاد بماند)
        foreach (var m in managerAssignees
                     .Where(x => !solution.IsLockedSkeleton(x.Assignment.UserId, x.Assignment.ShiftId, x.Assignment.Date))
                     .OrderBy(x => ShiftManagerRules.IsLevel1(x.User) ? 1 : 0))
        {
            if (lockedTotal < requiredTotal)
            {
                solution.LockSkeletonAssignment(m.Assignment.UserId, m.Assignment.ShiftId, m.Assignment.Date);
                lockedTotal++;
            }
        }
    }

    /// <summary>
    /// اسکلت L1 قفل‌شده در strip مجاورت محافظت می‌شود؛ L2 قفل‌شده فقط در productivity/coverage.
    /// </summary>
    public static bool IsLevel1LockedMixSkeleton(
        ShiftConstraints constraints,
        ShiftSolution solution,
        SaShiftAssignment assignment)
    {
        if (!IsLocked(solution, assignment))
        {
            return false;
        }

        var user = constraints.UserConstraints.FirstOrDefault(u => u.UserId == assignment.UserId);
        return user != null && ShiftManagerRules.IsLevel1(user);
    }

    [Obsolete("Use LockSlotManagerAssignments")]
    public static void MarkSlotManagersAsSkeleton(
        ShiftSolution solution,
        ShiftConstraints constraints,
        ShiftRequirement shiftReq,
        DateTime date) =>
        LockSlotManagerAssignments(solution, constraints, shiftReq, date);
}
