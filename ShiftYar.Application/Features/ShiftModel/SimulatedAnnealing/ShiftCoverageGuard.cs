using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;
using static ShiftYar.Domain.Enums.UserModel.UserEnums;

namespace ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;

/// <summary>
/// تضمین پوشش ظرفیت اجباری شیفت‌ها (صبح/عصر/شب).
/// عدالت و پخش روزهای کاری نرم هستند؛ جای خالی ظرفیت سخت است و اولویت دارد.
/// </summary>
public static class ShiftCoverageGuard
{
    public static void Enforce(ShiftSolution solution, ShiftConstraints constraints)
    {
        StripExcessCoverage(solution, constraints);
        FillMissingCoverage(solution, constraints, reserveUnmetOnSlots: true);
        StripExcessCoverage(solution, constraints);
    }

    /// <summary>
    /// پر کردن جای خالی بعد از ForceApply — اسلات ON ناموفق دیگر ظرفیت را قفل نمی‌کند.
    /// </summary>
    public static void FillRemainingAfterForceApply(ShiftSolution solution, ShiftConstraints constraints)
    {
        FillMissingCoverage(solution, constraints, reserveUnmetOnSlots: false);
    }

    /// <summary>

    /// <summary>
    /// پس از گاردهای عدالت: حذف مازاد غیر ON، پر کردن جای خالی، سپس ForceApply به‌عنوان آخرین حرف مطلق.
    /// هیچ strip بعد از ForceApply اجرا نمی‌شود تا درخواست ON حذف نشود.
    /// </summary>
    public static void EnforceCapacityCeiling(ShiftSolution solution, ShiftConstraints constraints)
    {
        for (var pass = 0; pass < 8; pass++)
        {
            StripExcessCoverage(solution, constraints);
            FillMissingCoverage(solution, constraints, reserveUnmetOnSlots: true);
            ApprovedRequestGuard.ForceApply(solution, constraints);

            if (!HasAnyOverCapacity(solution, constraints))
            {
                break;
            }
        }

        ApprovedRequestGuard.ForceApply(solution, constraints);
    }

    private static void FillMissingCoverage(
        ShiftSolution solution,
        ShiftConstraints constraints,
        bool reserveUnmetOnSlots)
    {
        var dates = Enumerable.Range(0, (constraints.EndDate.Date - constraints.StartDate.Date).Days + 1)
            .Select(i => constraints.StartDate.Date.AddDays(i))
            .ToList();

        foreach (var label in new[] { ShiftLabel.Night, ShiftLabel.Morning, ShiftLabel.Evening })
        {
            foreach (var date in dates)
            {
                foreach (var shiftReq in constraints.ShiftRequirements.Where(s => s.ShiftLabel == label))
                {
                    foreach (var specialtyReq in shiftReq.SpecialtyRequirements)
                    {
                        FillSpecialty(solution, constraints, shiftReq, date, specialtyReq, reserveUnmetOnSlots);
                    }
                }
            }
        }
    }

    /// <summary>
    /// حذف انتساب‌های بیش از ظرفیت روزانه هر شیفت/تخصص (مثلاً دو شب در یک روز وقتی فقط یک صندلی تعریف شده).
    /// </summary>
    public static void StripExcessCoverage(ShiftSolution solution, ShiftConstraints constraints)
    {
        var dates = Enumerable.Range(0, (constraints.EndDate.Date - constraints.StartDate.Date).Days + 1)
            .Select(i => constraints.StartDate.Date.AddDays(i))
            .ToList();

        foreach (var date in dates)
        {
            foreach (var shiftReq in constraints.ShiftRequirements)
            {
                foreach (var specialtyReq in shiftReq.SpecialtyRequirements)
                {
                    StripSpecialtyExcess(solution, constraints, shiftReq, date, specialtyReq);
                }
            }
        }
    }

    public static bool HasAnyOverCapacity(ShiftSolution solution, ShiftConstraints constraints) =>
        GetOverCapacityViolations(solution, constraints).Count > 0;

    public static List<string> GetOverCapacityViolations(ShiftSolution solution, ShiftConstraints constraints)
    {
        var violations = new List<string>();

        var dates = Enumerable.Range(0, (constraints.EndDate.Date - constraints.StartDate.Date).Days + 1)
            .Select(i => constraints.StartDate.Date.AddDays(i))
            .ToList();

        foreach (var date in dates)
        {
            foreach (var shiftReq in constraints.ShiftRequirements)
            {
                foreach (var specialtyReq in shiftReq.SpecialtyRequirements)
                {
                    var day = specialtyReq.ForDay(constraints.IsHoliday(date));
                    var regular = GetSpecialtyAssignments(
                        solution, constraints, shiftReq, date, specialtyReq.SpecialtyId, isOnCall: false);
                    var onCall = GetSpecialtyAssignments(
                        solution, constraints, shiftReq, date, specialtyReq.SpecialtyId, isOnCall: true);

                    var approvedRegularCount = regular.Count(a =>
                    {
                        var u = constraints.UserConstraints.FirstOrDefault(x => x.UserId == a.UserId);
                        return u != null && ApprovedRequestGuard.IsApprovedRequiredSlot(u, a.Date, a.ShiftLabel, a.ShiftId);
                    });
                    var effectiveRequiredTotal = Math.Max(day.RequiredTotalCount, approvedRegularCount);

                    if (regular.Count > effectiveRequiredTotal)
                    {
                        violations.Add(
                            $"Over capacity on {date:yyyy-MM-dd} shift {shiftReq.ShiftLabel} specialty {specialtyReq.SpecialtyId}: " +
                            $"{regular.Count}/{effectiveRequiredTotal} regular.");
                    }

                    if (onCall.Count > day.OnCallTotalCount)
                    {
                        violations.Add(
                            $"Over capacity on {date:yyyy-MM-dd} shift {shiftReq.ShiftLabel} specialty {specialtyReq.SpecialtyId}: " +
                            $"{onCall.Count}/{day.OnCallTotalCount} on-call.");
                    }
                }
            }
        }

        return violations;
    }

