using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;
using static ShiftYar.Domain.Enums.UserModel.UserEnums;

namespace ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;

/// <summary>
/// بازتوزیع نرم صبح/عصر/شب بر اساس تنظیمات سابقهٔ دپارتمان.
/// بعد از پر کردن موظفی اجرا می‌شود تا اثر جریمهٔ SA زیر گاردهای قوی‌تر از بین نرود.
/// Type: 0=سابقه بیشتر، 1=سابقه کمتر، 2=خنثی (سهم برابر).
/// </summary>
public static class ShiftSeniorityDistributionGuard
{
    private const double SurplusTolerance = 0.75;
    private const int MaxPassesPerLabel = 96;

    public static void Enforce(ShiftSolution solution, ShiftConstraints constraints)
    {
        EnforceLabel(
            solution,
            constraints,
            ShiftLabel.Morning,
            constraints.EnableMorningShiftDistributionBySeniority,
            constraints.MorningShiftDistributionType,
            constraints.SoftWeights.MorningShiftDistributionBySeniorityWeight,
            u => !u.HasExactMorningQuota);

        EnforceLabel(
            solution,
            constraints,
            ShiftLabel.Evening,
            constraints.EnableEveningShiftDistributionBySeniority,
            constraints.EveningShiftDistributionType,
            constraints.SoftWeights.EveningShiftDistributionBySeniorityWeight,
            u => !u.HasExactEveningQuota);

        EnforceLabel(
            solution,
            constraints,
            ShiftLabel.Night,
            constraints.EnableNightShiftDistributionBySeniority,
            constraints.NightShiftDistributionType,
            constraints.SoftWeights.NightShiftDistributionBySeniorityWeight,
            u => !u.HasExactNightQuota);
    }

    private static void EnforceLabel(
        ShiftSolution solution,
        ShiftConstraints constraints,
        ShiftLabel label,
        bool enabled,
        int distributionType,
        double weight,
        Func<UserConstraint, bool> isEligibleForSoftDistribution)
    {
        if (!enabled || weight <= 0)
        {
            return;
        }

        var shiftReq = constraints.ShiftRequirements.FirstOrDefault(s => s.ShiftLabel == label);
        if (shiftReq == null)
        {
            return;
        }

        var eligible = constraints.UserConstraints
            .Where(u => u.ShiftType != ShiftTypes.FixedShift)
            .Where(u => ShiftEligibilityResolver.MayEverTakeLabel(u, label))
            .Where(isEligibleForSoftDistribution)
            .ToList();
        if (eligible.Count < 2)
        {
            return;
        }

        var lookup = ProductivityWorkedHoursCalculator.BuildShiftInfoLookup(constraints.ShiftRequirements);

        for (var pass = 0; pass < MaxPassesPerLabel; pass++)
        {
            var counts = eligible.ToDictionary(
                u => u.UserId,
                u => CountLabel(solution, u.UserId, label));
            var total = counts.Values.Sum();
            if (total == 0)
            {
                return;
            }

            var fair = BuildFairShares(eligible, total, distributionType, constraints.SeniorityDistributionSlope);
            var donor = eligible
                .Select(u => (User: u, Count: counts[u.UserId], Fair: fair[u.UserId], Surplus: counts[u.UserId] - fair[u.UserId]))
                .Where(x => x.Surplus > SurplusTolerance)
                .OrderByDescending(x => x.Surplus)
                .ThenByDescending(x => x.Count)
                .FirstOrDefault();
            if (donor.User == null)
            {
                return;
            }

            var receiver = eligible
                .Where(u => u.UserId != donor.User.UserId)
                .Select(u => (User: u, Count: counts[u.UserId], Fair: fair[u.UserId], Deficit: fair[u.UserId] - counts[u.UserId]))
                .Where(x => x.Deficit > SurplusTolerance)
                .OrderByDescending(x => x.Deficit)
                .ThenBy(x => x.Count)
                .FirstOrDefault(x =>
                    TryTransferOne(
                        solution,
                        constraints,
                        lookup,
                        shiftReq,
                        label,
                        donor.User,
                        x.User));

            if (receiver.User == null)
            {
                // اگر گیرندهٔ ایده‌آل پیدا نشد، از donor با بیشترین مازاد حداقل یک انتقال ممکن را امتحان کن
                var anyMoved = false;
                foreach (var candidate in eligible
                             .Where(u => u.UserId != donor.User.UserId)
                             .OrderBy(u => counts[u.UserId] - fair[u.UserId]))
                {
                    if (counts[candidate.UserId] >= fair[candidate.UserId] + SurplusTolerance)
                    {
                        continue;
                    }

                    if (TryTransferOne(solution, constraints, lookup, shiftReq, label, donor.User, candidate))
                    {
                        anyMoved = true;
                        break;
                    }
                }

                if (!anyMoved)
                {
                    return;
                }
            }
        }
    }

    private static Dictionary<int, double> BuildFairShares(
        List<UserConstraint> eligible,
        int total,
        int distributionType,
        double slope)
    {
        var weights = eligible.ToDictionary(
            u => u.UserId,
            u => ResolveWeight(u.ExperienceYears, distributionType, slope));
        var totalWeight = weights.Values.Sum();
        if (totalWeight <= 0)
        {
            var equal = total / (double)eligible.Count;
            return eligible.ToDictionary(u => u.UserId, _ => equal);
        }

        return eligible.ToDictionary(
            u => u.UserId,
            u => total * weights[u.UserId] / totalWeight);
    }

