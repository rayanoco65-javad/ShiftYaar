using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;

/// <summary>
/// محافظت از انتساب‌های اسکلت مسئول (Phase 1) در برابر گاردهای پس از SA.
/// </summary>
public static class SkeletonAssignmentGuard
{
    public static bool IsSkeletonProtected(SaShiftAssignment assignment) => assignment.IsSkeleton;

    /// <summary>
    /// آیا این انتساب بدون force (یا ON صریح) قابل حذف است؟
    /// </summary>
    public static bool CanRemoveWithoutForce(
        ShiftConstraints constraints,
        UserConstraint? user,
        SaShiftAssignment assignment,
        bool onOverride = false)
    {
        if (!assignment.IsSkeleton)
        {
            return true;
        }

        if (onOverride && user != null &&
            ApprovedRequestGuard.IsApprovedRequiredSlot(
                user, assignment.Date, assignment.ShiftLabel, assignment.ShiftId))
        {
            return true;
        }

        return false;
    }

    public static void MarkSlotManagersAsSkeleton(
        ShiftSolution solution,
        ShiftConstraints constraints,
        ShiftRequirement shiftReq,
        DateTime date)
    {
        foreach (var assignment in solution.GetShiftAssignments(shiftReq.ShiftId, date)
                     .Where(a => !a.IsOnCall))
        {
            var user = constraints.UserConstraints.FirstOrDefault(u => u.UserId == assignment.UserId);
            if (user != null && ShiftManagerRules.IsLevel1(user))
            {
                assignment.IsSkeleton = true;
            }
        }
    }
}
