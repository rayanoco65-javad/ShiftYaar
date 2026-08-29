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
                    constraints.HardRules);
                if (pairs.Count == 0)
                {
                    break;
                }

                var (earlier, later) = pairs[0];
                // اگر فقط به‌خاطر تنظیمات عصر/شب بعد از شب ممنوع است و شیفت بعدی ON تأییدشده است،
                // انتساب غیرمحافظت‌شدهٔ قبلی را حذف کن؛ هر دو محافظت‌شده → نگه دار.
                if (IsWaivedByApprovedLaterSlot(user, earlier, later))
                {
                    if (IsRequestProtected(user, earlier))
                    {
                        break;
                    }

                    solution.RemoveAssignment(earlier.UserId, earlier.ShiftId, earlier.Date);
                    continue;
                }

                var remove = ChooseRemovable(user, earlier, later, solution, constraints);
                if (remove == null)
                {
                    // هر دو ON تأییدشده‌اند — این جفت گزارش نمی‌شود؛ حلقه را قطع کن
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
            foreach (var (earlier, later) in FindReportableForbiddenPairs(
                         user,
                         solution.GetUserAllAssignments(user.UserId),
                         constraints.HardRules))
            {
                var detail =
                    earlier.ShiftLabel == ShiftLabel.Night &&
                    later.ShiftLabel == ShiftLabel.Morning &&
                    later.Date.Date == earlier.Date.Date.AddDays(1)
                        ? "شیفت شب (۱۲ ساعت) با شیفت صبح روز بعد قابل ترکیب نیست — بیش از ۱۲ ساعت کار متوالی"
                        : earlier.ShiftLabel == ShiftLabel.Night &&
                          later.ShiftLabel == ShiftLabel.Evening &&
                          later.Date.Date == earlier.Date.Date.AddDays(1) &&
                          !constraints.HardRules.AllowEveningAfterNightShift
                            ? "شیفت عصر روز بعد از شب مجاز نیست (تنظیمات دپارتمان)"
                        : earlier.ShiftLabel == ShiftLabel.Night &&
                          later.ShiftLabel == ShiftLabel.Night &&
                          later.Date.Date == earlier.Date.Date.AddDays(1) &&
                          !constraints.HardRules.AllowNightShiftAfterNightShift
                            ? "شیفت شب روز بعد از شب مجاز نیست (تنظیمات دپارتمان)"
                            : "حداقل یک نوبت فاصله لازم است";
                violations.Add(
                    $"توالی ممنوع شیفت: کاربر {user.UserId} ({user.UserName}) " +
                    $"{earlier.ShiftLabel} در {earlier.Date:yyyy-MM-dd} بلافاصله با " +
                    $"{later.ShiftLabel} در {later.Date:yyyy-MM-dd}؛ {detail}.");
            }
        }

        return violations;
    }

    /// <summary>
    /// جفت‌های ممنوع برای feasibility/گزارش؛ محدودیت عصر/شب بعد از شب
    /// اگر انتساب بعدی از درخواست تأییدشده باشد، به‌خاطر اولویت ON نادیده گرفته می‌شود.
    /// </summary>
    public static bool HasReportableForbiddenPair(
        UserConstraint user,
        IEnumerable<SaShiftAssignment> assignments,
        HardRuleSet rules) =>
        FindReportableForbiddenPairs(user, assignments, rules).Count > 0;

    public static List<(SaShiftAssignment Earlier, SaShiftAssignment Later)> FindReportableForbiddenPairs(
        UserConstraint user,
        IEnumerable<SaShiftAssignment> assignments,
        HardRuleSet rules)
    {
        return AdjacentShiftRestRules.FindForbiddenPairs(assignments, rules)
            .Where(p => !IsWaivedByApprovedLaterSlot(user, p.Earlier, p.Later))
            .ToList();
    }

    /// <summary>
    /// درخواست تأییدشدهٔ شیفت بعدی (عصر یا شب روز بعد از شب) بر تنظیمات دپارتمان اولویت دارد.
    /// </summary>
    public static bool IsWaivedByApprovedLaterSlot(
        UserConstraint user,
        SaShiftAssignment earlier,
        SaShiftAssignment later)
    {
        if (!AdjacentShiftRestRules.IsSettingsControlledAfterNightPair(
                earlier.ShiftLabel, earlier.Date, later.ShiftLabel, later.Date))
        {
            return false;
        }

        return IsRequestProtected(user, later);
    }

    private static SaShiftAssignment? ChooseRemovable(
        UserConstraint user,
        SaShiftAssignment earlier,
        SaShiftAssignment later,
        ShiftSolution solution,
        ShiftConstraints constraints)
    {
        var earlierManagerCritical = ShiftManagerRules.IsCriticalForManagerMix(constraints, solution, earlier);
        var laterManagerCritical = ShiftManagerRules.IsCriticalForManagerMix(constraints, solution, later);
        var earlierOn = ApprovedRequestGuard.IsApprovedRequiredSlot(
            user, earlier.Date, earlier.ShiftLabel, earlier.ShiftId);
        var laterOn = ApprovedRequestGuard.IsApprovedRequiredSlot(
            user, later.Date, later.ShiftLabel, later.ShiftId);

        if (laterOn && !earlierOn)
        {
            return IsRequestProtected(user, earlier) ? null : earlier;
        }

        if (earlierOn && !laterOn)
        {
            return IsRequestProtected(user, later) ? null : later;
        }

        // شب→شب / شب→عصر تنظیمات دپارتمان: شیفت بعدی غیرِON همیشه حذف شود (سهمیه/مسئول اولویت ندارند)
        if (AdjacentShiftRestRules.IsSettingsControlledAfterNightPair(
                earlier.ShiftLabel, earlier.Date, later.ShiftLabel, later.Date)
            && !IsRequestProtected(user, later))
        {
            return later;
        }

        if (earlierManagerCritical && laterManagerCritical)
        {
            return null;
        }

        if (laterManagerCritical)
        {
            if (IsRequestProtected(user, earlier))
            {
                return null;
            }

            return earlier;
        }

        if (earlierManagerCritical)
        {
            if (IsRequestProtected(user, later))
            {
                return null;
            }

            return later;
        }

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

        return later;
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
