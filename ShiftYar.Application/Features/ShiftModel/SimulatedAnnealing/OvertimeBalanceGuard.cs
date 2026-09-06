using System;
using System.Collections.Generic;
using System.Linq;
using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;
using static ShiftYar.Domain.Enums.UserModel.UserEnums;

namespace ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;

/// <summary>
/// گارد هوشمند پس‌پردازش جهت توازن عادلانه اضافه کاری و اعمال دقیق رضایت اضافه کار (OvertimeConsent).
/// این گارد شیفت‌های غیردرخواستی مازاد را از پرسنل پرکار (یا بدون رضایت اضافه کار) به پرسنل کم‌کار متقاضی منتقل می‌کند
/// بدون آنکه درخواست‌های تأییدشده، سهمیه‌های دقیق یا ترکیب سرپرستی نقض شوند.
/// </summary>
public static class OvertimeBalanceGuard
{
    public static void Enforce(ShiftSolution solution, ShiftConstraints constraints)
    {
        var users = constraints.UserConstraints
            .Where(u => u.IsActive && u.ShiftType != ShiftTypes.FixedShift)
            .Where(u => u.IncludedInProductivityPlan && u.ProductivityRequiredHours.HasValue && u.ProductivityRequiredHours > 0)
            .ToList();

        if (users.Count < 2)
        {
            return;
        }

        var lookup = ProductivityWorkedHoursCalculator.BuildShiftInfoLookup(constraints.ShiftRequirements);

        // ۱) فاز اول: تصفیه اضافه کاری پرسنل بدون رضایت اضافه‌کار (OvertimeConsent == false)
        EnforceNonConsentingRelief(solution, constraints, users, lookup);

        // ۲) فاز دوم: مهار سقف ۸۰ ساعت و توازن عادلانه اضافه‌کاری بین پرسنل متقاضی
        EnforceConsentingOvertimeBalance(solution, constraints, users, lookup);
    }

    /// <summary>
    /// انتقال شیفت‌های مازاد غیردرخواستی از پرسنلی که OvertimeConsent = false دارند به همکاران متقاضی.
    /// </summary>
    private static void EnforceNonConsentingRelief(
        ShiftSolution solution,
        ShiftConstraints constraints,
        List<UserConstraint> users,
        IReadOnlyDictionary<int, ProductivityWorkedHoursCalculator.ShiftWorkInfo> lookup)
    {
        for (var pass = 0; pass < 32; pass++)
        {
            var progressed = false;

            var nonConsentingDonors = users
                .Where(u => !u.OvertimeConsent)
                .Select(u => new
                {
                    User = u,
                    Worked = CalculateHours(solution, u, lookup, constraints),
                    Required = (double)u.ProductivityRequiredHours!.Value
                })
                .Where(x => x.Worked > x.Required + 2.0)
                .OrderByDescending(x => x.Worked - x.Required)
                .ToList();

            if (nonConsentingDonors.Count == 0)
            {
                break;
            }

            foreach (var donorInfo in nonConsentingDonors)
            {
                var donor = donorInfo.User;
                var candidates = users
                    .Where(u => u.UserId != donor.UserId && u.SpecialtyId == donor.SpecialtyId && u.OvertimeConsent)
                    .Select(u => new
                    {
                        User = u,
                        Worked = CalculateHours(solution, u, lookup, constraints),
                        Required = (double)u.ProductivityRequiredHours!.Value,
                        MaxAllowed = ProjectPersonnelProductivityPriority.GetMaxAllowedSchedulingHours(u)
                    })
                    .Where(x => x.Worked + 8.0 <= x.MaxAllowed + 0.25)
                    .OrderBy(x => x.Worked - x.Required)
                    .ToList();

                if (candidates.Count == 0)
                {
                    continue;
                }

                var donorAssignments = solution.GetUserAllAssignments(donor.UserId)
                    .Where(a => !a.IsOnCall && !IsProtected(constraints, donor, a))
                    .Where(a => a.ShiftLabel == ShiftLabel.Morning || a.ShiftLabel == ShiftLabel.Evening)
                    .OrderBy(a => a.ShiftLabel == ShiftLabel.Morning ? 0 : 1)
                    .ToList();

                foreach (var asg in donorAssignments)
                {

                    foreach (var receiverInfo in candidates)
                    {
                        var receiver = receiverInfo.User;
                        if (!CanTakeShift(solution, constraints, lookup, receiver, asg))
                        {
                            continue;
                        }

                        if (!WouldPreserveManagerMix(solution, constraints, asg.ShiftId, asg.Date, donor.UserId, receiver.UserId))
                        {
                            continue;
                        }

                        // انجام انتقال
                        solution.UnlockSkeletonAssignment(donor.UserId, asg.ShiftId, asg.Date);
                        solution.RemoveAssignment(donor.UserId, asg.ShiftId, asg.Date, force: true);
                        solution.AddAssignment(receiver.UserId, asg.ShiftId, asg.Date, asg.ShiftLabel, isOnCall: false);

                        progressed = true;
                        break;
                    }

                    if (progressed)
                    {
                        break;
                    }
                }

                if (progressed)
                {
                    break;
                }
            }

            if (!progressed)
            {
                break;
            }
        }
    }

