using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;
using static ShiftYar.Domain.Enums.UserModel.UserEnums;

namespace ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;

/// <summary>
/// تعادل تعداد شیفت صبح/عصر درون هر کاربر با جابجایی هم‌روز بین دو نفر (پوشش حفظ می‌شود).
/// </summary>
public static class MorningEveningBalanceGuard
{
    private const int MaxAllowedMeSpread = 2;

    public static bool TrySingleSwap(ShiftSolution solution, ShiftConstraints constraints)
    {
        var lookup = ProductivityWorkedHoursCalculator.BuildShiftInfoLookup(constraints.ShiftRequirements);
        var users = constraints.UserConstraints
            .Where(u => u.IsActive && u.ShiftType != ShiftTypes.FixedShift)
            .Where(CanBalanceMorningEvening)
            .ToList();

        foreach (var pair in BuildImbalancedPairs(solution, users))
        {
            if (TrySwapMorningEveningBetweenUsers(solution, constraints, lookup, pair.MHeavy, pair.EHeavy))
            {
                return true;
            }
        }

        return false;
    }

    public static void Enforce(ShiftSolution solution, ShiftConstraints constraints)
    {
        var lookup = ProductivityWorkedHoursCalculator.BuildShiftInfoLookup(constraints.ShiftRequirements);
        var users = constraints.UserConstraints
            .Where(u => u.IsActive && u.ShiftType != ShiftTypes.FixedShift)
            .Where(CanBalanceMorningEvening)
            .ToList();

        if (users.Count < 2)
        {
            return;
        }

        for (var pass = 0; pass < 64; pass++)
        {
            var progressed = false;
            foreach (var pair in BuildImbalancedPairs(solution, users))
            {
                if (TrySwapMorningEveningBetweenUsers(solution, constraints, lookup, pair.MHeavy, pair.EHeavy))
                {
                    progressed = true;
                    break;
                }
            }

            if (!progressed)
            {
                break;
            }
        }
    }

    public static int CountMorning(ShiftSolution solution, int userId) =>
        solution.GetUserAllAssignments(userId).Count(a => a.ShiftLabel == ShiftLabel.Morning && !a.IsOnCall);

    public static int CountEvening(ShiftSolution solution, int userId) =>
        solution.GetUserAllAssignments(userId).Count(a => a.ShiftLabel == ShiftLabel.Evening && !a.IsOnCall);

    public static int GetMorningEveningSpread(ShiftSolution solution, UserConstraint user)
    {
        var morning = CountMorning(solution, user.UserId);
        var evening = CountEvening(solution, user.UserId);
        return Math.Abs(morning - evening);
    }

    private static bool CanBalanceMorningEvening(UserConstraint user) =>
        ShiftEligibilityResolver.IsLabelAllowed(user.AllowedShiftLabels, ShiftLabel.Morning) &&
        ShiftEligibilityResolver.IsLabelAllowed(user.AllowedShiftLabels, ShiftLabel.Evening);

    private static IEnumerable<(UserConstraint MHeavy, UserConstraint EHeavy)> BuildImbalancedPairs(
        ShiftSolution solution,
        List<UserConstraint> users)
    {
        var scored = users
            .Select(u =>
            {
                var morning = CountMorning(solution, u.UserId);
                var evening = CountEvening(solution, u.UserId);
                return (User: u, Morning: morning, Evening: evening, Delta: morning - evening);
            })
            .Where(x => Math.Abs(x.Delta) > MaxAllowedMeSpread)
            .OrderByDescending(x => Math.Abs(x.Delta))
            .ToList();

        var mHeavy = scored.Where(x => x.Delta > MaxAllowedMeSpread).OrderByDescending(x => x.Delta).ToList();
        var eHeavy = scored.Where(x => x.Delta < -MaxAllowedMeSpread).OrderBy(x => x.Delta).ToList();

        foreach (var m in mHeavy)
        {
            foreach (var e in eHeavy)
            {
                if (m.User.UserId != e.User.UserId)
                {
                    yield return (m.User, e.User);
                }
            }
        }
    }

