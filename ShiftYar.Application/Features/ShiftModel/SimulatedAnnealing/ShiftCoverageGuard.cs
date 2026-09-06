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

                    if (regular.Count > day.RequiredTotalCount)
                    {
                        violations.Add(
                            $"Over capacity on {date:yyyy-MM-dd} shift {shiftReq.ShiftLabel} specialty {specialtyReq.SpecialtyId}: " +
                            $"{regular.Count}/{day.RequiredTotalCount} regular.");
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

    private static void StripSpecialtyExcess(
        ShiftSolution solution,
        ShiftConstraints constraints,
        ShiftRequirement shiftReq,
        DateTime date,
        SpecialtyRequirement specialtyReq)
    {
        var day = specialtyReq.ForDay(constraints.IsHoliday(date));
        StripExcessOfType(
            solution, constraints, shiftReq, date, specialtyReq, day.RequiredTotalCount, isOnCall: false);
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

                var breaksMix = WouldBreakManagerMix(solution, constraints, shiftReq, date, a);

                return (Assignment: a, BreaksMix: breaksMix, NightSurplus: nightSurplus, DayShiftSurplus: dayShiftSurplus, LabelCount: labelCount, Protected: IsProtectedAssignment(constraints, solution, a));
            })
            .OrderBy(x => x.BreaksMix ? 1 : 0)
            .ThenByDescending(x => x.DayShiftSurplus)
            .ThenByDescending(x => x.NightSurplus)
            .ThenByDescending(x => x.LabelCount)
            .ThenBy(x => x.Protected ? 1 : 0)
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
            .Where(u => ShiftEligibilityResolver.MayEverTakeLabel(u, shiftReq.ShiftLabel))
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

            // اولویت کسری موظفی: کسی که هنوز به هدف نرسیده زودتر شیفت پوشش بگیرد
            if (user.IncludedInProductivityPlan && user.ProductivityRequiredHours is > 0)
            {
                var totalShifts = solution.GetUserAllAssignments(user.UserId).Count(a => !a.IsOnCall);
                var approxTargetShifts = Math.Max(1, (int)Math.Ceiling((double)user.ProductivityRequiredHours.Value / 8.0));
                score += (totalShifts - approxTargetShifts) * 35;
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

    private static bool CanAcceptShift(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        DateTime date,
        ShiftLabel label)
    {
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