    public static List<string> GetUnderCapacityViolations(ShiftSolution solution, ShiftConstraints constraints)
    {
        var violations = new List<string>();

        var dates = Enumerable.Range(0, (constraints.EndDate.Date - constraints.StartDate.Date).Days + 1)
            .Select(i => constraints.StartDate.Date.AddDays(i))
            .ToList();

        foreach (var date in dates)
        {
            foreach (var shiftReq in constraints.ShiftRequirements)
            {
                foreach (var specialtyReq in shiftReq.SpecialtyRequirements)
                {
                    var day = specialtyReq.ForDay(constraints.IsHoliday(date));
                    if (day.RequiredTotalCount <= 0) continue;

                    var regular = GetSpecialtyAssignments(
                        solution, constraints, shiftReq, date, specialtyReq.SpecialtyId, isOnCall: false);

                    if (regular.Count < day.RequiredTotalCount)
                    {
                        violations.Add(
                            $"ظرفیت تکمیل نشده در تاریخ {date:yyyy/MM/dd} شیفت {shiftReq.ShiftLabel} تخصص {specialtyReq.SpecialtyId}: " +
                            $"{regular.Count} نفر از {day.RequiredTotalCount} نفر تخصیص داده شده است.");
                    }
                }
            }
        }

        return violations;
    }

    /// <summary>
    /// فاز جبران نهایی: تضمین پر شدن ۱۰۰٪ جای خالی تمام شیفت‌های ماه.
    /// </summary>
    public static void ForceFillAllMissingCoverage(ShiftSolution solution, ShiftConstraints constraints)
    {
        for (var pass = 0; pass < 5; pass++)
        {
            FillMissingCoverage(solution, constraints, reserveUnmetOnSlots: false);
            if (GetUnderCapacityViolations(solution, constraints).Count == 0)
            {
                return;
            }
        }

        // در صورت وجود کسری قطعی ناشی از کمبود فیزیکی نیرو (مانند مرخصی همزمان چند پرسنل)،
        // از شیفت لانگ استاندارد (صبح+عصر) فقط در صورتی که سقف روزانه حداقل ۲ باشد استفاده می‌شود.
        var maxDaily = constraints.HardRules.EnforceMaxShiftsPerDay
            ? Math.Max(1, constraints.GlobalConstraints.MaxShiftsPerDay)
            : 2;
        if (maxDaily >= 2)
        {
            EmergencyFillMissingCoverageWithDoubleShift(solution, constraints);
        }
    }