    private static bool TrySwapMorningEveningBetweenUsers(
        ShiftSolution solution,
        ShiftConstraints constraints,
        IReadOnlyDictionary<int, ProductivityWorkedHoursCalculator.ShiftWorkInfo> lookup,
        UserConstraint mHeavyUser,
        UserConstraint eHeavyUser)
    {
        var mAssignments = solution.GetUserAllAssignments(mHeavyUser.UserId)
            .Where(a => a.ShiftLabel == ShiftLabel.Morning && !a.IsOnCall)
            .Where(a => !IsProtected(constraints, mHeavyUser, a))
            .OrderByDescending(a => constraints.IsHoliday(a.Date))
            .ToList();

        foreach (var mAssignment in mAssignments)
        {
            var eAssignments = solution.GetUserAllAssignments(eHeavyUser.UserId)
                .Where(a => a.ShiftLabel == ShiftLabel.Evening && !a.IsOnCall)
                .Where(a => a.Date.Date == mAssignment.Date.Date)
                .Where(a => !IsProtected(constraints, eHeavyUser, a))
                .ToList();

            foreach (var eAssignment in eAssignments)
            {
                if (TrySwapSameDayMorningEvening(
                        solution, constraints, lookup,
                        mHeavyUser, eHeavyUser,
                        mAssignment, eAssignment))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TrySwapSameDayMorningEvening(
        ShiftSolution solution,
        ShiftConstraints constraints,
        IReadOnlyDictionary<int, ProductivityWorkedHoursCalculator.ShiftWorkInfo> lookup,
        UserConstraint mHeavyUser,
        UserConstraint eHeavyUser,
        SaShiftAssignment morningAssignment,
        SaShiftAssignment eveningAssignment)
    {
        if (morningAssignment.Date.Date != eveningAssignment.Date.Date)
        {
            return false;
        }

        if (!CanUserTakeShiftAfterRemoval(
                solution, constraints, lookup, eHeavyUser, morningAssignment, eveningAssignment.ShiftId))
        {
            return false;
        }

        if (!CanUserTakeShiftAfterRemoval(
                solution, constraints, lookup, mHeavyUser, eveningAssignment, morningAssignment.ShiftId))
        {
            return false;
        }

        var beforeSpread = GetMorningEveningSpread(solution, mHeavyUser)
                           + GetMorningEveningSpread(solution, eHeavyUser);

        solution.RemoveAssignment(mHeavyUser.UserId, morningAssignment.ShiftId, morningAssignment.Date);
        solution.RemoveAssignment(eHeavyUser.UserId, eveningAssignment.ShiftId, eveningAssignment.Date);
        solution.AddAssignment(
            eHeavyUser.UserId,
            morningAssignment.ShiftId,
            morningAssignment.Date,
            ShiftLabel.Morning,
            isOnCall: false);
        solution.AddAssignment(
            mHeavyUser.UserId,
            eveningAssignment.ShiftId,
            eveningAssignment.Date,
            ShiftLabel.Evening,
            isOnCall: false);

        var afterSpread = GetMorningEveningSpread(solution, mHeavyUser)
                          + GetMorningEveningSpread(solution, eHeavyUser);

        if (afterSpread >= beforeSpread)
        {
            // برگرداندن
            solution.RemoveAssignment(eHeavyUser.UserId, morningAssignment.ShiftId, morningAssignment.Date);
            solution.RemoveAssignment(mHeavyUser.UserId, eveningAssignment.ShiftId, eveningAssignment.Date);
            solution.AddAssignment(
                mHeavyUser.UserId,
                morningAssignment.ShiftId,
                morningAssignment.Date,
                ShiftLabel.Morning,
                isOnCall: false);
            solution.AddAssignment(
                eHeavyUser.UserId,
                eveningAssignment.ShiftId,
                eveningAssignment.Date,
                ShiftLabel.Evening,
                isOnCall: false);
            return false;
        }

        return true;
    }

    private static bool CanUserTakeShiftAfterRemoval(
        ShiftSolution solution,
        ShiftConstraints constraints,
        IReadOnlyDictionary<int, ProductivityWorkedHoursCalculator.ShiftWorkInfo> lookup,
        UserConstraint user,
        SaShiftAssignment assignment,
        int ignoreShiftId)
    {
        if (user.UnavailableDates.Any(d => d.Date == assignment.Date.Date))
        {
            return false;
        }

        if (user.UnavailableShiftSlots.Any(s =>
                s.Date.Date == assignment.Date.Date && s.ShiftLabel == assignment.ShiftLabel))
        {
            return false;
        }

        if (user.RequiredShiftSlots.Any(s =>
                s.Date.Date == assignment.Date.Date &&
                s.ShiftLabel == assignment.ShiftLabel &&
                (!s.ShiftId.HasValue || s.ShiftId.Value == assignment.ShiftId)))
        {
            return false;
        }

        var existingLabels = solution.GetUserAssignments(user.UserId, assignment.Date)
            .Where(a => a.ShiftId != ignoreShiftId)
            .Select(a => a.ShiftLabel);
        var maxPerDay = constraints.HardRules.EnforceMaxShiftsPerDay
            ? Math.Max(1, constraints.GlobalConstraints.MaxShiftsPerDay)
            : 2;
        if (!DailyAssignmentRules.CanAddShift(
                existingLabels,
                assignment.ShiftLabel,
                maxPerDay,
                constraints.HardRules.ForbidDuplicateDailyAssignments))
        {
            return false;
        }

        if (AdjacentShiftRestRules.WouldConflict(
                solution.GetUserAllAssignments(user.UserId),
                assignment.Date,
                assignment.ShiftLabel,
                constraints,
                ignoreShiftId))
        {
            return false;
        }

        var projected = solution.GetUserAllAssignments(user.UserId)
            .Where(a => !(a.ShiftId == ignoreShiftId && a.Date.Date == assignment.Date.Date))
            .ToList();
        projected.Add(new SaShiftAssignment
        {
            UserId = user.UserId,
            ShiftId = assignment.ShiftId,
            Date = assignment.Date,
            ShiftLabel = assignment.ShiftLabel,
            IsOnCall = false
        });

        if (user.IncludedInProductivityPlan && user.ProductivityRequiredHours.HasValue)
        {
            var worked = ProductivityWorkedHoursCalculator.CalculateEffectiveWorkedHours(
                projected, lookup, constraints.IsHoliday);
            var maxAllowed = ProductivityWorkedHoursCalculator.GetMaxAllowedHours(
                user.ProductivityRequiredHours,
                user.OvertimeConsent,
                user.MaxMonthlyOvertimeHours);
            if (worked > maxAllowed + 0.25)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsProtected(ShiftConstraints constraints, UserConstraint user, SaShiftAssignment assignment) =>
        user.RequiredShiftSlots.Any(s =>
            s.Date.Date == assignment.Date.Date &&
            s.ShiftLabel == assignment.ShiftLabel &&
            (!s.ShiftId.HasValue || s.ShiftId.Value == assignment.ShiftId));
}
