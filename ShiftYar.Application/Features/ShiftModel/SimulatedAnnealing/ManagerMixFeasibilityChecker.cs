using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;
using static ShiftYar.Domain.Enums.UserModel.UserEnums;

namespace ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;

/// <summary>
/// بررسی سریع امکان‌پذیری ترکیب مسئول سطح‌۱ قبل از SA — با شبیه‌سازی حریصانه لایه L1.
/// </summary>
public static class ManagerMixFeasibilityChecker
{
    public static void ValidateOrThrow(ShiftConstraints constraints)
    {
        var reason = TryGetInfeasibilityReason(constraints);
        if (reason != null)
        {
            throw new InvalidOperationException(reason);
        }
    }

    public static string? TryGetInfeasibilityReason(ShiftConstraints constraints)
    {
        if (!constraints.ShiftRequirements.Any(ShiftManagerRules.RequiresAnyManager))
        {
            return null;
        }

        var dates = GetDateRange(constraints).ToList();
        var managerShifts = constraints.ShiftRequirements
            .Where(ShiftManagerRules.RequiresAnyManager)
            .OrderBy(s => s.ShiftLabel == ShiftLabel.Night ? 0
                : s.ShiftLabel == ShiftLabel.Evening ? 1 : 2)
            .ThenBy(s => s.ShiftId)
            .ToList();

        var l1Demand = 0;
        foreach (var shiftReq in managerShifts)
        {
            var (_, minL1) = ShiftManagerRules.GetRequirement(shiftReq);
            if (minL1 <= 0)
            {
                continue;
            }

            foreach (var date in dates)
            {
                if (SlotHasCoverageDemand(constraints, shiftReq, date))
                {
                    l1Demand += minL1;
                }
            }
        }

        var nightEligibleL1 = CountEligibleLevel1(constraints, ShiftLabel.Night);
        var eveningEligibleL1 = CountEligibleLevel1(constraints, ShiftLabel.Evening);

        if (l1Demand == 0)
        {
            return null;
        }

        var simulation = new ShiftSolution();
        SeedApprovedOnAssignments(simulation, constraints);

        foreach (var shiftReq in managerShifts.Where(s => s.ShiftLabel == ShiftLabel.Night))
        {
            var (_, minL1) = ShiftManagerRules.GetRequirement(shiftReq);
            if (minL1 <= 0)
            {
                continue;
            }

            foreach (var date in dates)
            {
                if (!SlotHasCoverageDemand(constraints, shiftReq, date))
                {
                    continue;
                }

                if (!TryPlaceLevel1Managers(simulation, constraints, shiftReq, date, minL1))
                {
                    return BuildPersianError(
                        constraints,
                        l1Demand,
                        nightEligibleL1,
                        eveningEligibleL1,
                        $"تخصیص مسئول سطح‌۱ برای شیفت شب در {date:yyyy-MM-dd} با قوانین استراحت و روزهای متوالی ممکن نیست.");
                }
            }
        }

        foreach (var shiftReq in managerShifts.Where(s => s.ShiftLabel == ShiftLabel.Evening))
        {
            var (_, minL1) = ShiftManagerRules.GetRequirement(shiftReq);
            if (minL1 <= 0)
            {
                continue;
            }

            foreach (var date in dates)
            {
                if (!SlotHasCoverageDemand(constraints, shiftReq, date))
                {
                    continue;
                }

                if (!TryPlaceLevel1Managers(simulation, constraints, shiftReq, date, minL1))
                {
                    return BuildPersianError(
                        constraints,
                        l1Demand,
                        nightEligibleL1,
                        eveningEligibleL1,
                        $"تخصیص مسئول سطح‌۱ برای شیفت عصر در {date:yyyy-MM-dd} با قوانین استراحت و روزهای متوالی ممکن نیست.");
                }
            }
        }

        return null;
    }

