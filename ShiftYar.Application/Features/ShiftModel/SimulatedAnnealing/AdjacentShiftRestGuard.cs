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
                    solution.GetUserAllAssignments(user.UserId),
                    constraints.HardRules.AllowEveningAfterNightShift);
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
                         solution.GetUserAllAssignments(user.UserId),
                         constraints.HardRules.AllowEveningAfterNightShift))
            {
                var detail = earlier.ShiftLabel == ShiftLabel.Night &&
                             later.Date.Date == earlier.Date.Date.AddDays(1) &&
                             !constraints.HardRules.AllowEveningAfterNightShift
                    ? "روز بعد از شب باید بدون شیفت باشد"
                    : "حداقل یک نوبت فاصله لازم است";
                violations.Add(
                    $"توالی ممنوع شیفت: کاربر {user.UserId} ({user.UserName}) " +
                    $"{earlier.ShiftLabel} در {earlier.Date:yyyy-MM-dd} بلافاصله با " +
                    $"{later.ShiftLabel} در {later.Date:yyyy-MM-dd}؛ {detail}.");
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
        // اولویت ۱: درخواست تأییدشده از سهمیهٔ شب Soft قوی‌تر است
        var earlierRequest = IsRequestProtected(user, earlier);
        var laterRequest = IsRequestProtected(user, later);

        if (earlierRequest && !laterRequest)
        {
            return later;
        }

        if (laterRequest && !earlierRequest)
        {
            return earlier;
        }

        if (earlierRequest && laterRequest)
        {
            return null;
        }

        var earlierQuota = IsQuotaNightProtected(user, earlier, solution);
        var laterQuota = IsQuotaNightProtected(user, later, solution);

        if (!laterQuota)
        {
            return later;
        }

        if (!earlierQuota)
        {
            return earlier;
        }

        return null;
    }

    private static bool IsRequestProtected(UserConstraint user, SaShiftAssignment assignment)
    {
        if (user.RequiredShiftSlots.Any(s =>
                s.Date.Date == assignment.Date.Date && s.ShiftLabel == assignment.ShiftLabel))
        {
            return true;
        }

        return !assignment.IsOnCall &&
               user.RequiredPresenceDates.Any(d => d.Date == assignment.Date.Date);
    }

    private static bool IsQuotaNightProtected(
        UserConstraint user,
        SaShiftAssignment assignment,
        ShiftSolution solution)
    {
        // شب‌های داخل سهمیه حداقل را در برابر انتساب‌های غیر درخواستی قربانی نکن
        if (assignment.ShiftLabel != ShiftLabel.Night ||
            assignment.IsOnCall ||
            !user.ExactNightShiftCount.HasValue)
        {
            return false;
        }

        var nightCount = solution.GetUserAllAssignments(user.UserId)
            .Count(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall);
        return nightCount <= user.ExactNightShiftCount.Value;
    }
}
