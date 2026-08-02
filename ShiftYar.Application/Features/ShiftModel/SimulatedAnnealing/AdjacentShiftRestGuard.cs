using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using System.Collections.Generic;
using System.Linq;

namespace ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;

/// <summary>
/// حذف / گزارش توالی ممنوع عصر→شب و شب→صبح.
/// انتساب‌های ناشی از درخواست تأییدشده در اولویت حفظ می‌مانند.
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

                var (earlier, later) = pairs[0];
                var remove = ChooseRemovable(user, earlier, later);
                if (remove == null)
                {
                    // هر دو محافظت‌شده‌اند — بن‌بست؛ حلقه را قطع کن
                    break;
                }

                solution.RemoveAssignment(remove.UserId, remove.ShiftId, remove.Date);
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

    private static SaShiftAssignment? ChooseRemovable(
        UserConstraint user,
        SaShiftAssignment earlier,
        SaShiftAssignment later)
    {
        var earlierProtected = IsApprovedProtected(user, earlier);
        var laterProtected = IsApprovedProtected(user, later);

        if (!laterProtected)
        {
            return later;
        }

        if (!earlierProtected)
        {
            return earlier;
        }

        return null;
    }

    private static bool IsApprovedProtected(UserConstraint user, SaShiftAssignment assignment)
    {
        if (user.RequiredShiftSlots.Any(s =>
                s.Date.Date == assignment.Date.Date && s.ShiftLabel == assignment.ShiftLabel))
        {
            return true;
        }

        return !assignment.IsOnCall &&
               user.RequiredPresenceDates.Any(d => d.Date == assignment.Date.Date);
    }
}
