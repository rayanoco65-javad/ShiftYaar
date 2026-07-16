using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using System.Collections.Generic;
using System.Linq;

namespace ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;

/// <summary>
/// حذف انتساب‌هایی که با نوع شیفت کاربر (فیکس/گردشی) سازگار نیستند.
/// </summary>
public static class ShiftEligibilityGuard
{
    public static void StripIneligibleAssignments(ShiftSolution solution, ShiftConstraints constraints)
    {
        foreach (var assignment in solution.Assignments.Values.ToList())
        {
            var user = constraints.UserConstraints.FirstOrDefault(u => u.UserId == assignment.UserId);
            if (user == null)
            {
                continue;
            }

            if (!ShiftEligibilityResolver.IsLabelAllowed(user.AllowedShiftLabels, assignment.ShiftLabel))
            {
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

            if (!ShiftEligibilityResolver.IsLabelAllowed(user.AllowedShiftLabels, assignment.ShiftLabel))
            {
                violations.Add(
                    $"نوع شیفت نقض شد: کاربر {user.UserId} ({user.UserName}) نوع شیفتش اجازهٔ {assignment.ShiftLabel} در {assignment.Date:yyyy-MM-dd} را نمی‌دهد.");
            }
        }

        return violations;
    }
}
