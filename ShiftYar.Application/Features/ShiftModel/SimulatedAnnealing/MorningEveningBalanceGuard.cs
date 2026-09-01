using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;
using static ShiftYar.Domain.Enums.UserModel.UserEnums;

namespace ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;

/// <summary>
/// تعادل تعداد شیفت صبح/عصر درون هر کاربر با جابجایی بین دو نفر (پوشش حفظ می‌شود).
/// سقف اختلاف مجاز از نسبت مجموع شیفت‌های صبح/عصر بخش مشتق می‌شود.
/// </summary>
public static class MorningEveningBalanceGuard
{
    /// <summary>
    /// حداکثر اختلاف مجاز صبح−عصر (مثبت) و عصر−صبح (مثبت) برای هر کاربر
    /// بر اساس نسبت کل شیفت‌های بخش.
    /// </summary>
    public readonly record struct MorningEveningSpreadLimits(int MaxMorningSurplus, int MaxEveningSurplus);

    public static bool TrySingleSwap(ShiftSolution solution, ShiftConstraints constraints)
    {
        var lookup = ProductivityWorkedHoursCalculator.BuildShiftInfoLookup(constraints.ShiftRequirements);
        var users = GetBalanceableUsers(constraints);
        var limits = GetDepartmentSpreadLimits(solution, users);

        foreach (var pair in BuildImbalancedPairs(solution, users, limits))
        {
            if (TryBalanceMorningEveningBetweenUsers(
                    solution, constraints, lookup, limits, pair.MHeavy, pair.EHeavy))
            {
                return true;
            }
        }

        return false;
    }