    public static double ResolveWeight(int experienceYears, int distributionType, double slope)
    {
        var years = Math.Clamp(experienceYears, 0, 40);
        var s = Math.Max(0.1, slope);
        return distributionType switch
        {
            0 => Math.Pow(Math.Max(1, years + 1), s),
            1 => Math.Pow(Math.Max(1, 40 - years), s),
            _ => 1.0
        };
    }

    private static bool TryTransferOne(
        ShiftSolution solution,
        ShiftConstraints constraints,
        IReadOnlyDictionary<int, ProductivityWorkedHoursCalculator.ShiftWorkInfo> lookup,
        ShiftRequirement shiftReq,
        ShiftLabel label,
        UserConstraint donor,
        UserConstraint receiver)
    {
        foreach (var assignment in solution.GetUserAllAssignments(donor.UserId)
                     .Where(a => a.ShiftLabel == label && !a.IsOnCall)
                     .Where(a => !IsProtected(donor, a))
                     .OrderByDescending(a => constraints.IsHoliday(a.Date) ? 0 : 1)
                     .ThenBy(a => a.Date))
        {
            if (!CanReceive(solution, constraints, lookup, receiver, assignment))
            {
                continue;
            }

            if (!HasSpecialtyRoomIgnoringDonor(solution, constraints, shiftReq, assignment.Date, receiver, donor.UserId))
            {
                continue;
            }

            solution.RemoveAssignment(donor.UserId, assignment.ShiftId, assignment.Date);
            solution.AddAssignment(
                receiver.UserId,
                assignment.ShiftId,
                assignment.Date,
                assignment.ShiftLabel,
                isOnCall: false);
            return true;
        }

        return false;
    }

    private static bool CanReceive(
        ShiftSolution solution,
        ShiftConstraints constraints,
        IReadOnlyDictionary<int, ProductivityWorkedHoursCalculator.ShiftWorkInfo> lookup,
        UserConstraint user,
        SaShiftAssignment assignment)
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

        if (!ShiftEligibilityResolver.MayEverTakeLabel(user, assignment.ShiftLabel))
        {
            return false;
        }

        var maxPerDay = constraints.HardRules.EnforceMaxShiftsPerDay
            ? Math.Max(1, constraints.GlobalConstraints.MaxShiftsPerDay)
            : 2;
        var existingLabels = solution.GetUserAssignments(user.UserId, assignment.Date)
            .Select(a => a.ShiftLabel);
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
                constraints))
        {
            return false;
        }

        if (MaxConsecutiveWorkdayRules.WouldExceedMaxConsecutiveWorkdays(
                solution, constraints, user, assignment.Date))
        {
            return false;
        }

        var projected = solution.GetUserAllAssignments(user.UserId).ToList();
        projected.Add(new SaShiftAssignment
        {
            UserId = user.UserId,
            ShiftId = assignment.ShiftId,
            Date = assignment.Date,
            ShiftLabel = assignment.ShiftLabel,
            IsOnCall = false
        });

        if (ProjectPersonnelProductivityPriority.WouldExceedSchedulingCap(
                user,
                ProductivityWorkedHoursCalculator.CalculateEffectiveWorkedHours(
                    projected,
                    lookup,
                    constraints.IsHoliday,
                    uid => uid == user.UserId && user.IncludedInProductivityPlan)))
        {
            return false;
        }

        return true;
    }

    private static bool HasSpecialtyRoomIgnoringDonor(
        ShiftSolution solution,
        ShiftConstraints constraints,
        ShiftRequirement shiftReq,
        DateTime date,
        UserConstraint receiver,
        int donorUserId)
    {
        var specialtyReq = shiftReq.SpecialtyRequirements
            .FirstOrDefault(r => r.SpecialtyId == receiver.SpecialtyId);
        var day = specialtyReq?.ForDay(constraints.IsHoliday(date));
        if (specialtyReq == null || day == null || day.Value.RequiredTotalCount <= 0)
        {
            return true;
        }

        var current = solution.GetShiftAssignments(shiftReq.ShiftId, date)
            .Count(a => !a.IsOnCall
                        && a.UserId != donorUserId
                        && a.UserId != receiver.UserId
                        && GetSpecialty(constraints, a.UserId) == receiver.SpecialtyId);
        return current < day.Value.RequiredTotalCount;
    }

    private static int GetSpecialty(ShiftConstraints constraints, int userId) =>
        constraints.UserConstraints.FirstOrDefault(u => u.UserId == userId)?.SpecialtyId ?? 0;

    private static int CountLabel(ShiftSolution solution, int userId, ShiftLabel label) =>
        solution.GetUserAllAssignments(userId).Count(a => a.ShiftLabel == label && !a.IsOnCall);

    private static bool IsProtected(UserConstraint user, SaShiftAssignment assignment) =>
        user.RequiredShiftSlots.Any(s =>
            s.Date.Date == assignment.Date.Date && s.ShiftLabel == assignment.ShiftLabel);
}
