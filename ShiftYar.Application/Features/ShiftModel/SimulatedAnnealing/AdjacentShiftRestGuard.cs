using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using System.Collections.Generic;
using System.Linq;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;

/// <summary>
/// حذف / گزارش توالی ممنوع عصر→شب و شب→صبح.
/// انتساب‌های ناشی از درخواست تأییدشده و شب‌های سهمیه حداقل در اولویت حفظ می‌مانند.
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
                var remove = ChooseRemovable(user, earlier, later, solution);
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
        SaShiftAssignment later,
        ShiftSolution solution)
    {
        var earlierProtected = IsProtectedAssignment(user, earlier, solution);
        var laterProtected = IsProtectedAssignment(user, later, solution);

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

    private static bool IsProtectedAssignment(
        UserConstraint user,
        SaShiftAssignment assignment,
        ShiftSolution solution)
    {
        if (user.RequiredShiftSlots.Any(s =>
                s.Date.Date == assignment.Date.Date && s.ShiftLabel == assignment.ShiftLabel))
        {
            return true;
        }

        if (!assignment.IsOnCall &&
            user.RequiredPresenceDates.Any(d => d.Date == assignment.Date.Date))
        {
            return true;
        }

        // شب‌های داخل سهمیه حداقل را برای توالی ممنوع قربانی نکن
        if (assignment.ShiftLabel == ShiftLabel.Night &&
            !assignment.IsOnCall &&
            user.ExactNightShiftCount.HasValue)
        {
            var nightCount = solution.GetUserAllAssignments(user.UserId)
                .Count(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall);
            if (nightCount <= user.ExactNightShiftCount.Value)
            {
                return true;
            }
        }

        return false;
    }
}