    /// <summary>
    /// متوازن‌سازی اضافه کاری میان پرسنلی که OvertimeConsent = true دارند و مهار سقف مجاز ۸۰ ساعت.
    /// </summary>
    private static void EnforceConsentingOvertimeBalance(
        ShiftSolution solution,
        ShiftConstraints constraints,
        List<UserConstraint> users,
        IReadOnlyDictionary<int, ProductivityWorkedHoursCalculator.ShiftWorkInfo> lookup)
    {
        for (var pass = 0; pass < 64; pass++)
        {
            var progressed = false;

            var consentingStats = users
                .Where(u => u.OvertimeConsent)
                .Select(u =>
                {
                    var worked = CalculateHours(solution, u, lookup, constraints);
                    var req = (double)u.ProductivityRequiredHours!.Value;
                    var ot = worked - req;
                    var max = ProjectPersonnelProductivityPriority.GetMaxAllowedSchedulingHours(u);
                    return (User: u, Worked: worked, Required: req, Overtime: ot, MaxAllowed: max);
                })
                .ToList();

            if (consentingStats.Count < 2)
            {
                break;
            }

            // مرتب‌سازی بر اساس اضافه‌کاری: بیشترین اضافه کاری (Donors) و کمترین اضافه کاری (Receivers)
            var donors = consentingStats
                .OrderByDescending(x => x.Overtime)
                .ToList();

            var receivers = consentingStats
                .OrderBy(x => x.Overtime)
                .ToList();

            var highestDonor = donors.First();
            var lowestReceiver = receivers.First();

            // اگر اختلاف اضافه‌کاری دهنده و گیرنده کمتر از یک شیفت (حدود ۷ ساعت) باشد، وضعیت متعادل است
            if (highestDonor.Overtime - lowestReceiver.Overtime < 7.0 && highestDonor.Worked <= highestDonor.MaxAllowed + 0.25)
            {
                break;
            }

            foreach (var donorInfo in donors)
            {
                // فقط در صورتی انتقال می‌دهیم که دهنده اضافه کاری قابل توجهی نسبت به گیرنده داشته باشد
                var donor = donorInfo.User;

                var donorAssignments = solution.GetUserAllAssignments(donor.UserId)
                    .Where(a => !a.IsOnCall && !IsProtected(constraints, donor, a))
                    .Where(a => a.ShiftLabel == ShiftLabel.Morning || a.ShiftLabel == ShiftLabel.Evening)
                    .OrderBy(a => a.ShiftLabel == ShiftLabel.Morning ? 0 : 1)
                    .ThenByDescending(a => a.Date)
                    .ToList();

                foreach (var asg in donorAssignments)
                {

                    var shiftEffectiveHours = ProductivityWorkedHoursCalculator.ResolveCreditedHours(
                        lookup[asg.ShiftId], constraints.IsHoliday(asg.Date), donor.IncludedInProductivityPlan);

                    // پیدا کردن گیرنده مناسب
                    foreach (var receiverInfo in receivers.Where(r => r.User.UserId != donor.UserId && r.User.SpecialtyId == donor.SpecialtyId))
                    {
                        var receiver = receiverInfo.User;

                        // شرط کاهش شکاف اضافه کاری
                        if (donorInfo.Overtime - receiverInfo.Overtime <= shiftEffectiveHours)
                        {
                            continue;
                        }

                        // گیرنده نباید از سقف قانونی ۸۰ ساعت فراتر رود
                        if (receiverInfo.Worked + shiftEffectiveHours > receiverInfo.MaxAllowed + 0.25)
                        {
                            continue;
                        }

                        if (!CanTakeShift(solution, constraints, lookup, receiver, asg))
                        {
                            continue;
                        }

                        if (!WouldPreserveManagerMix(solution, constraints, asg.ShiftId, asg.Date, donor.UserId, receiver.UserId))
                        {
                            continue;
                        }

                        // انجام انتقال هوشمند شیفت
                        solution.UnlockSkeletonAssignment(donor.UserId, asg.ShiftId, asg.Date);
                        solution.RemoveAssignment(donor.UserId, asg.ShiftId, asg.Date, force: true);
                        solution.AddAssignment(receiver.UserId, asg.ShiftId, asg.Date, asg.ShiftLabel, isOnCall: false);

                        progressed = true;
                        break;
                    }

                    if (progressed)
                    {
                        break;
                    }
                }

                if (progressed)
                {
                    break;
                }
            }

            if (!progressed)
            {
                break;
            }
        }
    }