    /// <summary>
    /// تکمیل اضطراری کسری ظرفیت با شیفت لانگ (صبح و عصر متوالی).
    /// فقط در شرایط بحران کمبود فیزیکی نیرو (مانند مرخصی همزمان پرسنل) و در صورت مجاز بودن سقف روزانه (>= 2) اجرا می‌شود.
    /// </summary>
    private static void EmergencyFillMissingCoverageWithDoubleShift(
        ShiftSolution solution,
        ShiftConstraints constraints)
    {
        var maxPerDay = constraints.HardRules.EnforceMaxShiftsPerDay
            ? Math.Max(1, constraints.GlobalConstraints.MaxShiftsPerDay)
            : 2;

        if (maxPerDay < 2)
        {
            return;
        }

        var underViolations = GetUnderCapacityViolations(solution, constraints);
        if (underViolations.Count == 0)
        {
            return;
        }

        var dates = Enumerable.Range(0, (constraints.EndDate.Date - constraints.StartDate.Date).Days + 1)
            .Select(i => constraints.StartDate.Date.AddDays(i))
            .ToList();

        foreach (var date in dates)
        {
            foreach (var shiftReq in constraints.ShiftRequirements)
            {
                // فقط شیفت‌های صبح یا عصر مجاز به ترکیب روزانه (لانگ) هستند؛ عصر+شب اکیداً ممنوع است.
                if (shiftReq.ShiftLabel != ShiftLabel.Morning && shiftReq.ShiftLabel != ShiftLabel.Evening)
                {
                    continue;
                }

                var complementaryLabel = shiftReq.ShiftLabel == ShiftLabel.Evening
                    ? ShiftLabel.Morning
                    : ShiftLabel.Evening;

                foreach (var specialtyReq in shiftReq.SpecialtyRequirements)
                {
                    var day = specialtyReq.ForDay(constraints.IsHoliday(date));
                    if (day.RequiredTotalCount <= 0) continue;

                    var regular = GetSpecialtyAssignments(
                        solution, constraints, shiftReq, date, specialtyReq.SpecialtyId, isOnCall: false);

                    var missing = day.RequiredTotalCount - regular.Count;
                    if (missing <= 0) continue;

                    var requiresManager = ShiftManagerRules.RequiresAnyManager(shiftReq);
                    var (reqTotal, reqL1) = ShiftManagerRules.GetRequirement(shiftReq);

                    // کاندیداها: پرسنلی که امروز در شیفت مکمل (صبح/عصر) حضور دارند و مجاز به لانگ هستند
                    var candidates = constraints.UserConstraints
                        .Where(u => u.IsActive && u.SpecialtyId == specialtyReq.SpecialtyId)
                        .Where(u => u.ShiftType != ShiftTypes.FixedShift)
                        .Where(ShiftEligibilityResolver.SupportsMorningEveningCombo)
                        .Where(u => ShiftEligibilityResolver.HasInherentPermission(u, shiftReq.ShiftLabel))
                        .Where(u => !u.UnavailableDates.Any(d => d.Date == date.Date))
                        .Where(u => !u.UnavailableShiftSlots.Any(s => s.Date.Date == date.Date && s.ShiftLabel == shiftReq.ShiftLabel))
                        .Where(u => !solution.HasAssignment(u.UserId, shiftReq.ShiftId, date))
                        .Where(u =>
                        {
                            var existing = solution.GetUserAssignments(u.UserId, date).Select(a => a.ShiftLabel).ToList();
                            if (!existing.Contains(complementaryLabel))
                            {
                                return false;
                            }

                            // بررسی ترکیب مجاز روزانه بر اساس سقف معتبر دپارتمان
                            if (!DailyAssignmentRules.CanAddShift(existing, shiftReq.ShiftLabel, maxShiftsPerDay: maxPerDay, forbidDuplicateLabels: true))
                            {
                                return false;
                            }

                            // بررسی صلاحیت انتساب شیفت با در نظر گرفتن مجوزهای روزانه
                            if (!ShiftEligibilityResolver.IsAssignmentAllowed(
                                    u,
                                    existing,
                                    shiftReq.ShiftLabel,
                                    maxShiftsPerDay: maxPerDay,
                                    forbidDuplicateLabels: true,
                                    date: date))
                            {
                                return false;
                            }

                            // عدم تداخل با استراحت‌های شیفت مجاور (شب روز قبل یا صبح روز بعد)
                            if (AdjacentShiftRestRules.WouldConflict(solution.GetUserAllAssignments(u.UserId), date, shiftReq.ShiftLabel, constraints))
                            {
                                return false;
                            }

                            return true;
                        })
                        .OrderBy(u =>
                        {
                            if (requiresManager)
                            {
                                var currentAssignees = solution.GetShiftAssignments(shiftReq.ShiftId, date)
                                    .Where(a => !a.IsOnCall)
                                    .Select(a => constraints.UserConstraints.FirstOrDefault(x => x.UserId == a.UserId))
                                    .Where(x => x != null)
                                    .Cast<UserConstraint>()
                                    .ToList();
                                if (currentAssignees.Count(ShiftManagerRules.IsLevel1) < reqL1 && ShiftManagerRules.IsLevel1(u))
                                    return -20000;
                                if (currentAssignees.Count(ShiftManagerRules.IsManager) < reqTotal && ShiftManagerRules.IsManager(u))
                                    return -10000;
                            }
                            return 0;
                        })
                        .ThenBy(u => u.OvertimeConsent ? 0 : 1) // اولویت ۱: داشتن رضایت به اضافه کار
                        .ThenBy(u => solution.GetUserAssignments(u.UserId, date.AddDays(1)).Any(a => !a.IsOnCall) ? 1 : 0) // اولویت ۲: داشتن استراحت در روز بعد
                        .ThenBy(u => solution.GetUserAllAssignments(u.UserId).Count)
                        .ToList();

                    foreach (var user in candidates)
                    {
                        if (missing <= 0) break;

                        solution.AddAssignment(user.UserId, shiftReq.ShiftId, date, shiftReq.ShiftLabel, isOnCall: false);
                        missing--;
                    }
                }
            }
        }
    }

    private static void StripSpecialtyExcess(
        ShiftSolution solution,
        ShiftConstraints constraints,
        ShiftRequirement shiftReq,
        DateTime date,
        SpecialtyRequirement specialtyReq)
    {
        var day = specialtyReq.ForDay(constraints.IsHoliday(date));
        var regular = GetSpecialtyAssignments(
            solution, constraints, shiftReq, date, specialtyReq.SpecialtyId, isOnCall: false);
        var approvedRegularCount = regular.Count(a =>
        {
            var u = constraints.UserConstraints.FirstOrDefault(x => x.UserId == a.UserId);
            return u != null && ApprovedRequestGuard.IsApprovedRequiredSlot(u, a.Date, a.ShiftLabel, a.ShiftId);
        });
        var effectiveRequiredTotal = Math.Max(day.RequiredTotalCount, approvedRegularCount);

        StripExcessOfType(
            solution, constraints, shiftReq, date, specialtyReq, effectiveRequiredTotal, isOnCall: false);
        StripExcessOfType(
            solution, constraints, shiftReq, date, specialtyReq, day.OnCallTotalCount, isOnCall: true);
    }