    public static void Enforce(ShiftSolution solution, ShiftConstraints constraints)
    {
        var lookup = ProductivityWorkedHoursCalculator.BuildShiftInfoLookup(constraints.ShiftRequirements);
        var users = GetBalanceableUsers(constraints);

        if (users.Count < 2)
        {
            return;
        }

        for (var pass = 0; pass < 64; pass++)
        {
            var limits = GetDepartmentSpreadLimits(solution, users);
            var progressed = false;
            foreach (var pair in BuildImbalancedPairs(solution, users, limits))
            {
                if (TryBalanceMorningEveningBetweenUsers(
                        solution, constraints, lookup, limits, pair.MHeavy, pair.EHeavy))
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

    public static (int TotalMorning, int TotalEvening) GetDepartmentMorningEveningTotals(
        ShiftSolution solution,
        IEnumerable<UserConstraint> users)
    {
        var totalMorning = 0;
        var totalEvening = 0;
        foreach (var user in users)
        {
            totalMorning += CountMorning(solution, user.UserId);
            totalEvening += CountEvening(solution, user.UserId);
        }

        return (totalMorning, totalEvening);
    }

    /// <summary>
    /// اگر مجموع صبح = n×مجموع عصر باشد، سقف اضافهٔ صبح هر کاربر n است؛
    /// اگر مجموع عصر = n×مجموع صبح باشد، سقف اضافهٔ عصر n است؛
    /// اگر برابر باشند هر دو سقف ۱ است؛
    /// در نسبت‌های غیردقیق، از سقف نسبتی و سقف مازادِ سراسری (پخش‌شده بین کاربران) کوچک‌تر استفاده می‌شود.
    /// </summary>
    public static MorningEveningSpreadLimits GetDepartmentSpreadLimits(
        int totalMorning,
        int totalEvening,
        int balanceableUserCount = 0)
    {
        if (totalMorning == totalEvening)
        {
            return new MorningEveningSpreadLimits(1, 1);
        }

        if (totalMorning > totalEvening)
        {
            var maxMorningSurplus = CalculateDirectionalSurplus(
                totalMorning, totalEvening, balanceableUserCount);
            return new MorningEveningSpreadLimits(maxMorningSurplus, 1);
        }

        var maxEveningSurplus = CalculateDirectionalSurplus(
            totalEvening, totalMorning, balanceableUserCount);
        return new MorningEveningSpreadLimits(1, maxEveningSurplus);
    }

    public static MorningEveningSpreadLimits GetDepartmentSpreadLimits(
        ShiftSolution solution,
        IEnumerable<UserConstraint> users)
    {
        var userList = users as IList<UserConstraint> ?? users.ToList();
        var (totalMorning, totalEvening) = GetDepartmentMorningEveningTotals(solution, userList);
        return GetDepartmentSpreadLimits(totalMorning, totalEvening, userList.Count);
    }

    private static int CalculateDirectionalSurplus(
        int dominantTotal,
        int otherTotal,
        int balanceableUserCount)
    {
        if (otherTotal <= 0)
        {
            return Math.Max(1, dominantTotal);
        }

        if (dominantTotal % otherTotal == 0)
        {
            return dominantTotal / otherTotal;
        }

        var ratioCeil = (int)Math.Ceiling((double)dominantTotal / otherTotal);
        if (balanceableUserCount <= 0)
        {
            return Math.Max(1, ratioCeil);
        }

        var globalSlack = dominantTotal - otherTotal;
        var distributedSlack = 1 + (globalSlack + balanceableUserCount - 1) / balanceableUserCount;
        return Math.Max(1, Math.Min(ratioCeil, distributedSlack));
    }

    public static bool IsWithinAllowedSpread(
        ShiftSolution solution,
        UserConstraint user,
        MorningEveningSpreadLimits limits)
    {
        return GetMorningEveningViolation(solution, user, limits) == 0;
    }

    public static int GetMorningEveningViolation(
        ShiftSolution solution,
        UserConstraint user,
        MorningEveningSpreadLimits limits)
    {
        var delta = CountMorning(solution, user.UserId) - CountEvening(solution, user.UserId);
        if (delta > limits.MaxMorningSurplus)
        {
            return delta - limits.MaxMorningSurplus;
        }

        if (delta < -limits.MaxEveningSurplus)
        {
            return (-delta) - limits.MaxEveningSurplus;
        }

        return 0;
    }

    private static List<UserConstraint> GetBalanceableUsers(ShiftConstraints constraints) =>
        constraints.UserConstraints
            .Where(u => u.IsActive && u.ShiftType != ShiftTypes.FixedShift)
            .Where(CanBalanceMorningEvening)
            .ToList();

    private static bool CanBalanceMorningEvening(UserConstraint user) =>
        ShiftEligibilityResolver.SupportsMorningEveningCombo(user);

    private static IEnumerable<(UserConstraint MHeavy, UserConstraint EHeavy)> BuildImbalancedPairs(
        ShiftSolution solution,
        List<UserConstraint> users,
        MorningEveningSpreadLimits limits)
    {
        var scored = users
            .Select(u =>
            {
                var morning = CountMorning(solution, u.UserId);
                var evening = CountEvening(solution, u.UserId);
                return (User: u, Morning: morning, Evening: evening, Delta: morning - evening);
            })
            .Where(x => x.Delta > limits.MaxMorningSurplus || x.Delta < -limits.MaxEveningSurplus)
            .OrderByDescending(x => Math.Abs(x.Delta))
            .ToList();

        var mHeavy = scored.Where(x => x.Delta > limits.MaxMorningSurplus).OrderByDescending(x => x.Delta).ToList();
        var eHeavy = scored.Where(x => x.Delta < -limits.MaxEveningSurplus).OrderBy(x => x.Delta).ToList();

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

    private static bool TryBalanceMorningEveningBetweenUsers(
        ShiftSolution solution,
        ShiftConstraints constraints,
        IReadOnlyDictionary<int, ProductivityWorkedHoursCalculator.ShiftWorkInfo> lookup,
        MorningEveningSpreadLimits limits,
        UserConstraint mHeavyUser,
        UserConstraint eHeavyUser)
    {
        var mAssignments = solution.GetUserAllAssignments(mHeavyUser.UserId)
            .Where(a => a.ShiftLabel == ShiftLabel.Morning && !a.IsOnCall)
            .Where(a => !IsProtected(constraints, mHeavyUser, a))
            .OrderByDescending(a => constraints.IsHoliday(a.Date))
            .ToList();

        var eAssignments = solution.GetUserAllAssignments(eHeavyUser.UserId)
            .Where(a => a.ShiftLabel == ShiftLabel.Evening && !a.IsOnCall)
            .Where(a => !IsProtected(constraints, eHeavyUser, a))
            .OrderByDescending(a => constraints.IsHoliday(a.Date))
            .ToList();

        foreach (var mAssignment in mAssignments)
        {
            foreach (var eAssignment in eAssignments.Where(a => a.Date.Date == mAssignment.Date.Date))
            {
                if (TrySwapSameDayMorningEvening(
                        solution, constraints, lookup, limits,
                        mHeavyUser, eHeavyUser,
                        mAssignment, eAssignment))
                {
                    return true;
                }
            }
        }

        foreach (var mAssignment in mAssignments)
        {
            foreach (var eAssignment in eAssignments.Where(a => a.Date.Date != mAssignment.Date.Date))
            {
                if (TryCrossDayMorningEveningExchange(
                        solution, constraints, lookup, limits,
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
        MorningEveningSpreadLimits limits,
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

        var beforeViolation = GetMorningEveningViolation(solution, mHeavyUser, limits)
                              + GetMorningEveningViolation(solution, eHeavyUser, limits);

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

        return AcceptOrRevertMorningEveningSwap(
            solution,
            mHeavyUser,
            eHeavyUser,
            limits,
            beforeViolation,
            () =>
            {
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
            });
    }

    private static bool TryCrossDayMorningEveningExchange(
        ShiftSolution solution,
        ShiftConstraints constraints,
        IReadOnlyDictionary<int, ProductivityWorkedHoursCalculator.ShiftWorkInfo> lookup,
        MorningEveningSpreadLimits limits,
        UserConstraint mHeavyUser,
        UserConstraint eHeavyUser,
        SaShiftAssignment morningAssignment,
        SaShiftAssignment eveningAssignment)
    {
        if (morningAssignment.Date.Date == eveningAssignment.Date.Date)
        {
            return false;
        }

        var mHeavyEvening = new SaShiftAssignment
        {
            UserId = mHeavyUser.UserId,
            ShiftId = eveningAssignment.ShiftId,
            Date = eveningAssignment.Date,
            ShiftLabel = ShiftLabel.Evening,
            IsOnCall = false
        };
        var eHeavyMorning = new SaShiftAssignment
        {
            UserId = eHeavyUser.UserId,
            ShiftId = morningAssignment.ShiftId,
            Date = morningAssignment.Date,
            ShiftLabel = ShiftLabel.Morning,
            IsOnCall = false
        };

        if (!CanUserTakeShiftAfterRemovals(
                solution, constraints, lookup, eHeavyUser, eHeavyMorning, eveningAssignment))
        {
            return false;
        }

        if (!CanUserTakeShiftAfterRemovals(
                solution, constraints, lookup, mHeavyUser, mHeavyEvening, morningAssignment))
        {
            return false;
        }

        var beforeViolation = GetMorningEveningViolation(solution, mHeavyUser, limits)
                              + GetMorningEveningViolation(solution, eHeavyUser, limits);

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

        return AcceptOrRevertMorningEveningSwap(
            solution,
            mHeavyUser,
            eHeavyUser,
            limits,
            beforeViolation,
            () =>
            {
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
            });
    }

    private static bool AcceptOrRevertMorningEveningSwap(
        ShiftSolution solution,
        UserConstraint mHeavyUser,
        UserConstraint eHeavyUser,
        MorningEveningSpreadLimits limits,
        int beforeViolation,
        Action revert)
    {
        var afterViolation = GetMorningEveningViolation(solution, mHeavyUser, limits)
                             + GetMorningEveningViolation(solution, eHeavyUser, limits);
        var withinTarget = IsWithinAllowedSpread(solution, mHeavyUser, limits)
                           && IsWithinAllowedSpread(solution, eHeavyUser, limits);

        if (afterViolation < beforeViolation || withinTarget)
        {
            return true;
        }

        revert();
        return false;
    }

    private static bool CanUserTakeShiftAfterRemoval(
        ShiftSolution solution,
        ShiftConstraints constraints,
        IReadOnlyDictionary<int, ProductivityWorkedHoursCalculator.ShiftWorkInfo> lookup,
        UserConstraint user,
        SaShiftAssignment assignment,
        int ignoreShiftId) =>
        CanUserTakeShiftAfterRemovals(
            solution,
            constraints,
            lookup,
            user,
            assignment,
            solution.GetUserAllAssignments(user.UserId)
                .Where(a => a.ShiftId == ignoreShiftId && a.Date.Date == assignment.Date.Date)
                .ToArray());

    private static bool CanUserTakeShiftAfterRemovals(
        ShiftSolution solution,
        ShiftConstraints constraints,
        IReadOnlyDictionary<int, ProductivityWorkedHoursCalculator.ShiftWorkInfo> lookup,
        UserConstraint user,
        SaShiftAssignment assignment,
        params SaShiftAssignment[] toRemove)
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

        var removeKeys = toRemove
            .Select(r => (r.ShiftId, r.Date.Date))
            .ToHashSet();
        var existingLabels = solution.GetUserAllAssignments(user.UserId)
            .Where(a => !removeKeys.Contains((a.ShiftId, a.Date.Date)))
            .Where(a => a.Date.Date == assignment.Date.Date)
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

        var remaining = solution.GetUserAllAssignments(user.UserId)
            .Where(a => !removeKeys.Contains((a.ShiftId, a.Date.Date)))
            .ToList();
        if (AdjacentShiftRestRules.WouldConflict(
                remaining,
                assignment.Date,
                assignment.ShiftLabel,
                constraints))
        {
            return false;
        }

        if (MaxConsecutiveWorkdayRules.WouldExceedMaxConsecutiveWorkdays(
                remaining,
                user,
                assignment.Date,
                constraints.HardRules.EnforceMaxConsecutiveShifts))
        {
            return false;
        }

        var projected = remaining.ToList();
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
                projected,
                lookup,
                constraints.IsHoliday,
                uid => uid == user.UserId && user.IncludedInProductivityPlan);
            if (ProjectPersonnelProductivityPriority.WouldExceedSchedulingCap(user, worked))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsProtected(ShiftConstraints constraints, UserConstraint user, SaShiftAssignment assignment) =>
        assignment.IsSkeleton
        || user.RequiredShiftSlots.Any(s =>
            s.Date.Date == assignment.Date.Date &&
            s.ShiftLabel == assignment.ShiftLabel &&
            (!s.ShiftId.HasValue || s.ShiftId.Value == assignment.ShiftId));
}