    public static double CalculateHours(
        ShiftSolution solution,
        UserConstraint user,
        IReadOnlyDictionary<int, ProductivityWorkedHoursCalculator.ShiftWorkInfo> lookup,
        ShiftConstraints constraints)
    {
        var assignments = solution.GetUserAllAssignments(user.UserId).Where(a => !a.IsOnCall).ToList();
        return ProductivityWorkedHoursCalculator.CalculateEffectiveWorkedHours(
            assignments,
            lookup,
            constraints.IsHoliday,
            _ => user.IncludedInProductivityPlan);
    }

    public static double CalculateHours(
        ShiftSolution solution,
        UserConstraint user,
        ShiftConstraints constraints)
    {
        var lookup = ProductivityWorkedHoursCalculator.BuildShiftInfoLookup(constraints.ShiftRequirements);
        return CalculateHours(solution, user, lookup, constraints);
    }

    private static bool IsProtected(
        ShiftConstraints constraints,
        UserConstraint user,
        SaShiftAssignment assignment)
    {
        return ApprovedRequestGuard.IsApprovedRequiredSlot(
            user, assignment.Date, assignment.ShiftLabel, assignment.ShiftId);
    }

    private static bool CanTakeShift(
        ShiftSolution solution,
        ShiftConstraints constraints,
        IReadOnlyDictionary<int, ProductivityWorkedHoursCalculator.ShiftWorkInfo> lookup,
        UserConstraint user,
        SaShiftAssignment targetShift)
    {
        var date = targetShift.Date.Date;
        var label = targetShift.ShiftLabel;

        if (user.UnavailableDates.Any(d => d.Date == date))
        {
            return false;
        }

        if (user.UnavailableShiftSlots.Any(s => s.Date.Date == date && s.ShiftLabel == label))
        {
            return false;
        }

        if (solution.HasAssignment(user.UserId, targetShift.ShiftId, date))
        {
            return false;
        }

        var existing = solution.GetUserAssignments(user.UserId, date).Select(a => a.ShiftLabel).ToList();
        var maxPerDay = constraints.HardRules.EnforceMaxShiftsPerDay
            ? Math.Max(1, constraints.GlobalConstraints.MaxShiftsPerDay)
            : 2;

        if (!DailyAssignmentRules.CanAddShift(
                existing,
                label,
                maxPerDay,
                constraints.HardRules.ForbidDuplicateDailyAssignments))
        {
            return false;
        }

        var userAssignments = solution.GetUserAllAssignments(user.UserId);
        if (AdjacentShiftRestRules.WouldConflict(userAssignments, date, label, constraints))
        {
            return false;
        }

        if (MaxConsecutiveWorkdayRules.WouldExceedMaxConsecutiveWorkdays(solution, constraints, user, date))
        {
            return false;
        }

        if (label == ShiftLabel.Night)
        {
            if (user.MinDaysBetweenNightShifts > 0)
            {
                foreach (var n in userAssignments.Where(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall))
                {
                    if (Math.Abs((date - n.Date.Date).Days) <= user.MinDaysBetweenNightShifts)
                    {
                        return false;
                    }
                }
            }

            if (!NightQuotaEligibility.CanAssignInCoverageFill(solution, constraints, user, date))
            {
                return false;
            }
        }
        else if (label == ShiftLabel.Morning || label == ShiftLabel.Evening)
        {
            if (!DayShiftQuotaEligibility.CanAssignInCoverageFill(solution, constraints, user, label, date))
            {
                return false;
            }
        }

        return true;
    }

    public static bool WouldPreserveManagerMix(
        ShiftSolution solution,
        ShiftConstraints constraints,
        int shiftId,
        DateTime date,
        int donorUserId,
        int receiverUserId)
    {
        var shiftReq = constraints.ShiftRequirements.FirstOrDefault(s => s.ShiftId == shiftId);
        if (shiftReq == null || !ShiftManagerRules.RequiresAnyManager(shiftReq))
        {
            return true;
        }

        var (requiredTotal, minLevel1) = ShiftManagerRules.GetRequirement(shiftReq);
        var assignees = solution.GetShiftAssignments(shiftId, date)
            .Where(a => !a.IsOnCall && a.UserId != donorUserId)
            .Select(a => constraints.UserConstraints.FirstOrDefault(u => u.UserId == a.UserId))
            .Where(u => u != null)
            .Select(u => u!)
            .ToList();

        var receiver = constraints.UserConstraints.FirstOrDefault(u => u.UserId == receiverUserId);
        if (receiver != null)
        {
            assignees.Add(receiver);
        }

        return ShiftManagerRules.IsSatisfied(assignees, requiredTotal, minLevel1);
    }
}