    private static void StripExcessOfType(
        ShiftSolution solution,
        ShiftConstraints constraints,
        ShiftRequirement shiftReq,
        DateTime date,
        SpecialtyRequirement specialtyReq,
        int maxAllowed,
        bool isOnCall)
    {
        for (var pass = 0; pass < 10; pass++)
        {
            var assignments = GetSpecialtyAssignments(
                solution, constraints, shiftReq, date, specialtyReq.SpecialtyId, isOnCall);
            if (assignments.Count <= maxAllowed)
            {
                break;
            }

            var excess = assignments.Count - maxAllowed;
            var unprot = RankForRemoval(solution, constraints, shiftReq, date, assignments)
                .Where(a => !IsProtectedAssignment(constraints, solution, a))
                .ToList();

            if (unprot.Count == 0)
            {
                break;
            }

            // اولویت حذف با انتساب‌هایی است که ترکیب مسئول شیفت را نقض نکنند
            var removable = unprot
                .Where(a => !WouldBreakManagerMix(solution, constraints, shiftReq, date, a))
                .Take(excess)
                .ToList();

            if (removable.Count < excess)
            {
                var remainingNeeded = excess - removable.Count;
                var additional = unprot
                    .Where(a => !removable.Contains(a))
                    .Take(remainingNeeded)
                    .ToList();
                removable.AddRange(additional);
            }

            if (removable.Count == 0)
            {
                break;
            }

            foreach (var assignment in removable)
            {
                solution.UnlockSkeletonAssignment(assignment.UserId, assignment.ShiftId, assignment.Date);
                solution.RemoveAssignment(assignment.UserId, assignment.ShiftId, assignment.Date, force: true);
            }
        }
    }

    private static bool WouldBreakManagerMix(
        ShiftSolution solution,
        ShiftConstraints constraints,
        ShiftRequirement shiftReq,
        DateTime date,
        SaShiftAssignment assignment)
    {
        if (assignment.IsOnCall || !ShiftManagerRules.RequiresAnyManager(shiftReq))
        {
            return false;
        }

        var remainingAssignees = solution.GetShiftAssignments(shiftReq.ShiftId, date)
            .Where(a => !a.IsOnCall && a.UserId != assignment.UserId)
            .Select(a => constraints.UserConstraints.FirstOrDefault(u => u.UserId == a.UserId))
            .Where(u => u != null)
            .Cast<UserConstraint>()
            .ToList();

        var (requiredTotal, minLevel1) = ShiftManagerRules.GetRequirement(shiftReq);
        return !ShiftManagerRules.IsSatisfied(remainingAssignees, requiredTotal, minLevel1);
    }

    private static List<SaShiftAssignment> GetSpecialtyAssignments(
        ShiftSolution solution,
        ShiftConstraints constraints,
        ShiftRequirement shiftReq,
        DateTime date,
        int specialtyId,
        bool isOnCall) =>
        solution.GetShiftAssignments(shiftReq.ShiftId, date)
            .Where(a => a.IsOnCall == isOnCall)
            .Where(a => constraints.UserConstraints.FirstOrDefault(u => u.UserId == a.UserId)?.SpecialtyId == specialtyId)
            .ToList();

    private static IEnumerable<SaShiftAssignment> RankForRemoval(
        ShiftSolution solution,
        ShiftConstraints constraints,
        ShiftRequirement shiftReq,
        DateTime date,
        IReadOnlyList<SaShiftAssignment> assignments)
    {
        return assignments
            .Select(a =>
            {
                var user = constraints.UserConstraints.FirstOrDefault(u => u.UserId == a.UserId);
                var labelCount = user == null
                    ? 0
                    : solution.GetUserAllAssignments(user.UserId)
                        .Count(x => x.ShiftLabel == a.ShiftLabel && !x.IsOnCall);
                var nightSurplus = 0;
                if (user != null &&
                    a.ShiftLabel == ShiftLabel.Night)
                {
                    var maxAllowed = NightQuotaEligibility.GetMaxAllowedTotal(user);
                    if (maxAllowed != int.MaxValue)
                    {
                        nightSurplus = labelCount - maxAllowed;
                    }
                }

                var dayShiftSurplus = 0;
                if (user != null &&
                    (a.ShiftLabel == ShiftLabel.Morning || a.ShiftLabel == ShiftLabel.Evening))
                {
                    var maxAllowed = DayShiftQuotaEligibility.GetMaxAllowedTotal(user, a.ShiftLabel);
                    if (maxAllowed != int.MaxValue)
                    {
                        dayShiftSurplus = labelCount - maxAllowed;
                    }
                }

                var otSurplus = 0.0;
                var nonConsentOt = false;
                if (user != null && user.IncludedInProductivityPlan && user.ProductivityRequiredHours.HasValue)
                {
                    var worked = OvertimeBalanceGuard.CalculateHours(solution, user, constraints);
                    var ot = worked - (double)user.ProductivityRequiredHours.Value;
                    if (!user.OvertimeConsent && ot > 0)
                    {
                        nonConsentOt = true;
                    }
                    otSurplus = Math.Max(0.0, ot);
                }

                var breaksMix = WouldBreakManagerMix(solution, constraints, shiftReq, date, a);

                return (Assignment: a, BreaksMix: breaksMix, NonConsentOt: nonConsentOt, OtSurplus: otSurplus, NightSurplus: nightSurplus, DayShiftSurplus: dayShiftSurplus, LabelCount: labelCount, Protected: IsProtectedAssignment(constraints, solution, a));
            })
            .OrderBy(x => x.BreaksMix ? 1 : 0)
            .ThenBy(x => x.Protected ? 1 : 0)
            .ThenByDescending(x => x.NonConsentOt ? 1 : 0)
            .ThenByDescending(x => x.OtSurplus)
            .ThenByDescending(x => x.DayShiftSurplus)
            .ThenByDescending(x => x.NightSurplus)
            .ThenByDescending(x => x.LabelCount)
            .ThenByDescending(x => x.Assignment.Date)
            .Select(x => x.Assignment);
    }

