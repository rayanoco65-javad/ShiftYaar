using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using System.Collections.Generic;
using System.Linq;

namespace ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;

/// <summary>
/// حذف / گزارش توالی ممنوع عصر→شب و شب→صبح.
/// </summary>
public static class AdjacentShiftRestGuard
{
    public static void StripForbiddenAdjacencies(ShiftSolution solution, ShiftConstraints constraints)
    {
        foreach (var user in constraints.UserConstraints)
        {
            // چند پاس تا همه جفت‌های ممنوع پاک شوند
            for (var pass = 0; pass < 8; pass++)
            {
                var pairs = AdjacentShiftRestRules.FindForbiddenPairs(
                    solution.GetUserAllAssignments(user.UserId));
                if (pairs.Count == 0)
                {
                    break;
                }

                // انتساب دیرتر حذف می‌شود تا فاصله ایجاد شود
                var later = pairs[0].Later;
                solution.RemoveAssignment(later.UserId, later.ShiftId, later.Date);
            }
        }
    }

    public static List<string> GetViolations(ShiftSolution solution, ShiftConstraints constraints)
    {
        var violations = new List<string>();
        foreach (var user in constraints.UserConstraints)
        {
            foreach (var (earlier, later) in AdjacentShiftRestRules.FindForbiddenPairs(
                         solution.GetUserAllAssignments(user.UserId)))
            {
                violations.Add(
                    $"توالی ممنوع شیفت: کاربر {user.UserId} ({user.UserName}) " +
                    $"{earlier.ShiftLabel} در {earlier.Date:yyyy-MM-dd} بلافاصله با " +
                    $"{later.ShiftLabel} در {later.Date:yyyy-MM-dd}؛ حداقل یک نوبت فاصله لازم است.");
            }
        }

        return violations;
    }
}
