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
    /// قفل بی‌قید همهٔ مسئول‌های فعلی اسلات (فاز ۱).
    /// </summary>
    public static void LockSlotManagerAssignments(
        ShiftSolution solution,
        ShiftConstraints constraints,
        ShiftRequirement shiftReq,
        DateTime date)
    {
        foreach (var assignment in solution.GetShiftAssignments(shiftReq.ShiftId, date)
                     .Where(a => !a.IsOnCall))
        {
            var user = constraints.UserConstraints.FirstOrDefault(u => u.UserId == assignment.UserId);
            if (user == null || !ShiftManagerRules.IsManager(user))
            {
                continue;
            }

            solution.LockSkeletonAssignment(assignment.UserId, assignment.ShiftId, assignment.Date);
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