    private static bool IsProtectedAssignment(
        ShiftConstraints constraints,
        ShiftSolution solution,
        SaShiftAssignment assignment)
    {

        var user = constraints.UserConstraints.FirstOrDefault(u => u.UserId == assignment.UserId);
        if (user == null)
        {
            return false;
        }

        if (ApprovedRequestGuard.IsApprovedRequiredSlot(
            user, assignment.Date, assignment.ShiftLabel, assignment.ShiftId))
        {
            return true;
        }

        if (assignment.ShiftLabel == ShiftLabel.Night)
        {
            if (user.ExactNightShiftCount.HasValue)
            {
                var userNights = solution.GetUserAllAssignments(user.UserId)
                    .Count(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall);
                if (userNights <= user.ExactNightShiftCount.Value)
                {
                    return true;
                }
            }

            if (user.ExactHolidayWeekendNightShiftCount.HasValue && constraints.IsHolidayWeekendNight(assignment.Date))
            {
                var userHolNights = solution.GetUserAllAssignments(user.UserId)
                    .Count(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall && constraints.IsHolidayWeekendNight(a.Date));
                if (userHolNights <= user.ExactHolidayWeekendNightShiftCount.Value)
                {
                    return true;
                }
            }
        }
        else if (assignment.ShiftLabel == ShiftLabel.Morning && user.ExactMorningShiftCount.HasValue)
        {
            var userMornings = solution.GetUserAllAssignments(user.UserId)
                .Count(a => a.ShiftLabel == ShiftLabel.Morning && !a.IsOnCall);
            if (userMornings <= user.ExactMorningShiftCount.Value)
            {
                return true;
            }
        }
        else if (assignment.ShiftLabel == ShiftLabel.Evening && user.ExactEveningShiftCount.HasValue)
        {
            var userEvenings = solution.GetUserAllAssignments(user.UserId)
                .Count(a => a.ShiftLabel == ShiftLabel.Evening && !a.IsOnCall);
            if (userEvenings <= user.ExactEveningShiftCount.Value)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// اگر کاربری درخواست ON برای این روز/شیفت دارد ولی هنوز انتساب نگرفته، Coverage خودکار جای او را پر نکند.
    /// </summary>
    private static bool HasUnmetRequiredSlotForShift(
        ShiftSolution solution,
        ShiftConstraints constraints,
        ShiftRequirement shiftReq,
        DateTime date,
        SpecialtyRequirement specialtyReq) =>
        constraints.UserConstraints
            .Where(u => u.IsActive && u.SpecialtyId == specialtyReq.SpecialtyId)
            .Any(u => u.RequiredShiftSlots.Any(s =>
                s.Date.Date == date.Date &&
                s.ShiftLabel == shiftReq.ShiftLabel &&
                (!s.ShiftId.HasValue || s.ShiftId.Value == shiftReq.ShiftId) &&
                !solution.GetShiftAssignments(shiftReq.ShiftId, date)
                    .Any(a => a.UserId == u.UserId && !a.IsOnCall)));

    private static void FillSpecialty(
        ShiftSolution solution,
        ShiftConstraints constraints,
        ShiftRequirement shiftReq,
        DateTime date,
        SpecialtyRequirement specialtyReq,
        bool reserveUnmetOnSlots)
    {
        var day = specialtyReq.ForDay(constraints.IsHoliday(date));
        var needed = day.RequiredTotalCount;
        if (needed <= 0)
        {
            return;
        }

        if (reserveUnmetOnSlots
            && HasUnmetRequiredSlotForShift(solution, constraints, shiftReq, date, specialtyReq))
        {
            return;
        }

        var current = solution.GetShiftAssignments(shiftReq.ShiftId, date)
            .Count(a => !a.IsOnCall &&
                        constraints.UserConstraints.FirstOrDefault(u => u.UserId == a.UserId)?.SpecialtyId
                        == specialtyReq.SpecialtyId);

        var missing = needed - current;
        if (missing <= 0)
        {
            return;
        }

        var candidates = constraints.UserConstraints
            .Where(u => u.IsActive && u.SpecialtyId == specialtyReq.SpecialtyId)
            .Where(u => ShiftEligibilityResolver.MayTakeLabelOnDate(u, shiftReq.ShiftLabel, date))
            .Where(u => IsEligibleForCoverageFill(solution, constraints, u, shiftReq.ShiftLabel, date))
            .Where(u => !u.UnavailableDates.Any(d => d.Date == date.Date))
            .Where(u => !u.UnavailableShiftSlots.Any(s => s.Date.Date == date.Date && s.ShiftLabel == shiftReq.ShiftLabel))
            .Where(u => !solution.HasAssignment(u.UserId, shiftReq.ShiftId, date))
            .Where(u => CanAcceptShift(solution, constraints, u, date, shiftReq.ShiftLabel))
            .OrderBy(u => CoveragePriority(solution, constraints, u, date, shiftReq.ShiftLabel))
            .ThenBy(u => solution.GetUserAllAssignments(u.UserId).Count)
            .ToList();

        foreach (var user in candidates)
        {
            if (missing <= 0)
            {
                break;
            }

            // ممکن است صبح/عصر با قوانین روزانه تداخل داشته باشد — دوباره چک
            if (!CanAcceptShift(solution, constraints, user, date, shiftReq.ShiftLabel))
            {
                continue;
            }

            solution.AddAssignment(user.UserId, shiftReq.ShiftId, date, shiftReq.ShiftLabel, isOnCall: false);
            missing--;
        }

        if (missing > 0)
        {
            TryFillByRelievingAdjacentWorkDay(solution, constraints, shiftReq, date, specialtyReq, ref missing);
        }
    }

    private static void TryFillByRelievingAdjacentWorkDay(
        ShiftSolution solution,
        ShiftConstraints constraints,
        ShiftRequirement shiftReq,
        DateTime date,
        SpecialtyRequirement specialtyReq,
        ref int missing)
    {
        if (missing <= 0) return;

        var potentialUsers = constraints.UserConstraints
            .Where(u => u.IsActive && u.SpecialtyId == specialtyReq.SpecialtyId)
            .Where(u => ShiftEligibilityResolver.MayTakeLabelOnDate(u, shiftReq.ShiftLabel, date))
            .Where(u => IsEligibleForCoverageFill(solution, constraints, u, shiftReq.ShiftLabel, date))
            .Where(u => !u.UnavailableDates.Any(d => d.Date == date.Date))
            .Where(u => !u.UnavailableShiftSlots.Any(s => s.Date.Date == date.Date && s.ShiftLabel == shiftReq.ShiftLabel))
            .Where(u => !solution.HasAssignment(u.UserId, shiftReq.ShiftId, date))
            .Where(u =>
            {
                var existing = solution.GetUserAssignments(u.UserId, date).Select(a => a.ShiftLabel);
                var maxPerDay = constraints.HardRules.EnforceMaxShiftsPerDay
                    ? Math.Max(1, constraints.GlobalConstraints.MaxShiftsPerDay)
                    : 2;
                if (!DailyAssignmentRules.CanAddShift(existing, shiftReq.ShiftLabel, maxPerDay, constraints.HardRules.ForbidDuplicateDailyAssignments))
                    return false;
                return true;
            })
            .ToList();

        foreach (var u in potentialUsers)
        {
            if (missing <= 0) break;

            var adjacentDates = new[] { date.AddDays(-1), date.AddDays(1), date.AddDays(-2), date.AddDays(2) };
            foreach (var adjDate in adjacentDates)
            {
                var uAsgs = solution.GetUserAssignments(u.UserId, adjDate).Where(a => !a.IsOnCall).ToList();
                foreach (var asg in uAsgs)
                {
                    if (ApprovedRequestGuard.IsApprovedRequiredSlot(u, asg.Date, asg.ShiftLabel, asg.ShiftId)
                        || u.RequiredPresenceDates.Any(d => d.Date == asg.Date.Date))
                    {
                        continue;
                    }

                    var targetShiftReq = constraints.ShiftRequirements.FirstOrDefault(s => s.ShiftId == asg.ShiftId);
                    if (targetShiftReq == null) continue;

                    var donors = constraints.UserConstraints
                        .Where(v => v.UserId != u.UserId && v.IsActive && v.SpecialtyId == specialtyReq.SpecialtyId)
                        .Where(v => ShiftEligibilityResolver.MayTakeLabelOnDate(v, asg.ShiftLabel, asg.Date))
                        .Where(v => IsEligibleForCoverageFill(solution, constraints, v, asg.ShiftLabel, asg.Date))
                        .Where(v => !v.UnavailableDates.Any(d => d.Date == asg.Date.Date))
                        .Where(v => !v.UnavailableShiftSlots.Any(s => s.Date.Date == asg.Date.Date && s.ShiftLabel == asg.ShiftLabel))
                        .Where(v => !solution.HasAssignment(v.UserId, asg.ShiftId, asg.Date))
                        .Where(v => CanAcceptShift(solution, constraints, v, asg.Date, asg.ShiftLabel))
                        .ToList();

                    foreach (var v in donors)
                    {
                        if (ShiftManagerRules.RequiresAnyManager(targetShiftReq))
                        {
                            var (reqTotal, reqL1) = ShiftManagerRules.GetRequirement(targetShiftReq);
                            var currentAssignees = solution.GetShiftAssignments(asg.ShiftId, asg.Date)
                                .Where(a => !a.IsOnCall && a.UserId != u.UserId)
                                .Select(a => constraints.UserConstraints.FirstOrDefault(x => x.UserId == a.UserId))
                                .Where(x => x != null)
                                .ToList();
                            currentAssignees.Add(v);
                            if (currentAssignees.Count(ShiftManagerRules.IsLevel1) < reqL1
                                || currentAssignees.Count(ShiftManagerRules.IsManager) < reqTotal)
                            {
                                continue;
                            }
                        }

                        var backup = solution.Clone();
                        var isSkel = solution.IsLockedSkeleton(u.UserId, asg.ShiftId, asg.Date);
                        if (isSkel)
                        {
                            solution.UnlockSkeletonAssignment(u.UserId, asg.ShiftId, asg.Date);
                        }
                        solution.RemoveAssignment(u.UserId, asg.ShiftId, asg.Date, force: true);
                        solution.AddAssignment(v.UserId, asg.ShiftId, asg.Date, asg.ShiftLabel, isOnCall: false, isSkeleton: isSkel);
                        if (isSkel)
                        {
                            SkeletonAssignmentGuard.LockSlotManagerAssignments(solution, constraints, targetShiftReq, asg.Date);
                        }

                        if (CanAcceptShift(solution, constraints, u, date, shiftReq.ShiftLabel))
                        {
                            solution.AddAssignment(u.UserId, shiftReq.ShiftId, date, shiftReq.ShiftLabel, isOnCall: false);
                            missing--;
                            return;
                        }

                        solution.Assignments.Clear();
                        foreach (var a in backup.Assignments.Values)
                        {
                            solution.AddAssignment(a.UserId, a.ShiftId, a.Date, a.ShiftLabel, a.IsOnCall, a.IsSkeleton);
                        }
                        solution.LockedSkeletonAssignments.Clear();
                        foreach (var l in backup.LockedSkeletonAssignments)
                        {
                            solution.LockedSkeletonAssignments.Add(l);
                        }
                        solution.SyncSkeletonFlagsFromLockSet();
                    }
                }
            }
        }
    }

    private static int CoveragePriority(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        DateTime date,
        ShiftLabel label)
    {
        var score = 0;

        var shiftReq = constraints.ShiftRequirements.FirstOrDefault(s => s.ShiftLabel == label);
        if (shiftReq != null && ShiftManagerRules.RequiresAnyManager(shiftReq))
        {
            var assignees = solution.GetShiftAssignments(shiftReq.ShiftId, date)
                .Where(a => !a.IsOnCall)
                .Select(a => constraints.UserConstraints.FirstOrDefault(u => u.UserId == a.UserId))
                .Where(u => u != null)
                .Cast<UserConstraint>()
                .ToList();
            var (requiredTotal, minLevel1) = ShiftManagerRules.GetRequirement(shiftReq);
            if (!ShiftManagerRules.IsSatisfied(assignees, requiredTotal, minLevel1))
            {
                if (ShiftManagerRules.IsLevel1(user))
                {
                    score -= 20_000;
                }
                else if (ShiftManagerRules.IsManager(user))
                {
                    score -= 8_000;
                }
            }
        }

        // کسری سهمیه شب اولویت مطلق برای شب
        if (label == ShiftLabel.Night)
        {
            var nights = solution.GetUserAllAssignments(user.UserId)
                .Count(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall);
            if (user.ExactNightShiftCount.HasValue && nights < user.ExactNightShiftCount.Value)
            {
                score -= 5000 + (user.ExactNightShiftCount.Value - nights) * 100;
            }

            if (user.ExactHolidayWeekendNightShiftCount.HasValue && constraints.IsHolidayWeekendNight(date))
            {
                var holiday = solution.GetUserAllAssignments(user.UserId)
                    .Count(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall &&
                                constraints.IsHolidayWeekendNight(a.Date));
                if (holiday < user.ExactHolidayWeekendNightShiftCount.Value)
                {
                    score -= 8000;
                }
            }
        }
        else
        {
            // کسری سهمیه صبح/عصر
            var totalLabel = solution.GetUserAllAssignments(user.UserId)
                .Count(a => a.ShiftLabel == label && !a.IsOnCall);
            var exact = label == ShiftLabel.Morning ? user.ExactMorningShiftCount : user.ExactEveningShiftCount;
            if (exact.HasValue && totalLabel < exact.Value)
            {
                score -= 5000 + (exact.Value - totalLabel) * 100;
            }

            if (constraints.IsHoliday(date))
            {
                var holidayExact = label == ShiftLabel.Morning
                    ? user.ExactHolidayMorningShiftCount
                    : user.ExactHolidayEveningShiftCount;
                if (holidayExact.HasValue)
                {
                    var holidayCount = DayShiftQuotaEligibility.CountHolidayLabel(
                        solution, constraints, user.UserId, label);
                    if (holidayCount < holidayExact.Value)
                    {
                        score -= 8000;
                    }
                }
            }

            // تعادل peer برای صبح/عصر — فقط به‌عنوان اولویت نرم داخل پوشش اجباری
            score += totalLabel * 10;

            // اولویت کسری موظفی و اضافه کاری: کسی که هنوز به موظفی نرسیده زودتر شیفت پوشش بگیرد
            // پرسنلی که OvertimeConsent ندارند و موظفی‌شان پر شده یا پرسنلی که از سقف ۸۰ ساعت گذشته‌اند، اولویت بسیار پایینی دارند
            if (user.IncludedInProductivityPlan && user.ProductivityRequiredHours is > 0)
            {
                var worked = OvertimeBalanceGuard.CalculateHours(solution, user, constraints);
                var req = (double)user.ProductivityRequiredHours.Value;
                var ot = worked - req;

                if (!user.OvertimeConsent && ot >= 0)
                {
                    score += 25_000 + (int)(ot * 100);
                }
                else if (ot >= user.MaxMonthlyOvertimeHours)
                {
                    score += 50_000 + (int)((ot - user.MaxMonthlyOvertimeHours) * 200);
                }
                else
                {
                    score += (int)(ot * 30);
                }
            }

            if (constraints.IsHoliday(date))
            {
                score += HolidayMorningEveningFairnessGuard.CountHolidayLabel(
                    solution, constraints, user.UserId, label) * 40;
            }

            var workDates = solution.GetUserAllAssignments(user.UserId)
                .Where(a => !a.IsOnCall)
                .Select(a => a.Date.Date)
                .ToHashSet();
            if (!workDates.Contains(date.Date))
            {
                var run = 1;
                var c = date.Date.AddDays(-1);
                while (workDates.Contains(c))
                {
                    run++;
                    c = c.AddDays(-1);
                }

                c = date.Date.AddDays(1);
                while (workDates.Contains(c))
                {
                    run++;
                    c = c.AddDays(1);
                }

                score += run * 3;
            }
        }

        if (user.ShiftType == ShiftTypes.FixedShift)
        {
            score -= 100; // فیکس‌ها برای پوشش روزانه مناسب‌اند
        }

        return score;
    }

    internal static bool CanAcceptShift(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        DateTime date,
        ShiftLabel label)
    {
        // بررسی مجوز نوع شیفت با آگاهی از تاریخ:
        // کاربر فقط در صورتی می‌تواند این نوع شیفت را بگیرد که یا مجوز کلی داشته باشد
        // یا درخواست تأییدشده دقیقاً برای همین تاریخ و نوع شیفت داشته باشد.
        if (!ShiftEligibilityResolver.IsAssignmentAllowed(
                user,
                solution.GetUserAssignments(user.UserId, date).Select(a => a.ShiftLabel),
                label,
                constraints.HardRules.EnforceMaxShiftsPerDay
                    ? Math.Max(1, constraints.GlobalConstraints.MaxShiftsPerDay)
                    : 2,
                constraints.HardRules.ForbidDuplicateDailyAssignments,
                date))
        {
            return false;
        }

        // عمداً سقف هفته اینجا اعمال نمی‌شود — پوشش ظرفیت اجباری است؛ سقف روزهای کاری متوالی اعمال می‌شود.
        var existing = solution.GetUserAssignments(user.UserId, date).Select(a => a.ShiftLabel);
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

        if (AdjacentShiftRestRules.WouldConflict(
                solution.GetUserAllAssignments(user.UserId), date, label, constraints))
        {
            return false;
        }

        if (MaxConsecutiveWorkdayRules.WouldExceedMaxConsecutiveWorkdays(
                solution, constraints, user, date))
        {
            return false;
        }

        if (label == ShiftLabel.Night && user.MinDaysBetweenNightShifts > 0)
        {
            foreach (var n in solution.GetUserAllAssignments(user.UserId)
                         .Where(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall))
            {
                if (Math.Abs((date.Date - n.Date.Date).Days) <= user.MinDaysBetweenNightShifts)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool IsEligibleForCoverageFill(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        ShiftLabel label,
        DateTime date)
    {
        return label switch
        {
            ShiftLabel.Night => NightQuotaEligibility.CanAssignInCoverageFill(
                solution, constraints, user, date)
                && ComboShiftQuotaEligibility.CanAssignInCoverageFill(
                    solution, constraints, user, ShiftLabel.Night, date),
            ShiftLabel.Morning or ShiftLabel.Evening => DayShiftQuotaEligibility.CanAssignInCoverageFill(
                solution, constraints, user, label, date)
                && ComboShiftQuotaEligibility.CanAssignInCoverageFill(
                    solution, constraints, user, label, date),
            _ => ComboShiftQuotaEligibility.CanAssignInCoverageFill(
                solution, constraints, user, label, date)
        };
    }
}