    private static string BuildPersianError(
        ShiftConstraints constraints,
        int l1Demand,
        int nightEligibleL1,
        int eveningEligibleL1,
        string detail)
    {
        return
            "ترکیب مسئول شیفت (سطح‌۱) برای این ماه با منابع فعلی امکان‌پذیر نیست.\n" +
            $"{detail}\n" +
            $"تقاضای سطح‌۱ در ماه: {l1Demand} انتساب (عصر+شب).\n" +
            $"مسئولان سطح‌۱ واجد شرایط شب: {nightEligibleL1} نفر؛ عصر: {eveningEligibleL1} نفر.\n" +
            "راه‌حل‌های ممکن: افزودن مسئول سطح‌۱ واجد شرایط شب/عصر، کاهش ManagerMinLevel1Count، " +
            "یا اصلاح درخواست‌های ON/سهمیه/سقف روزهای متوالی.";
    }

    private static int CountEligibleLevel1(ShiftConstraints constraints, ShiftLabel label) =>
        constraints.UserConstraints.Count(u =>
            u.IsActive
            && ShiftManagerRules.IsLevel1(u)
            && ShiftEligibilityResolver.MayEverTakeLabel(u, label));

    private static IEnumerable<DateTime> GetDateRange(ShiftConstraints constraints)
    {
        for (var date = constraints.StartDate.Date; date <= constraints.EndDate.Date; date = date.AddDays(1))
        {
            yield return date;
        }
    }

    private static bool SlotHasCoverageDemand(ShiftConstraints constraints, ShiftRequirement shiftReq, DateTime date)
    {
        var holiday = constraints.IsHoliday(date);
        return shiftReq.SpecialtyRequirements.Any(s => s.ForDay(holiday).RequiredTotalCount > 0);
    }

    private static void SeedApprovedOnAssignments(ShiftSolution solution, ShiftConstraints constraints)
    {
        foreach (var user in constraints.UserConstraints)
        {
            foreach (var required in user.RequiredShiftSlots)
            {
                if (required.Date.Date < constraints.StartDate.Date ||
                    required.Date.Date > constraints.EndDate.Date)
                {
                    continue;
                }

                var shiftReq = ResolveShift(constraints, required.ShiftLabel, user.SpecialtyId, required.ShiftId);
                if (shiftReq == null)
                {
                    continue;
                }

                RemoveConflictingDaily(solution, constraints, user, required.Date, shiftReq.ShiftId, required.ShiftLabel);
                if (!solution.HasAssignment(user.UserId, shiftReq.ShiftId, required.Date))
                {
                    solution.AddAssignment(
                        user.UserId,
                        shiftReq.ShiftId,
                        required.Date,
                        required.ShiftLabel,
                        isOnCall: false);
                }
            }
        }
    }

    private static ShiftRequirement? ResolveShift(
        ShiftConstraints constraints,
        ShiftLabel label,
        int specialtyId,
        int? shiftId)
    {
        if (shiftId.HasValue)
        {
            return constraints.ShiftRequirements.FirstOrDefault(s => s.ShiftId == shiftId.Value);
        }

        return constraints.ShiftRequirements
            .Where(s => s.ShiftLabel == label)
            .OrderByDescending(s =>
                s.SpecialtyRequirements.FirstOrDefault(r => r.SpecialtyId == specialtyId)?.RequiredTotalCount ?? 0)
            .FirstOrDefault();
    }

