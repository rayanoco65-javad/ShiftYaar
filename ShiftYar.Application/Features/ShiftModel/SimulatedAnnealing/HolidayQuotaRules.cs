using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;

/// <summary>
/// قوانین جامع سهمیه و محدودیت شیفت‌های روزهای تعطیل:
/// ۱. اولویت مطلق با درخواست‌های تأییدشده پرسنل است (Level 1 Absolute Priority).
/// ۲. اگر سهمیه انفرادی تعطیل کاربری ۰ وارد شده باشد (ExactHolidayShiftCount == 0 یا سهمیه نوع شیفت == 0)،
///    برنامه تحت هیچ شرایطی نباید به او شیفت تعطیل بدهد مگر آنکه درخواست تأییدشده داشته باشد.
/// ۳. اگر سهمیه کل تعطیل (ExactHolidayShiftCount) تعیین شده باشد، سقف شیفت‌های تعطیل کاربر را کنترل می‌کند.
/// </summary>
public static class HolidayQuotaRules
{
    /// <summary>
    /// بررسی اینکه آیا تخصیص شیفت در این تاریخ تعطیل برای کاربر اکیداً ممنوع است یا خیر.
    /// در صورت وجود درخواست تأییدشده پرسنل (حضور/شیفت مشخص)، درخواست اولویت دارد و ممنوع نیست.
    /// </summary>
    public static bool IsHolidayShiftForbidden(
        UserConstraint user,
        ShiftConstraints constraints,
        DateTime date,
        ShiftLabel label,
        int shiftId = 0)
    {
        // درخواست‌های تأییدشده پرسنل بر تمام سهمیه‌ها و محدودیت‌ها اولویت مطلق دارند
        if (IsApprovedHolidayRequest(user, date, label, shiftId))
        {
            return false;
        }

        var isHoliday = constraints.IsHoliday(date);
        var isHolidayNight = label == ShiftLabel.Night && constraints.IsHolidayWeekendNight(date);

        if (!isHoliday && !isHolidayNight)
        {
            return false;
        }

        // سهمیه کلی شیفت تعطیل = ۰ (ممنوعیت کامل دریافت هرگونه شیفت صبح/عصر/شب در تعطیلات)
        if (user.ExactHolidayShiftCount == 0)
        {
            return true;
        }

        // سهمیه اختصاصی نوع شیفت = ۰
        if (label == ShiftLabel.Morning && user.ExactHolidayMorningShiftCount == 0 && isHoliday)
        {
            return true;
        }

        if (label == ShiftLabel.Evening && user.ExactHolidayEveningShiftCount == 0 && isHoliday)
        {
            return true;
        }

        if (label == ShiftLabel.Night && user.ExactHolidayWeekendNightShiftCount == 0 && isHolidayNight)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// آیا این اسلات به دلیل درخواست تأییدشده پرسنل محافظت‌شده و مجاز است؟
    /// </summary>
    public static bool IsApprovedHolidayRequest(
        UserConstraint user,
        DateTime date,
        ShiftLabel label,
        int shiftId = 0)
    {
        if (ApprovedRequestGuard.IsApprovedRequiredSlot(user, date, label, shiftId))
        {
            return true;
        }

        if (user.RequiredShiftSlots.Any(s => s.Date.Date == date.Date && s.ShiftLabel == label))
        {
            return true;
        }

        if (user.RequiredPresenceDates.Any(d => d.Date == date.Date))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// شمارش کل شیفت‌های تعطیل کاربر (شامل صبح، عصر روی تعطیل و شب روی تعطیل/آخرهفته).
    /// </summary>
    public static int CountTotalHolidayShifts(
        ShiftSolution solution,
        ShiftConstraints constraints,
        int userId)
    {
        return solution.GetUserAllAssignments(userId)
            .Count(a => !a.IsOnCall && (constraints.IsHoliday(a.Date) || (a.ShiftLabel == ShiftLabel.Night && constraints.IsHolidayWeekendNight(a.Date))));
    }

    /// <summary>
    /// بررسی اینکه آیا کاربر به سقف سهمیه کلی تعطیل خود رسیده است یا خیر.
    /// </summary>
    public static bool HasReachedTotalHolidayQuota(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user)
    {
        if (!user.ExactHolidayShiftCount.HasValue)
        {
            return false;
        }

        return CountTotalHolidayShifts(solution, constraints, user.UserId) >= user.ExactHolidayShiftCount.Value;
    }

    /// <summary>
    /// بررسی نهایی تضمین سهمیه صفر: حذف یا انتقال هر شیفت تعطیلی که بدون درخواست تأییدشده به کاربر با سهمیه ۰ داده شده است.
    /// </summary>
    public static void EnforceZeroQuotaGuarantees(ShiftSolution solution, ShiftConstraints constraints)
    {
        foreach (var user in constraints.UserConstraints)
        {
            var hasZeroQuota = user.ExactHolidayShiftCount == 0
                || user.ExactHolidayMorningShiftCount == 0
                || user.ExactHolidayEveningShiftCount == 0
                || user.ExactHolidayWeekendNightShiftCount == 0;

            if (!hasZeroQuota)
            {
                continue;
            }

            var assignments = solution.GetUserAllAssignments(user.UserId)
                .Where(a => !a.IsOnCall)
                .ToList();

            foreach (var a in assignments)
            {
                if (!IsHolidayShiftForbidden(user, constraints, a.Date, a.ShiftLabel, a.ShiftId))
                {
                    continue;
                }

                if (IsApprovedHolidayRequest(user, a.Date, a.ShiftLabel, a.ShiftId))
                {
                    continue;
                }

                // سعی در انتقال به یک همکار مجاز در همان بخش
                var transferred = TryTransferHolidayAssignment(solution, constraints, user, a);
                if (!transferred)
                {
                    // اگر همکار مجازی نبود، اگر اسکلت قفل‌شده نیست حذف می‌کنیم
                    if (!solution.IsLockedSkeleton(user.UserId, a.ShiftId, a.Date))
                    {
                        solution.RemoveAssignment(user.UserId, a.ShiftId, a.Date);
                    }
                }
            }
        }
    }

    private static bool TryTransferHolidayAssignment(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint donor,
        SaShiftAssignment assignment)
    {
        var peers = constraints.UserConstraints
            .Where(u => u.UserId != donor.UserId && u.IsActive && u.SpecialtyId == donor.SpecialtyId)
            .Where(u => !IsHolidayShiftForbidden(u, constraints, assignment.Date, assignment.ShiftLabel, assignment.ShiftId))
            .OrderBy(u => CountTotalHolidayShifts(solution, constraints, u.UserId))
            .ToList();

        foreach (var receiver in peers)
        {
            if (solution.HasAssignment(receiver.UserId, assignment.ShiftId, assignment.Date))
            {
                continue;
            }

            if (receiver.UnavailableDates.Any(d => d.Date == assignment.Date.Date)
                || receiver.UnavailableShiftSlots.Any(s => s.Date.Date == assignment.Date.Date && s.ShiftLabel == assignment.ShiftLabel))
            {
                continue;
            }

            if (!ShiftEligibilityResolver.MayTakeLabelOnDate(receiver, assignment.ShiftLabel, assignment.Date, constraints.StartDate))
            {
                continue;
            }

            var isSkeleton = solution.IsLockedSkeleton(donor.UserId, assignment.ShiftId, assignment.Date) || assignment.IsSkeleton;

            solution.RemoveAssignment(donor.UserId, assignment.ShiftId, assignment.Date, force: true);
            solution.AddAssignment(receiver.UserId, assignment.ShiftId, assignment.Date, assignment.ShiftLabel, assignment.IsOnCall);

            if (isSkeleton)
            {
                var newAsg = solution.GetUserAssignments(receiver.UserId, assignment.Date)
                    .FirstOrDefault(asg => asg.ShiftId == assignment.ShiftId);
                if (newAsg != null)
                {
                    newAsg.IsSkeleton = true;
                }
            }

            return true;
        }

        return false;
    }
}
