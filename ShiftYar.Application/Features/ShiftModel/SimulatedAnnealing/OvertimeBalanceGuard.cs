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

        try
        {
            LogAction?.Invoke($"OvertimeBalanceGuard.Enforce called with {users.Count} eligible users.");
        }
        catch { }

        if (users.Count < 2)
        {
            return;
        }

        var lookup = ProductivityWorkedHoursCalculator.BuildShiftInfoLookup(constraints.ShiftRequirements);

        // ۰) فاز اولویت مطلق: جبران کسری کار کلیه پرسنل دارای کسری موظفی از مازاد دیگران (Deficit Elimination)
        EnforceDeficitFill(solution, constraints, users, lookup);

        // ۱) فاز اول: تصفیه اضافه کاری پرسنل بدون رضایت اضافه‌کار (OvertimeConsent == false)
        EnforceNonConsentingRelief(solution, constraints, users, lookup);

        // ۲) فاز دوم: مهار سقف ۸۰ ساعت و توازن عادلانه اضافه‌کاری بین پرسنل متقاضی
        EnforceConsentingOvertimeBalance(solution, constraints, users, lookup);

        // ۳) فاز سوم: یکنواخت‌سازی و برابرسازی اضافه کاری میان پرسنل هم‌سابقه (Peer Overtime Equalization)
        EnforcePeerOvertimeEqualization(solution, constraints, users, lookup);

        // ۴) پاس پایانی: تضمین عدم وجود کسری کار پس از بازتوزیع اضافه‌کار
        EnforceDeficitFill(solution, constraints, users, lookup);
    }

    /// <summary>
    /// جبران کسری کار پرسنل دارای کسری موظفی از شیفت‌های مازاد پرسنل دارای اضافه کار.
    /// اولویت برنامه در اختصاص اضافه کار باید به کاربرانی باشد که کسر کار دارند.
    /// </summary>
    private static void EnforceDeficitFill(
        ShiftSolution solution,
        ShiftConstraints constraints,
        List<UserConstraint> users,
        IReadOnlyDictionary<int, ProductivityWorkedHoursCalculator.ShiftWorkInfo> lookup)
    {
        for (var pass = 0; pass < 32; pass++)
        {
            var progressed = false;

            var deficitUsers = users
                .Select(u => new
                {
                    User = u,
                    Worked = CalculateHours(solution, u, lookup, constraints),
                    Required = (double)u.ProductivityRequiredHours!.Value
                })
                .Where(x => x.Worked < x.Required - 0.25)
                .OrderByDescending(x => x.Required - x.Worked)
                .ToList();

            if (deficitUsers.Count == 0)
            {
                break;
            }

            foreach (var defInfo in deficitUsers)
            {
                var receiver = defInfo.User;
                var receiverWorked = defInfo.Worked;
                var receiverReq = defInfo.Required;

                var donors = users
                    .Where(u => u.UserId != receiver.UserId && u.SpecialtyId == receiver.SpecialtyId)
                    .Select(u => new
                    {
                        User = u,
                        Worked = CalculateHours(solution, u, lookup, constraints),
                        Required = (double)u.ProductivityRequiredHours!.Value
                    })
                    .Where(x => x.Worked > x.Required + 2.0)
                    .OrderByDescending(x => x.Worked - x.Required)
                    .ToList();

                if (donors.Count == 0)
                {
                    continue;
                }

                LogAction?.Invoke($"Deficit user: {receiver.UserName} (Id={receiver.UserId}, Worked={receiverWorked:F1}, Req={receiverReq:F1}). Surplus donors count: {donors.Count}");

                // مسیر ۱: انتقال مستقیم شیفت‌های روزانه
                foreach (var donorInfo in donors)
                {
                    var donor = donorInfo.User;
                    var donorAssignments = solution.GetUserAllAssignments(donor.UserId)
                        .Where(a => !a.IsOnCall && !IsProtected(constraints, donor, a))
                        .Where(a => a.ShiftLabel == ShiftLabel.Morning || a.ShiftLabel == ShiftLabel.Evening)
                        .OrderBy(a => a.Date)
                        .ToList();

                    foreach (var asg in donorAssignments)
                    {
                        if (!CanTakeShift(solution, constraints, lookup, receiver, asg))
                        {
                            continue;
                        }

                        if (!WouldPreserveManagerMix(solution, constraints, asg.ShiftId, asg.Date, donor.UserId, receiver.UserId))
                        {
                            continue;
                        }

                        solution.UnlockSkeletonAssignment(donor.UserId, asg.ShiftId, asg.Date);
                        solution.RemoveAssignment(donor.UserId, asg.ShiftId, asg.Date, force: true);
                        solution.AddAssignment(receiver.UserId, asg.ShiftId, asg.Date, asg.ShiftLabel, isOnCall: false);

                        LogAction?.Invoke($"Route 1 success: Transferred {asg.ShiftLabel} on {asg.Date:yyyy-MM-dd} from {donor.UserName} to {receiver.UserName}");
                        progressed = true;
                        break;
                    }

                    if (progressed) break;
                }

                if (progressed) break;

                // مسیر ۲: اگر انتقال مستقیم ممکن نبود و کاربر شیفت شب دارد، جابه‌جایی شب با اهداکنندگان مازاد جهت آزادسازی روز
                if (receiver.HasExactNightQuota || receiver.ExactNightShiftCount.HasValue)
                {
                    var nightShiftReq = constraints.ShiftRequirements.FirstOrDefault(s => s.ShiftLabel == ShiftLabel.Night);
                    if (nightShiftReq != null)
                    {
                        var receiverNights = solution.GetUserAllAssignments(receiver.UserId)
                            .Where(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall)
                            .OrderBy(a => a.Date)
                            .ToList();

                        LogAction?.Invoke($"Route 2 check for {receiver.UserName}: {receiverNights.Count} nights found.");

                        foreach (var rNight in receiverNights.Where(n => !IsProtected(constraints, receiver, n)))
                        {
                            var freedDate = rNight.Date.Date;
                            var receiverAsgsWithoutRNight = solution.GetUserAllAssignments(receiver.UserId)
                                .Where(a => a.Date.Date != freedDate)
                                .ToList();

                            var dayCandidatesOnFreedDate = solution.Assignments.Values
                                .Where(a => a.Date.Date == freedDate && !a.IsOnCall && (a.ShiftLabel == ShiftLabel.Morning || a.ShiftLabel == ShiftLabel.Evening))
                                .Where(a => a.UserId != receiver.UserId)
                                .Select(a => new
                                {
                                    Assignment = a,
                                    Donor = users.FirstOrDefault(u => u.UserId == a.UserId),
                                    Surplus = users.Where(u => u.UserId == a.UserId)
                                        .Select(u => CalculateHours(solution, u, lookup, constraints) - (double)u.ProductivityRequiredHours!.Value)
                                        .FirstOrDefault()
                                })
                                .Where(x => x.Donor != null && x.Surplus > 2.0 && !IsProtected(constraints, x.Donor, x.Assignment))
                                .OrderByDescending(x =>
                                {
                                    var dAsgs = solution.GetUserAllAssignments(x.Donor!.UserId)
                                        .Where(a => !(a.ShiftId == x.Assignment.ShiftId && a.Date.Date == freedDate))
                                        .ToList();
                                    var canTakeNight = !x.Donor.UnavailableDates.Any(d => d.Date == freedDate)
                                        && !x.Donor.UnavailableShiftSlots.Any(s => s.Date.Date == freedDate && s.ShiftLabel == ShiftLabel.Night)
                                        && ShiftEligibilityResolver.MayTakeLabelOnDate(x.Donor, ShiftLabel.Night, freedDate)
                                        && !AdjacentShiftRestRules.WouldConflict(dAsgs, freedDate, ShiftLabel.Night, constraints)
                                        && !MaxConsecutiveWorkdayRules.WouldExceedMaxConsecutiveWorkdays(dAsgs, x.Donor, freedDate, constraints.HardRules.EnforceMaxConsecutiveShifts)
                                        && WouldPreserveManagerMix(solution, constraints, nightShiftReq.ShiftId, freedDate, receiver.UserId, x.Donor.UserId);
                                    return canTakeNight ? 1 : 0;
                                })
                                .ThenByDescending(x => x.Surplus)
                                .ToList();

                            LogAction?.Invoke($"  freedDate={freedDate:yyyy-MM-dd} (Day {freedDate.Day}): {dayCandidatesOnFreedDate.Count} day donors available.");

                            foreach (var dayCand in dayCandidatesOnFreedDate)
                            {
                                var dayShiftAsg = dayCand.Assignment;
                                var dayDonor = dayCand.Donor!;

                                if (receiver.UnavailableShiftSlots.Any(s => s.Date.Date == freedDate && s.ShiftLabel == dayShiftAsg.ShiftLabel))
                                    continue;

                                if (!ShiftEligibilityResolver.MayTakeLabelOnDate(receiver, dayShiftAsg.ShiftLabel, freedDate))
                                    continue;

                                if (AdjacentShiftRestRules.WouldConflict(receiverAsgsWithoutRNight, freedDate, dayShiftAsg.ShiftLabel, constraints))
                                    continue;

                                if (MaxConsecutiveWorkdayRules.WouldExceedMaxConsecutiveWorkdays(receiverAsgsWithoutRNight, receiver, freedDate, constraints.HardRules.EnforceMaxConsecutiveShifts))
                                    continue;

                                if (!WouldPreserveManagerMix(solution, constraints, dayShiftAsg.ShiftId, freedDate, dayDonor.UserId, receiver.UserId))
                                    continue;

                                var receiverAsgsWithNewDay = receiverAsgsWithoutRNight
                                    .Append(new SaShiftAssignment { UserId = receiver.UserId, ShiftId = dayShiftAsg.ShiftId, Date = freedDate, ShiftLabel = dayShiftAsg.ShiftLabel, IsOnCall = false })
                                    .ToList();

                                var candidateTargetDates = constraints.ShiftRequirements
                                    .Where(s => s.ShiftLabel == ShiftLabel.Night)
                                    .SelectMany(_ => constraints.UserConstraints.SelectMany(u => solution.GetUserAllAssignments(u.UserId)))
                                    .Where(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall && a.Date.Date != freedDate)
                                    .Select(a => a.Date.Date)
                                    .Distinct()
                                    .Where(d => !receiver.UnavailableDates.Any(x => x.Date == d)
                                             && !receiver.UnavailableShiftSlots.Any(s => s.Date.Date == d && s.ShiftLabel == ShiftLabel.Night)
                                             && !AdjacentShiftRestRules.WouldConflict(receiverAsgsWithNewDay, d, ShiftLabel.Night, constraints)
                                             && !MaxConsecutiveWorkdayRules.WouldExceedMaxConsecutiveWorkdays(receiverAsgsWithNewDay, receiver, d, constraints.HardRules.EnforceMaxConsecutiveShifts))
                                    .OrderByDescending(d => d)
                                    .ToList();

                                LogAction?.Invoke($"    DayDonor={dayDonor.UserName}, shift={dayShiftAsg.ShiftLabel}: {candidateTargetDates.Count} target dates: [{string.Join(",", candidateTargetDates.Select(d=>d.Day))}]");

                                var dayDonorAsgsWithoutDayShift = solution.GetUserAllAssignments(dayDonor.UserId)
                                    .Where(a => !(a.ShiftId == dayShiftAsg.ShiftId && a.Date.Date == freedDate))
                                    .ToList();
                                var unavailDate = dayDonor.UnavailableDates.Any(x => x.Date == freedDate);
                                var unavailSlot = dayDonor.UnavailableShiftSlots.Any(s => s.Date.Date == freedDate && s.ShiftLabel == ShiftLabel.Night);
                                var eligibility = ShiftEligibilityResolver.MayTakeLabelOnDate(dayDonor, ShiftLabel.Night, freedDate);
                                var adjacent = AdjacentShiftRestRules.WouldConflict(dayDonorAsgsWithoutDayShift, freedDate, ShiftLabel.Night, constraints);
                                var maxConsec = MaxConsecutiveWorkdayRules.WouldExceedMaxConsecutiveWorkdays(dayDonorAsgsWithoutDayShift, dayDonor, freedDate, constraints.HardRules.EnforceMaxConsecutiveShifts);
                                var mgrMix = WouldPreserveManagerMix(solution, constraints, nightShiftReq.ShiftId, freedDate, receiver.UserId, dayDonor.UserId);

                                var dayDonorCanTakeFreedNight = !unavailDate && !unavailSlot && eligibility && !adjacent && !maxConsec && mgrMix;

                                LogAction?.Invoke($"      DayDonor {dayDonor.UserName} canTakeNight={dayDonorCanTakeFreedNight} (unavailDate={unavailDate}, unavailSlot={unavailSlot}, elig={eligibility}, adj={adjacent}, maxConsec={maxConsec}, mgrMix={mgrMix})");

                                // مسیر ۲-الف: dayDonor خود شیفت شب تاریخ freedDate را می‌گیرد، و یک اهداکننده در targetDate شب را واگذار می‌کند
                                if (dayDonorCanTakeFreedNight)
                                {
                                    foreach (var targetDate in candidateTargetDates)
                                    {
                                        var allNightAssignees = solution.GetShiftAssignments(nightShiftReq.ShiftId, targetDate)
                                            .Where(a => !a.IsOnCall && a.UserId != receiver.UserId)
                                            .Select(a => constraints.UserConstraints.FirstOrDefault(u => u.UserId == a.UserId))
                                            .Where(u => u != null)
                                            .ToList();

                                        foreach (var u in allNightAssignees)
                                        {
                                            if (u!.UserId != dayDonor.UserId)
                                            {
                                                var uSurplus = CalculateHours(solution, u, lookup, constraints) - (double)u.ProductivityRequiredHours!.Value;
                                                if (uSurplus < 2.0) continue;
                                            }

                                            if (!WouldPreserveManagerMix(solution, constraints, nightShiftReq.ShiftId, targetDate, u.UserId, receiver.UserId))
                                                continue;

                                            var uNights = solution.GetUserAllAssignments(u.UserId).Count(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall);
                                            DateTime? balanceDate = null;
                                            SaShiftAssignment? uBalanceDayAsg = null;

                                            // اگر اهداکننده شب سهمیه دقیق دارد و با واگذاری این شب زیر سهمیه می‌رود، با شیفت شب دیگری از dayDonor تعادل برقرار شود
                                            if (u.ExactNightShiftCount.HasValue && uNights <= u.ExactNightShiftCount.Value && u.UserId != dayDonor.UserId)
                                            {
                                                var dayDonorOtherNights = solution.GetUserAllAssignments(dayDonor.UserId)
                                                    .Where(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall && a.Date.Date != freedDate && a.Date.Date != targetDate)
                                                    .ToList();

                                                foreach (var dNight in dayDonorOtherNights)
                                                {
                                                    var bDate = dNight.Date.Date;
                                                    if (u.UnavailableDates.Any(x => x.Date == bDate) || u.UnavailableShiftSlots.Any(s => s.Date.Date == bDate && s.ShiftLabel == ShiftLabel.Night))
                                                        continue;

                                                    var uAsgsWithoutTarget = solution.GetUserAllAssignments(u.UserId)
                                                        .Where(a => a.Date.Date != targetDate && a.Date.Date != bDate)
                                                        .ToList();

                                                    if (AdjacentShiftRestRules.WouldConflict(uAsgsWithoutTarget, bDate, ShiftLabel.Night, constraints))
                                                        continue;

                                                    if (MaxConsecutiveWorkdayRules.WouldExceedMaxConsecutiveWorkdays(uAsgsWithoutTarget, u, bDate, constraints.HardRules.EnforceMaxConsecutiveShifts))
                                                        continue;

                                                    if (!WouldPreserveManagerMix(solution, constraints, nightShiftReq.ShiftId, bDate, dayDonor.UserId, u.UserId))
                                                        continue;

                                                    var dayDonorAsgsWithFreedNight = dayDonorAsgsWithoutDayShift
                                                        .Append(new SaShiftAssignment { UserId = dayDonor.UserId, ShiftId = nightShiftReq.ShiftId, Date = freedDate, ShiftLabel = ShiftLabel.Night, IsOnCall = false })
                                                        .Where(a => a.Date.Date != bDate)
                                                        .ToList();

                                                    var uDayAsgOnBDate = solution.GetUserAssignments(u.UserId, bDate).FirstOrDefault(a => !a.IsOnCall);
                                                    if (uDayAsgOnBDate != null)
                                                    {
                                                        if (dayDonor.UnavailableDates.Any(x => x.Date == bDate) || dayDonor.UnavailableShiftSlots.Any(s => s.Date.Date == bDate && s.ShiftLabel == uDayAsgOnBDate.ShiftLabel))
                                                            continue;
                                                        if (!ShiftEligibilityResolver.MayTakeLabelOnDate(dayDonor, uDayAsgOnBDate.ShiftLabel, bDate))
                                                            continue;
                                                        if (AdjacentShiftRestRules.WouldConflict(dayDonorAsgsWithFreedNight, bDate, uDayAsgOnBDate.ShiftLabel, constraints))
                                                            continue;
                                                        if (MaxConsecutiveWorkdayRules.WouldExceedMaxConsecutiveWorkdays(dayDonorAsgsWithFreedNight, dayDonor, bDate, constraints.HardRules.EnforceMaxConsecutiveShifts))
                                                            continue;
                                                        if (!WouldPreserveManagerMix(solution, constraints, uDayAsgOnBDate.ShiftId, bDate, u.UserId, dayDonor.UserId))
                                                            continue;
                                                    }

                                                    balanceDate = bDate;
                                                    uBalanceDayAsg = uDayAsgOnBDate;
                                                    break;
                                                }

                                                if (balanceDate == null)
                                                    continue;
                                            }

                                            solution.UnlockSkeletonAssignment(receiver.UserId, nightShiftReq.ShiftId, freedDate);
                                            solution.RemoveAssignment(receiver.UserId, nightShiftReq.ShiftId, freedDate, force: true);

                                            solution.UnlockSkeletonAssignment(dayDonor.UserId, dayShiftAsg.ShiftId, freedDate);
                                            solution.RemoveAssignment(dayDonor.UserId, dayShiftAsg.ShiftId, freedDate, force: true);
                                            solution.AddAssignment(dayDonor.UserId, nightShiftReq.ShiftId, freedDate, ShiftLabel.Night, isOnCall: false);

                                            solution.UnlockSkeletonAssignment(u.UserId, nightShiftReq.ShiftId, targetDate);
                                            solution.RemoveAssignment(u.UserId, nightShiftReq.ShiftId, targetDate, force: true);

                                            solution.AddAssignment(receiver.UserId, nightShiftReq.ShiftId, targetDate, ShiftLabel.Night, isOnCall: false);
                                            solution.AddAssignment(receiver.UserId, dayShiftAsg.ShiftId, freedDate, dayShiftAsg.ShiftLabel, isOnCall: false);

                                            if (balanceDate.HasValue)
                                            {
                                                var bDate = balanceDate.Value;
                                                if (uBalanceDayAsg != null)
                                                {
                                                    solution.UnlockSkeletonAssignment(u.UserId, uBalanceDayAsg.ShiftId, bDate);
                                                    solution.RemoveAssignment(u.UserId, uBalanceDayAsg.ShiftId, bDate, force: true);
                                                    solution.AddAssignment(dayDonor.UserId, uBalanceDayAsg.ShiftId, bDate, uBalanceDayAsg.ShiftLabel, isOnCall: false);
                                                }

                                                solution.UnlockSkeletonAssignment(dayDonor.UserId, nightShiftReq.ShiftId, bDate);
                                                solution.RemoveAssignment(dayDonor.UserId, nightShiftReq.ShiftId, bDate, force: true);
                                                solution.AddAssignment(u.UserId, nightShiftReq.ShiftId, bDate, ShiftLabel.Night, isOnCall: false);
                                            }

                                            LogAction?.Invoke($"Route 2A success: Gave {receiver.UserName} {dayDonor.UserName}'s {dayShiftAsg.ShiftLabel} on Day {freedDate.Day}, moved {dayDonor.UserName} to Night on Day {freedDate.Day}, gave {receiver.UserName} {u.UserName}'s Night on Day {targetDate.Day}" + (balanceDate.HasValue ? $", and balanced night between {u.UserName} and {dayDonor.UserName} on Day {balanceDate.Value.Day}" : ""));
                                            progressed = true;
                                            break;
                                        }

                                        if (progressed) break;
                                    }
                                }

                                if (progressed) break;

                                // مسیر ۲-ب: اهداکننده شب در targetDate شیفت شب freedDate را می‌گیرد
                                foreach (var targetDate in candidateTargetDates)
                                {
                                    var allNightAssignees = solution.GetShiftAssignments(nightShiftReq.ShiftId, targetDate)
                                        .Where(a => !a.IsOnCall && a.UserId != receiver.UserId)
                                        .Select(a => constraints.UserConstraints.FirstOrDefault(u => u.UserId == a.UserId))
                                        .Where(u => u != null)
                                        .ToList();

                                    var candidateNightDonors = new List<UserConstraint>();
                                    foreach (var u in allNightAssignees)
                                    {
                                        var reasons = new List<string>();
                                        if (solution.HasAssignment(u!.UserId, nightShiftReq.ShiftId, freedDate)) reasons.Add("AlreadyHasFreedNight");
                                        if (u.UnavailableDates.Any(x => x.Date == freedDate)) reasons.Add("UnavailDateFreed");
                                        if (u.UnavailableShiftSlots.Any(s => s.Date.Date == freedDate && s.ShiftLabel == ShiftLabel.Night)) reasons.Add("UnavailSlotFreed");
                                        var donorAsgsWithoutTarget = solution.GetUserAllAssignments(u!.UserId)
                                            .Where(a => a.Date.Date != targetDate && !(u.UserId == dayDonor.UserId && a.ShiftId == dayShiftAsg.ShiftId && a.Date.Date == freedDate))
                                            .ToList();
                                        if (AdjacentShiftRestRules.WouldConflict(donorAsgsWithoutTarget, freedDate, ShiftLabel.Night, constraints)) reasons.Add("AdjacentConflictFreed");
                                        if (MaxConsecutiveWorkdayRules.WouldExceedMaxConsecutiveWorkdays(donorAsgsWithoutTarget, u, freedDate, constraints.HardRules.EnforceMaxConsecutiveShifts)) reasons.Add("MaxConsecutiveWorkdays");
                                        if (!WouldPreserveManagerMix(solution, constraints, nightShiftReq.ShiftId, targetDate, u.UserId, receiver.UserId)) reasons.Add("BreakMgrMixTarget");
                                        if (!WouldPreserveManagerMix(solution, constraints, nightShiftReq.ShiftId, freedDate, receiver.UserId, u.UserId)) reasons.Add("BreakMgrMixFreed");

                                        if (reasons.Count == 0)
                                        {
                                            candidateNightDonors.Add(u);
                                        }
                                        else
                                        {
                                            LogAction?.Invoke($"        Assignee {u.UserName} rejected for TargetDate {targetDate.Day}->FreedDate {freedDate.Day}: [{string.Join(", ", reasons)}]");
                                        }
                                    }

                                    var nightDonor = candidateNightDonors.FirstOrDefault();
                                    if (nightDonor != null)
                                    {
                                        solution.UnlockSkeletonAssignment(receiver.UserId, nightShiftReq.ShiftId, freedDate);
                                        solution.RemoveAssignment(receiver.UserId, nightShiftReq.ShiftId, freedDate, force: true);

                                        solution.UnlockSkeletonAssignment(nightDonor.UserId, nightShiftReq.ShiftId, targetDate);
                                        solution.RemoveAssignment(nightDonor.UserId, nightShiftReq.ShiftId, targetDate, force: true);

                                        solution.AddAssignment(nightDonor.UserId, nightShiftReq.ShiftId, freedDate, ShiftLabel.Night, isOnCall: false);
                                        solution.AddAssignment(receiver.UserId, nightShiftReq.ShiftId, targetDate, ShiftLabel.Night, isOnCall: false);

                                        solution.UnlockSkeletonAssignment(dayDonor.UserId, dayShiftAsg.ShiftId, freedDate);
                                        solution.RemoveAssignment(dayDonor.UserId, dayShiftAsg.ShiftId, freedDate, force: true);
                                        solution.AddAssignment(receiver.UserId, dayShiftAsg.ShiftId, freedDate, dayShiftAsg.ShiftLabel, isOnCall: false);
                                        LogAction?.Invoke($"Route 2B success: Swapped night {freedDate.Day}<->{targetDate.Day} with {nightDonor.UserName} and gave {dayDonor.UserName}'s {dayShiftAsg.ShiftLabel} on Day {freedDate.Day} to {receiver.UserName}");
                                        progressed = true;
                                        break;
                                    }
                                }

                                if (progressed) break;
                            }

                            if (progressed) break;
                        }
                    }
                }

                if (progressed) break;
            }

            if (!progressed)
            {
                break;
            }
        }
    }

    /// <summary>
    /// انتقال شیفت‌های مازاد غیردرخواستی از پرسنلی که OvertimeConsent = false دارند به همکاران متقاضی.
    /// شامل انتقال مستقیم ۱-به-۱ و اسلاید درون‌روزی ۳-طرفه (صبح به عصر).
    /// </summary>
    private static void EnforceNonConsentingRelief(
        ShiftSolution solution,
        ShiftConstraints constraints,
        List<UserConstraint> users,
        IReadOnlyDictionary<int, ProductivityWorkedHoursCalculator.ShiftWorkInfo> lookup)
    {
        var attemptedSlides = new HashSet<(DateTime Date, int DonorId, int ReceiverId)>();

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
                .Where(x => x.Worked > x.Required + 1.0)
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
                    .Where(x => x.Worked + 6.0 <= x.MaxAllowed + 0.25)
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

                if (candidates.Count > 0)
                {
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

                            // انجام انتقال ۱-به-۱ مستقیم
                            solution.UnlockSkeletonAssignment(donor.UserId, asg.ShiftId, asg.Date);
                            solution.RemoveAssignment(donor.UserId, asg.ShiftId, asg.Date, force: true);
                            solution.AddAssignment(receiver.UserId, asg.ShiftId, asg.Date, asg.ShiftLabel, isOnCall: false);

                            LogAction?.Invoke($"[NonConsentingDirectTransfer] Transferred {asg.ShiftLabel} on {asg.Date:yyyy-MM-dd} from {donor.UserName} to {receiver.UserName}");
                            progressed = true;
                            break;
                        }

                        if (progressed)
                        {
                            break;
                        }
                    }
                }

                if (progressed)
                {
                    break;
                }
            }

            // اگر انتقال مستقیم ۱-به-۱ ممکن نبود، تلاش برای اسلاید درون‌روزی ۳-طرفه
            if (!progressed)
            {
                progressed = TryIntraDayNonConsentingReliefSlide(
                    solution,
                    constraints,
                    lookup,
                    nonConsentingDonors.Select(x => x.User).ToList(),
                    users,
                    attemptedSlides);
            }

            if (!progressed)
            {
                break;
            }
        }
    }

    /// <summary>
    /// اسلاید درون‌روزی ۳ طرفه ویژه پرسنل بدون رضایت اضافه‌کار (یا پرسنل باسابقه در بخش‌های گریزان از اضافه‌کار):
    /// ۱) همکار واجد شرایط از صبح همان روز به عصر منتقل می‌شود تا ترکیب سرپرستی عصر حفظ گردد.
    /// ۲) دهنده بدون رضایت از شیفت عصر همان روز حذف می‌شود (کاهش اضافه‌کار ناخواسته).
    /// ۳) جای خالی شیفت صبح توسط یک پرسنل متقاضی یا جوان‌تر پر می‌شود.
    /// ظرفیت کل شیفت‌های صبح و عصر در طول این عملیات کاملاً ثابت و بدون کاهش باقی می‌ماند.
    /// </summary>
    private static bool TryIntraDayNonConsentingReliefSlide(
        ShiftSolution solution,
        ShiftConstraints constraints,
        IReadOnlyDictionary<int, ProductivityWorkedHoursCalculator.ShiftWorkInfo> lookup,
        List<UserConstraint> nonConsentingDonors,
        List<UserConstraint> allUsers,
        HashSet<(DateTime Date, int DonorId, int ReceiverId)> attemptedSlides)
    {
        foreach (var donor in nonConsentingDonors)
        {
            var donorWorked = CalculateHours(solution, donor, lookup, constraints);
            var donorReq = (double)donor.ProductivityRequiredHours!.Value;
            if (donorWorked <= donorReq + 1.0)
            {
                continue;
            }

            var donorAssignments = solution.GetUserAllAssignments(donor.UserId)
                .Where(a => !a.IsOnCall && !IsProtected(constraints, donor, a))
                .Where(a => a.ShiftLabel == ShiftLabel.Evening)
                .OrderByDescending(a => a.Date)
                .ToList();

            foreach (var asg in donorAssignments)
            {
                var date = asg.Date.Date;
                var donorShiftReq = constraints.ShiftRequirements.FirstOrDefault(s => s.ShiftId == asg.ShiftId);
                if (donorShiftReq == null) continue;

                var otherShiftReq = constraints.ShiftRequirements.FirstOrDefault(s => s.ShiftLabel == ShiftLabel.Morning);
                if (otherShiftReq == null) continue;

                var otherAssignees = solution.GetShiftAssignments(otherShiftReq.ShiftId, date)
                    .Where(a => !a.IsOnCall)
                    .ToList();
                if (otherAssignees.Count == 0) continue;

                var slideCandidates = otherAssignees
                    .Select(a => (Assignment: a, User: constraints.UserConstraints.FirstOrDefault(u => u.UserId == a.UserId)))
                    .Where(x => x.User != null && !IsProtected(constraints, x.User!, x.Assignment) && x.User!.UserId != donor.UserId)
                    .OrderBy(x =>
                    {
                        if (ShiftManagerRules.RequiresAnyManager(donorShiftReq))
                        {
                            return ShiftManagerRules.IsLevel1(x.User!) ? 0 : ShiftManagerRules.IsManager(x.User!) ? 1 : 2;
                        }
                        return 0;
                    })
                    .ToList();

                foreach (var cand in slideCandidates)
                {
                    var mUser = cand.User!;

                    if (mUser.UnavailableDates.Any(d => d.Date == date)) continue;
                    if (mUser.UnavailableShiftSlots.Any(s => s.Date.Date == date && s.ShiftLabel == ShiftLabel.Evening)) continue;
                    if (!ShiftEligibilityResolver.MayTakeLabelOnDate(mUser, ShiftLabel.Evening, date)) continue;

                    var mAsgsWithoutCurrent = solution.GetUserAllAssignments(mUser.UserId)
                        .Where(a => !(a.Date.Date == date && a.ShiftId == cand.Assignment.ShiftId))
                        .ToList();
                    if (AdjacentShiftRestRules.WouldConflict(mAsgsWithoutCurrent, date, ShiftLabel.Evening, constraints)) continue;

                    if (!WouldPreserveManagerMix(solution, constraints, donorShiftReq.ShiftId, date, donor.UserId, mUser.UserId))
                    {
                        continue;
                    }

                    var receivers = allUsers
                        .Where(u => u.UserId != donor.UserId && u.UserId != mUser.UserId && u.SpecialtyId == donor.SpecialtyId)
                        .Select(u =>
                        {
                            var rWorked = CalculateHours(solution, u, lookup, constraints);
                            var rReq = (double)u.ProductivityRequiredHours!.Value;
                            var rMax = ProjectPersonnelProductivityPriority.GetMaxAllowedSchedulingHours(u);
                            return (User: u, Worked: rWorked, Required: rReq, MaxAllowed: rMax);
                        })
                        .Where(x => x.Worked + 6.0 <= x.MaxAllowed + 0.25)
                        .OrderBy(x => x.Worked < x.Required ? 0 : 1)
                        .ThenBy(x => x.User.OvertimeConsent ? 0 : 1)
                        .ThenBy(x =>
                        {
                            if (constraints.EnableOvertimeDistributionBySeniority && constraints.OvertimePreferenceType == 1)
                                return x.User.ExperienceYears;

                            if (constraints.EnableOvertimeDistributionBySeniority && constraints.OvertimePreferenceType == 0)
                                return -x.User.ExperienceYears;

                            return (int)(x.Worked - x.Required);
                        })
                        .ToList();

                    foreach (var receiverInfo in receivers)
                    {
                        var receiver = receiverInfo.User;
                        if (attemptedSlides.Contains((date, donor.UserId, receiver.UserId))) continue;

                        var receiverEffectiveHours = ProductivityWorkedHoursCalculator.ResolveCreditedHours(
                            lookup[otherShiftReq.ShiftId], constraints.IsHoliday(date), receiver.IncludedInProductivityPlan);

                        if (receiverInfo.Worked + receiverEffectiveHours > receiverInfo.MaxAllowed + 0.25) continue;
                        if (solution.GetUserAssignments(receiver.UserId, date).Any(a => !a.IsOnCall)) continue;
                        if (receiver.UnavailableDates.Any(d => d.Date == date)) continue;
                        if (receiver.UnavailableShiftSlots.Any(s => s.Date.Date == date && s.ShiftLabel == ShiftLabel.Morning)) continue;
                        if (!ShiftEligibilityResolver.MayTakeLabelOnDate(receiver, ShiftLabel.Morning, date)) continue;

                        var receiverAsgs = solution.GetUserAllAssignments(receiver.UserId);
                        if (AdjacentShiftRestRules.WouldConflict(receiverAsgs, date, ShiftLabel.Morning, constraints)) continue;
                        if (MaxConsecutiveWorkdayRules.WouldExceedMaxConsecutiveWorkdays(solution, constraints, receiver, date)) continue;

                        if (!WouldPreserveManagerMix(solution, constraints, otherShiftReq.ShiftId, date, mUser.UserId, receiver.UserId))
                        {
                            continue;
                        }

                        attemptedSlides.Add((date, donor.UserId, receiver.UserId));

                        // انجام انتقال ۳ طرفه با تضمین حفظ کامل ظرفیت هر دو شیفت
                        solution.UnlockSkeletonAssignment(donor.UserId, asg.ShiftId, asg.Date);
                        solution.RemoveAssignment(donor.UserId, asg.ShiftId, asg.Date, force: true);
                        solution.RemoveAssignment(mUser.UserId, cand.Assignment.ShiftId, cand.Assignment.Date, force: true);
                        solution.AddAssignment(mUser.UserId, donorShiftReq.ShiftId, date, ShiftLabel.Evening, isOnCall: false);
                        solution.AddAssignment(receiver.UserId, otherShiftReq.ShiftId, date, ShiftLabel.Morning, isOnCall: false);

                        LogAction?.Invoke($"[NonConsentingReliefSlide] On {date:yyyy-MM-dd}: Removed {donor.UserName} (Exp={donor.ExperienceYears}) from Evening, moved {mUser.UserName} to Evening, assigned {receiver.UserName} (Exp={receiver.ExperienceYears}) to Morning");
                        return true;
                    }
                }
            }
        }

        return false;
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
        var attemptedSlides = new HashSet<(DateTime Date, int DonorId, int ReceiverId)>();

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

                // در دپارتمان‌های گریزان از اضافه کار، پرسنل باسابقه (بالای ۷ سال) در صورتی که به تارگت اضافه کار خود رسیده‌اند نباید اضافه کار مازاد بگیرند
                if (constraints.EnableOvertimeDistributionBySeniority && constraints.OvertimePreferenceType == 1)
                {
                    if (receiver.ExperienceYears >= 7 && receiverInfo.Delta >= 0.0)
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

                        // اهداکننده نباید به کسر کار بیفتد
                        if (donorInfo.Worked - shiftEffectiveHours < donorInfo.Required - 0.25)
                        {
                            continue;
                        }

                        // در حالت گریزان از اضافه‌کار، گیرنده باسابقه نباید از تارگت مصوب خود فراتر رود
                        if (constraints.EnableOvertimeDistributionBySeniority && constraints.OvertimePreferenceType == 1)
                        {
                            if (receiver.ExperienceYears >= 7 && newReceiverDelta > 0.5)
                            {
                                continue;
                            }
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
                progressed = TryIntraDayShiftRebalance(solution, constraints, lookup, donors, receivers, attemptedSlides);
            }

            if (!progressed)
            {
                break;
            }
        }
    }

    /// <summary>
    /// بازتوزیع درون‌روزی هوشمند (Intra-Day Slide & Rebalance):
    /// هنگامی که دهنده پرکار (یا باسابقه در حالت گریزان از اضافه‌کار) دارای شیفت عصر غیردرخواستی در یک روز است
    /// و گیرندگان کم‌کار به دلیل شیفت صبح همان روز یا استراحت شب قبل نمی‌توانند مستقیماً عصر را بگیرند:
    /// ۱) یک مسئول یا پرسنل واجد شرایط از صبح همان روز به عصر منتقل می‌شود تا ترکیب سرپرستی عصر حفظ گردد.
    /// ۲) دهنده از شیفت عصر همان روز حذف می‌شود (کاهش اضافه‌کار).
    /// ۳) جای خالی شیفت صبح همان روز توسط یکی از گیرندگان کم‌کار (که شیفت صبح برایش مجاز است) پر می‌شود.
    /// </summary>
    private static bool TryIntraDayShiftRebalance(
        ShiftSolution solution,
        ShiftConstraints constraints,
        IReadOnlyDictionary<int, ProductivityWorkedHoursCalculator.ShiftWorkInfo> lookup,
        List<(UserConstraint User, double Worked, double Required, double Overtime, double MaxAllowed, double TargetOvertime, double Delta)> donors,
        List<(UserConstraint User, double Worked, double Required, double Overtime, double MaxAllowed, double TargetOvertime, double Delta)> receivers,
        HashSet<(DateTime Date, int DonorId, int ReceiverId)> attemptedSlides)
    {
        foreach (var donorInfo in donors.Where(d => d.Delta > 0.5))
        {
            var donor = donorInfo.User;
            var donorAssignments = solution.GetUserAllAssignments(donor.UserId)
                .Where(a => !a.IsOnCall && !IsProtected(constraints, donor, a))
                .Where(a => a.ShiftLabel == ShiftLabel.Evening)
                .OrderByDescending(a => a.Date)
                .ToList();

            foreach (var asg in donorAssignments)
            {
                var date = asg.Date.Date;
                var donorShiftReq = constraints.ShiftRequirements.FirstOrDefault(s => s.ShiftId == asg.ShiftId);
                if (donorShiftReq == null) continue;

                var otherShiftReq = constraints.ShiftRequirements.FirstOrDefault(s => s.ShiftLabel == ShiftLabel.Morning);
                if (otherShiftReq == null) continue;

                var donorEffectiveHours = ProductivityWorkedHoursCalculator.ResolveCreditedHours(
                    lookup[donorShiftReq.ShiftId], constraints.IsHoliday(date), donor.IncludedInProductivityPlan);

                var otherAssignees = solution.GetShiftAssignments(otherShiftReq.ShiftId, date)
                    .Where(a => !a.IsOnCall)
                    .ToList();
                if (otherAssignees.Count == 0) continue;

                var slideCandidates = otherAssignees
                    .Select(a => (Assignment: a, User: constraints.UserConstraints.FirstOrDefault(u => u.UserId == a.UserId)))
                    .Where(x => x.User != null && !IsProtected(constraints, x.User!, x.Assignment) && x.User!.UserId != donor.UserId)
                    .OrderBy(x =>
                    {
                        if (ShiftManagerRules.RequiresAnyManager(donorShiftReq))
                        {
                            return ShiftManagerRules.IsLevel1(x.User!) ? 0 : ShiftManagerRules.IsManager(x.User!) ? 1 : 2;
                        }
                        return 0;
                    })
                    .ToList();

                foreach (var cand in slideCandidates)
                {
                    var mUser = cand.User!;

                    if (mUser.UnavailableDates.Any(d => d.Date == date)) continue;
                    if (mUser.UnavailableShiftSlots.Any(s => s.Date.Date == date && s.ShiftLabel == ShiftLabel.Evening)) continue;
                    if (!ShiftEligibilityResolver.MayTakeLabelOnDate(mUser, ShiftLabel.Evening, date)) continue;

                    var mAsgsWithoutCurrent = solution.GetUserAllAssignments(mUser.UserId)
                        .Where(a => !(a.Date.Date == date && a.ShiftId == cand.Assignment.ShiftId))
                        .ToList();
                    if (AdjacentShiftRestRules.WouldConflict(mAsgsWithoutCurrent, date, ShiftLabel.Evening, constraints)) continue;

                    if (!WouldPreserveManagerMix(solution, constraints, donorShiftReq.ShiftId, date, donor.UserId, mUser.UserId))
                    {
                        continue;
                    }

                    foreach (var receiverInfo in receivers)
                    {
                        var receiver = receiverInfo.User;
                        if (receiver.UserId == donor.UserId || receiver.UserId == mUser.UserId) continue;

                        if (attemptedSlides.Contains((date, donor.UserId, receiver.UserId)))
                        {
                            continue;
                        }

                        if (constraints.EnableOvertimeDistributionBySeniority && constraints.OvertimePreferenceType == 1)
                        {
                            if (receiver.ExperienceYears >= 7 && receiverInfo.Delta >= 0.0)
                            {
                                continue;
                            }
                        }

                        var receiverEffectiveHours = ProductivityWorkedHoursCalculator.ResolveCreditedHours(
                            lookup[otherShiftReq.ShiftId], constraints.IsHoliday(date), receiver.IncludedInProductivityPlan);

                        var oldImbalance = donorInfo.Delta * donorInfo.Delta + receiverInfo.Delta * receiverInfo.Delta;
                        var newDonorDelta = donorInfo.Delta - donorEffectiveHours;
                        var newReceiverDelta = receiverInfo.Delta + receiverEffectiveHours;
                        var newImbalance = newDonorDelta * newDonorDelta + newReceiverDelta * newReceiverDelta;

                        if (newImbalance >= oldImbalance - 0.01)
                        {
                            continue;
                        }

                        if (newDonorDelta < newReceiverDelta - 0.5)
                        {
                            continue;
                        }

                        if (constraints.EnableOvertimeDistributionBySeniority && constraints.OvertimePreferenceType == 1)
                        {
                            if (receiver.ExperienceYears >= 7 && newReceiverDelta > 0.5)
                            {
                                continue;
                            }
                        }

                        if (receiverInfo.Worked + receiverEffectiveHours > receiverInfo.MaxAllowed + 0.25)
                        {
                            continue;
                        }

                        if (solution.GetUserAssignments(receiver.UserId, date).Any(a => !a.IsOnCall))
                        {
                            continue;
                        }

                        if (receiver.UnavailableDates.Any(d => d.Date == date)) continue;
                        if (receiver.UnavailableShiftSlots.Any(s => s.Date.Date == date && s.ShiftLabel == ShiftLabel.Morning)) continue;
                        if (!ShiftEligibilityResolver.MayTakeLabelOnDate(receiver, ShiftLabel.Morning, date)) continue;

                        var receiverAsgs = solution.GetUserAllAssignments(receiver.UserId);
                        if (AdjacentShiftRestRules.WouldConflict(receiverAsgs, date, ShiftLabel.Morning, constraints)) continue;
                        if (MaxConsecutiveWorkdayRules.WouldExceedMaxConsecutiveWorkdays(solution, constraints, receiver, date)) continue;

                        if (!WouldPreserveManagerMix(solution, constraints, otherShiftReq.ShiftId, date, mUser.UserId, receiver.UserId))
                        {
                            continue;
                        }

                        attemptedSlides.Add((date, donor.UserId, receiver.UserId));

                        solution.UnlockSkeletonAssignment(donor.UserId, asg.ShiftId, asg.Date);
                        solution.RemoveAssignment(donor.UserId, asg.ShiftId, asg.Date, force: true);
                        solution.RemoveAssignment(mUser.UserId, cand.Assignment.ShiftId, cand.Assignment.Date, force: true);
                        solution.AddAssignment(mUser.UserId, donorShiftReq.ShiftId, date, ShiftLabel.Evening, isOnCall: false);
                        solution.AddAssignment(receiver.UserId, otherShiftReq.ShiftId, date, ShiftLabel.Morning, isOnCall: false);

                        LogAction?.Invoke($"[IntraDaySlide] On {date:yyyy-MM-dd}: Removed {donor.UserName} from Evening, moved {mUser.UserName} to Evening, assigned {receiver.UserName} to Morning");
                        return true;
                    }
                }
            }
        }

        return false;
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

                        // بررسی پرسنل با سابقه یکسان یا تا ۲ سال اختلاف سابقه
                        if (Math.Abs(u1.User.ExperienceYears - u2.User.ExperienceYears) > 2)
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

                            // اهداکننده نباید به کسر کار بیفتد
                            if (newDonorOt < -0.25)
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
        var approvedCount = user.RequiredShiftSlots.Count(s => s.Date.Date == date.Date);
        var effectiveMax = Math.Max(maxPerDay, approvedCount);

        if (!DailyAssignmentRules.CanAddShift(
                existing,
                label,
                effectiveMax,
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