    private static bool TryPlaceLevel1Managers(
        ShiftSolution solution,
        ShiftConstraints constraints,
        ShiftRequirement shiftReq,
        DateTime date,
        int minLevel1)
    {
        var specialtyId = shiftReq.SpecialtyRequirements
            .OrderByDescending(s => s.ForDay(constraints.IsHoliday(date)).RequiredTotalCount)
            .Select(s => s.SpecialtyId)
            .FirstOrDefault();

        var placed = 0;
        for (var attempt = 0; attempt < minLevel1 * 4 && placed < minLevel1; attempt++)
        {
            var candidate = RankLevel1Candidates(solution, constraints, shiftReq, date, specialtyId)
                .FirstOrDefault(u => CanInstallStrict(solution, constraints, u, shiftReq, date));
            if (candidate == null)
            {
                break;
            }

            RemoveConflictingDaily(solution, constraints, candidate, date, shiftReq.ShiftId, shiftReq.ShiftLabel);
            ClearUnprotectedAdjacencyForInstall(solution, constraints, candidate, date, shiftReq.ShiftLabel);
            solution.AddAssignment(candidate.UserId, shiftReq.ShiftId, date, shiftReq.ShiftLabel, isOnCall: false);
            placed++;
        }

        var assignees = solution.GetShiftAssignments(shiftReq.ShiftId, date)
            .Where(a => !a.IsOnCall)
            .Select(a => constraints.UserConstraints.FirstOrDefault(u => u.UserId == a.UserId))
            .Where(u => u != null)
            .Cast<UserConstraint>()
            .ToList();
        var (_, minL1Req) = ShiftManagerRules.GetRequirement(shiftReq);
        return ShiftManagerRules.IsSatisfied(assignees, Math.Max(minLevel1, minL1Req), minL1Req);
    }

    private static IEnumerable<UserConstraint> RankLevel1Candidates(
        ShiftSolution solution,
        ShiftConstraints constraints,
        ShiftRequirement shiftReq,
        DateTime date,
        int specialtyId) =>
        constraints.UserConstraints
            .Where(u => u.IsActive && ShiftManagerRules.IsLevel1(u))
            .Where(u => u.SpecialtyId == specialtyId)
            .Where(u => ShiftEligibilityResolver.MayEverTakeLabel(u, shiftReq.ShiftLabel))
            .Where(u => !HasConflictingApprovedOnDate(constraints, u, date, shiftReq.ShiftLabel))
            .Where(u => !solution.HasAssignment(u.UserId, shiftReq.ShiftId, date))
            .OrderBy(u => CountLabelAssignments(solution, u.UserId, shiftReq.ShiftLabel))
            .ThenBy(u => HasNightAdjacentConflict(solution, constraints, u, date, shiftReq.ShiftLabel) ? 1 : 0)
            .ThenBy(u => u.UserId);

    private static int CountLabelAssignments(ShiftSolution solution, int userId, ShiftLabel label) =>
        solution.GetUserAllAssignments(userId).Count(a => a.ShiftLabel == label && !a.IsOnCall);

    private static bool HasNightAdjacentConflict(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        DateTime date,
        ShiftLabel label) =>
        AdjacentShiftRestRules.WouldConflict(
            solution.GetUserAllAssignments(user.UserId), date, label, constraints);

    private static bool HasConflictingApprovedOnDate(
        ShiftConstraints constraints,
        UserConstraint user,
        DateTime date,
        ShiftLabel installLabel)
    {
        var maxPerDay = constraints.HardRules.EnforceMaxShiftsPerDay
            ? Math.Max(1, constraints.GlobalConstraints.MaxShiftsPerDay)
            : 2;
        return user.RequiredShiftSlots.Any(s =>
            s.Date.Date == date.Date &&
            s.ShiftLabel != installLabel &&
            !DailyAssignmentRules.IsValidDaySet(
                new[] { s.ShiftLabel, installLabel },
                maxPerDay,
                constraints.HardRules.ForbidDuplicateDailyAssignments));
    }

