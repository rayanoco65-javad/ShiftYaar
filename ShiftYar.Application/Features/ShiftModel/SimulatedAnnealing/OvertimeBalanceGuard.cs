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
    public static Action<string>? LogAction { get; set; }

    public static void Enforce(ShiftSolution solution, ShiftConstraints constraints)
    {
        var users = constraints.UserConstraints
            .Where(u => u.IsActive && u.ShiftType != ShiftTypes.FixedShift)
            .Where(u => u.IncludedInProductivityPlan && u.ProductivityRequiredHours.HasValue && u.ProductivityRequiredHours > 0)
            .ToList();

        LogAction?.Invoke($"OvertimeBalanceGuard.Enforce called with {users.Count} eligible users.");

        if (users.Count < 2)
        {
            return;
        }

        var lookup = ProductivityWorkedHoursCalculator.BuildShiftInfoLookup(constraints.ShiftRequirements);

        // ۱) فاز اول: تصفیه اضافه کاری پرسنل بدون رضایت اضافه‌کار (OvertimeConsent == false)
        EnforceNonConsentingRelief(solution, constraints, users, lookup);

        // ۲) فاز دوم: مهار سقف ۸۰ ساعت و توازن عادلانه اضافه‌کاری بین پرسنل متقاضی
        EnforceConsentingOvertimeBalance(solution, constraints, users, lookup);

        // ۳) فاز سوم: یکنواخت‌سازی و برابرسازی اضافه کاری میان پرسنل هم‌سابقه (Peer Overtime Equalization)
        EnforcePeerOvertimeEqualization(solution, constraints, users, lookup);
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
                    .OrderBy(x => x.Worked < x.Required ? 0 : 1)
                    .ThenBy(x =>
                    {
                        if (!constraints.EnableOvertimeDistributionBySeniority || constraints.OvertimePreferenceType == 2)
                            return x.Worked - x.Required;

                        if (constraints.OvertimePreferenceType == 0)
                            return -x.User.ExperienceYears;

                        return x.User.ExperienceYears;
                    })
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
    /// متوازن‌سازی اضافه کاری میان پرسنلی که OvertimeConsent = true دارند بر اساس سهمیه سابقه دپارتمان و مهار سقف مجاز ۸۰ ساعت.
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

            var rawStats = users
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

            if (rawStats.Count < 2)
            {
                break;
            }

            var targetLookup = ComputeTargetOvertimeLookup(rawStats, constraints);

            var consentingStats = rawStats
                .Select(x => (x.User, x.Worked, x.Required, x.Overtime, x.MaxAllowed,
                              TargetOvertime: targetLookup[x.User.UserId],
                              Delta: x.Overtime - targetLookup[x.User.UserId]))
                .ToList();

            // مرتب‌سازی بر اساس انحراف از اضافه کاری هدف و اولویت سابقه:
            // Donors: بیشترین اضافه کاری مازاد بر سهمیه هدف (Delta > 0)
            // Receivers: کمترین اضافه کاری نسبت به سهمیه هدف (Delta < 0)
            var donors = consentingStats
                .OrderByDescending(x => x.Delta)
                .ThenBy(x =>
                {
                    if (!constraints.EnableOvertimeDistributionBySeniority || constraints.OvertimePreferenceType == 2)
                        return 0;
                    // در حالت گریزان از اضافه کار (1)، پرسنل با سابقه بیشتر اولویت اهدا (کاهش شیفت) دارند
                    if (constraints.OvertimePreferenceType == 1)
                        return -x.User.ExperienceYears;
                    // در حالت علاقه‌مند (0)، پرسنل با سابقه کمتر اولویت اهدا دارند
                    return x.User.ExperienceYears;
                })
                .ToList();

            var receivers = consentingStats
                .OrderBy(x => x.Delta)
                .ThenBy(x =>
                {
                    if (!constraints.EnableOvertimeDistributionBySeniority || constraints.OvertimePreferenceType == 2)
                        return 0;
                    // در حالت گریزان از اضافه کار (1)، پرسنل با سابقه کمتر اولویت دریافت دارند
                    if (constraints.OvertimePreferenceType == 1)
                        return x.User.ExperienceYears;
                    // در حالت علاقه‌مند (0)، پرسنل با سابقه بیشتر اولویت دریافت دارند
                    return -x.User.ExperienceYears;
                })
                .ToList();

            var highestDonor = donors.First();
            var lowestReceiver = receivers.First();

            LogAction?.Invoke($"Pass {pass}: HighestDonor={highestDonor.User.UserName} (OT={highestDonor.Overtime:F1}, Target={highestDonor.TargetOvertime:F1}, Delta={highestDonor.Delta:F1}), LowestReceiver={lowestReceiver.User.UserName} (OT={lowestReceiver.Overtime:F1}, Target={lowestReceiver.TargetOvertime:F1}, Delta={lowestReceiver.Delta:F1})");

            // اگر اختلاف انحراف از هدف دهنده و گیرنده کمتر از یک شیفت (حدود ۷ ساعت) باشد، وضعیت متعادل است
            if (highestDonor.Delta - lowestReceiver.Delta < 7.0 && highestDonor.Worked <= highestDonor.MaxAllowed + 0.25)
            {
                LogAction?.Invoke("Break: Delta Difference < 7.0 and worked <= max allowed");
                break;
            }

            foreach (var receiverInfo in receivers)
            {
                var receiver = receiverInfo.User;

                // در دپارتمان‌های گریزان از اضافه کار، پرسنل باسابقه (بالای ۷ سال) که به موظفی رسیده‌اند نباید اضافه کار مازاد بگیرند
                if (constraints.EnableOvertimeDistributionBySeniority && constraints.OvertimePreferenceType == 1)
                {
                    if (receiver.ExperienceYears >= 7 && receiverInfo.Overtime >= 0)
                    {
                        continue;
                    }
                }

                foreach (var donorInfo in donors.Where(d => d.User.UserId != receiver.UserId && d.User.SpecialtyId == receiver.SpecialtyId))
                {
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

                        // شرط کاهش شکاف انحراف از هدف بر اساس مجموع مربعات انحراف (SSE)
                        var oldImbalance = donorInfo.Delta * donorInfo.Delta + receiverInfo.Delta * receiverInfo.Delta;
                        var newDonorDelta = donorInfo.Delta - shiftEffectiveHours;
                        var newReceiverDelta = receiverInfo.Delta + shiftEffectiveHours;
                        var newImbalance = newDonorDelta * newDonorDelta + newReceiverDelta * newReceiverDelta;

                        if (newImbalance >= oldImbalance - 0.01)
                        {
                            continue;
                        }

                        // جلوگیری از معکوس شدن جایگاه دهنده و گیرنده یا نوسان پینگ‌پونگی
                        if (newDonorDelta < newReceiverDelta - 0.5)
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

                        LogAction?.Invoke($"Transferred {asg.ShiftLabel} on {asg.Date:yyyy-MM-dd} from {donor.UserName} (OT={donorInfo.Overtime:F1}, Delta={donorInfo.Delta:F1}) to {receiver.UserName} (OT={receiverInfo.Overtime:F1}, Delta={receiverInfo.Delta:F1}), shiftEff={shiftEffectiveHours:F1}");

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

    /// <summary>
    /// یکنواخت‌سازی و تعادل قطعی اضافه کاری میان پرسنلی که سابقه خدمت یکسان یا نزدیک به هم دارند.
    /// این فاز تضمین می‌کند که پرسنل هم‌تراز به دلیل ترتیبات تصادفی تقویم، اختلاف ساعت غیرعادلانه نداشته باشند.
    /// </summary>
    private static void EnforcePeerOvertimeEqualization(
        ShiftSolution solution,
        ShiftConstraints constraints,
        List<UserConstraint> users,
        IReadOnlyDictionary<int, ProductivityWorkedHoursCalculator.ShiftWorkInfo> lookup)
    {
        for (var pass = 0; pass < 32; pass++)
        {
            var progressed = false;

            var stats = users
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

            foreach (var specGroup in stats.GroupBy(x => x.User.SpecialtyId))
            {
                var members = specGroup.ToList();
                if (members.Count < 2) continue;

                var sortedMembers = members.OrderBy(m => m.User.ExperienceYears).ToList();

                for (var i = 0; i < sortedMembers.Count; i++)
                {
                    for (var j = i + 1; j < sortedMembers.Count; j++)
                    {
                        var u1 = sortedMembers[i];
                        var u2 = sortedMembers[j];

                        // بررسی پرسنل با سابقه یکسان یا حداکثر ۱ سال اختلاف
                        if (Math.Abs(u1.User.ExperienceYears - u2.User.ExperienceYears) > 1)
                        {
                            continue;
                        }

                        var (donorInfo, receiverInfo) = u1.Overtime > u2.Overtime ? (u1, u2) : (u2, u1);
                        var otDiff = donorInfo.Overtime - receiverInfo.Overtime;

                        // اگر اختلاف کمتر از ۷ ساعت باشد متعادل است
                        if (otDiff < 7.0)
                        {
                            continue;
                        }

                        var donor = donorInfo.User;
                        var receiver = receiverInfo.User;

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

                            var newDonorOt = donorInfo.Overtime - shiftEffectiveHours;
                            var newReceiverOt = receiverInfo.Overtime + shiftEffectiveHours;
                            var newDiff = Math.Abs(newDonorOt - newReceiverOt);

                            if (newDiff >= otDiff - 0.01)
                            {
                                continue;
                            }

                            // گیرنده پس از دریافت شیفت نباید از دهنده بیشتر شود (مهار قطعی پینگ‌پنگ)
                            if (newReceiverOt > newDonorOt + 0.5)
                            {
                                continue;
                            }

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

                            LogAction?.Invoke($"[PeerEqualization] Transferred {asg.ShiftLabel} on {asg.Date:yyyy-MM-dd} from {donor.UserName} (OT={donorInfo.Overtime:F1}, Exp={donor.ExperienceYears}) to {receiver.UserName} (OT={receiverInfo.Overtime:F1}, Exp={receiver.ExperienceYears}), shiftEff={shiftEffectiveHours:F1}");

                            solution.UnlockSkeletonAssignment(donor.UserId, asg.ShiftId, asg.Date);
                            solution.RemoveAssignment(donor.UserId, asg.ShiftId, asg.Date, force: true);
                            solution.AddAssignment(receiver.UserId, asg.ShiftId, asg.Date, asg.ShiftLabel, isOnCall: false);

                            progressed = true;
                            break;
                        }

                        if (progressed) break;
                    }

                    if (progressed) break;
                }

                if (progressed) break;
            }

            if (!progressed) break;
        }
    }

    private static Dictionary<int, double> ComputeTargetOvertimeLookup(
        List<(UserConstraint User, double Worked, double Required, double Overtime, double MaxAllowed)> stats,
        ShiftConstraints constraints)
    {
        var targetLookup = new Dictionary<int, double>();

        foreach (var group in stats.GroupBy(x => x.User.SpecialtyId))
        {
            var members = group.ToList();
            var totalOvertime = members.Sum(m => Math.Max(0.0, m.Overtime));

            if (totalOvertime <= 0 || members.Count < 2)
            {
                foreach (var m in members)
                {
                    targetLookup[m.User.UserId] = 0.0;
                }
                continue;
            }

            var weights = members.ToDictionary(
                m => m.User.UserId,
                m => constraints.EnableOvertimeDistributionBySeniority && constraints.OvertimePreferenceType != 2
                    ? ShiftSeniorityDistributionGuard.ResolveOvertimeWeight(
                        m.User.ExperienceYears,
                        constraints.OvertimePreferenceType,
                        constraints.OvertimeSeniorityDistributionSlope)
                    : 1.0);

            var totalWeight = weights.Values.Sum();
            if (totalWeight <= 0)
            {
                totalWeight = members.Count;
            }

            foreach (var m in members)
            {
                targetLookup[m.User.UserId] = totalOvertime * (weights[m.User.UserId] / totalWeight);
            }
        }

        return targetLookup;
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