    private static bool CanInstallStrict(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        ShiftRequirement shiftReq,
        DateTime date)
    {
        if (user.UnavailableDates.Any(d => d.Date == date.Date))
        {
            return false;
        }

        if (user.UnavailableShiftSlots.Any(s => s.Date.Date == date.Date && s.ShiftLabel == shiftReq.ShiftLabel))
        {
            return false;
        }

        var maxPerDay = constraints.HardRules.EnforceMaxShiftsPerDay
            ? Math.Max(1, constraints.GlobalConstraints.MaxShiftsPerDay)
            : 2;
        var sameDay = solution.GetUserAssignments(user.UserId, date)
            .Where(a => ApprovedRequestGuard.IsApprovedRequiredSlot(user, a.Date, a.ShiftLabel, a.ShiftId))
            .Select(a => a.ShiftLabel);
        if (!ShiftEligibilityResolver.IsAssignmentAllowed(
                user, sameDay, shiftReq.ShiftLabel, maxPerDay,
                constraints.HardRules.ForbidDuplicateDailyAssignments))
        {
            return false;
        }

        var lockedAssignments = solution.GetUserAllAssignments(user.UserId)
            .Where(a => ApprovedRequestGuard.IsApprovedRequiredSlot(user, a.Date, a.ShiftLabel, a.ShiftId));
        if (AdjacentShiftRestRules.WouldConflict(
                lockedAssignments, date, shiftReq.ShiftLabel, constraints))
        {
            return false;
        }

        if (MaxConsecutiveWorkdayRules.WouldExceedMaxConsecutiveWorkdays(
                solution, constraints, user, date))
        {
            return false;
        }

        if (shiftReq.ShiftLabel == ShiftLabel.Night && user.MinDaysBetweenNightShifts > 0)
        {
            foreach (var n in lockedAssignments.Where(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall))
            {
                if (Math.Abs((date.Date - n.Date.Date).Days) <= user.MinDaysBetweenNightShifts)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static void ClearUnprotectedAdjacencyForInstall(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        DateTime date,
        ShiftLabel label)
    {
        if (label == ShiftLabel.Night)
        {
            if (!constraints.HardRules.AllowNightShiftAfterNightShift)
            {
                ClearLabelOnDate(solution, constraints, user, date.AddDays(-1), ShiftLabel.Night);
            }

            foreach (var assignment in solution.GetUserAssignments(user.UserId, date.AddDays(1)).ToList())
            {
                if (constraints.HardRules.IsForbiddenOnDayAfterNight(assignment.ShiftLabel))
                {
                    TryForceRemove(solution, constraints, user, assignment);
                }
            }

            foreach (var assignment in solution.GetUserAssignments(user.UserId, date)
                         .Where(a => a.ShiftLabel == ShiftLabel.Evening)
                         .ToList())
            {
                TryForceRemove(solution, constraints, user, assignment);
            }
        }
        else if (label == ShiftLabel.Evening && !constraints.HardRules.AllowEveningAfterNightShift)
        {
            ClearLabelOnDate(solution, constraints, user, date.AddDays(-1), ShiftLabel.Night);
        }
    }

    private static void ClearLabelOnDate(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        DateTime date,
        ShiftLabel label)
    {
        foreach (var assignment in solution.GetUserAssignments(user.UserId, date)
                     .Where(a => a.ShiftLabel == label && !a.IsOnCall)
                     .ToList())
        {
            TryForceRemove(solution, constraints, user, assignment);
        }
    }

    private static void TryForceRemove(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        SaShiftAssignment assignment)
    {
        if (ApprovedRequestGuard.IsApprovedRequiredSlot(
                user, assignment.Date, assignment.ShiftLabel, assignment.ShiftId))
        {
            return;
        }

        solution.RemoveAssignment(assignment.UserId, assignment.ShiftId, assignment.Date, force: true);
    }

    private static void RemoveConflictingDaily(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        DateTime date,
        int keepShiftId,
        ShiftLabel keepLabel)
    {
        var maxPerDay = constraints.HardRules.EnforceMaxShiftsPerDay
            ? Math.Max(1, constraints.GlobalConstraints.MaxShiftsPerDay)
            : 2;
        var forbidDup = constraints.HardRules.ForbidDuplicateDailyAssignments;

        foreach (var assignment in solution.GetUserAssignments(user.UserId, date).ToList())
        {
            if (assignment.ShiftId == keepShiftId)
            {
                continue;
            }

            if (ApprovedRequestGuard.IsApprovedRequiredSlot(
                    user, assignment.Date, assignment.ShiftLabel, assignment.ShiftId))
            {
                continue;
            }

            var trial = new[] { assignment.ShiftLabel, keepLabel };
            if (!DailyAssignmentRules.IsValidDaySet(trial, maxPerDay, forbidDup))
            {
                solution.RemoveAssignment(user.UserId, assignment.ShiftId, date, force: true);
            }
        }
    }
}
