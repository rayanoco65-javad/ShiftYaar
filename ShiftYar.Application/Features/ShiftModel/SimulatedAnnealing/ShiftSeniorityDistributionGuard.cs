using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;
using static ShiftYar.Domain.Enums.UserModel.UserEnums;

namespace ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;

/// <summary>
/// بازتوزیع صبح/عصر/شب بر اساس سابقه — فقط روی مازاد بالای موظفی.
/// اگر کسی کسری موظفی دارد، اول فقط از اضافه‌کار به کسری منتقل می‌شود؛ بعد نوبت تقسیم آزاد با سابقه است.
/// Type: 0=اولویت سابقه بیشتر، 1=اولویت سابقه کمتر، 2=خنثی/ظرفیت روز.
/// </summary>
public static class ShiftSeniorityDistributionGuard
{
    private const double SurplusTolerance = 0.75;
    private const double HourTolerance = 0.25;
    private const int MaxPasses = 120;

    public static void Enforce(ShiftSolution solution, ShiftConstraints constraints)
    {
        var morningOn = constraints.EnableMorningShiftDistributionBySeniority
                        && constraints.SoftWeights.MorningShiftDistributionBySeniorityWeight > 0;
        var eveningOn = constraints.EnableEveningShiftDistributionBySeniority
                        && constraints.SoftWeights.EveningShiftDistributionBySeniorityWeight > 0;

        if (morningOn && eveningOn
            && constraints.MorningShiftDistributionType == constraints.EveningShiftDistributionType)
        {
            EnforceDayShiftPool(
                solution,
                constraints,
                constraints.MorningShiftDistributionType,
                Math.Max(
                    constraints.SoftWeights.MorningShiftDistributionBySeniorityWeight,
                    constraints.SoftWeights.EveningShiftDistributionBySeniorityWeight));
        }
        else
        {
            if (morningOn)
            {
                EnforceLabel(
                    solution,
                    constraints,
                    ShiftLabel.Morning,
                    constraints.MorningShiftDistributionType,
                    u => !u.HasExactMorningQuota);
            }

            if (eveningOn)
            {
                EnforceLabel(
                    solution,
                    constraints,
                    ShiftLabel.Evening,
                    constraints.EveningShiftDistributionType,
                    u => !u.HasExactEveningQuota);
            }
        }

        if (constraints.EnableNightShiftDistributionBySeniority
            && constraints.SoftWeights.NightShiftDistributionBySeniorityWeight > 0)
        {
            EnforceLabel(
                solution,
                constraints,
                ShiftLabel.Night,
                constraints.NightShiftDistributionType,
                u => !u.HasExactNightQuota);
        }
    }

    private static void EnforceDayShiftPool(
        ShiftSolution solution,
        ShiftConstraints constraints,
        int distributionType,
        double weight)
    {
        if (weight <= 0)
        {
            return;
        }

        var morningReq = constraints.ShiftRequirements.FirstOrDefault(s => s.ShiftLabel == ShiftLabel.Morning);
        var eveningReq = constraints.ShiftRequirements.FirstOrDefault(s => s.ShiftLabel == ShiftLabel.Evening);
        if (morningReq == null && eveningReq == null)
        {
            return;
        }

        var eligible = constraints.UserConstraints
            .Where(u => u.ShiftType != ShiftTypes.FixedShift)
            .Where(u =>
                (ShiftEligibilityResolver.MayEverTakeLabel(u, ShiftLabel.Morning) && !u.HasExactMorningQuota)
                || (ShiftEligibilityResolver.MayEverTakeLabel(u, ShiftLabel.Evening) && !u.HasExactEveningQuota))
            .ToList();
        if (eligible.Count < 2)
        {
            return;
        }

        var lookup = ProductivityWorkedHoursCalculator.BuildShiftInfoLookup(constraints.ShiftRequirements);
        var monthDays = Math.Max(1, (constraints.EndDate.Date - constraints.StartDate.Date).Days + 1);
        var planLookup = ProductivityWorkedHoursCalculator.BuildProductivityPlanLookup(constraints.UserConstraints);

        for (var pass = 0; pass < MaxPasses; pass++)
        {
            var worked = BuildWorkedLookup(solution, constraints, lookup, planLookup, eligible);
            var hourDeficitUsers = eligible.Where(u => GetHourDeficit(u, worked[u.UserId]) > HourTolerance).ToList();
            var fillRequiredFirst = hourDeficitUsers.Count > 0;

            var counts = eligible.ToDictionary(u => u.UserId, u => CountDayShifts(solution, u.UserId));
            var total = counts.Values.Sum();
            if (total == 0)
            {
                return;
            }

            var fair = BuildDayFairShares(
                solution, eligible, total, distributionType, constraints.SeniorityDistributionSlope, monthDays);

            var donorCandidates = eligible
                .Where(u => HasDonatableHourSurplus(u, worked[u.UserId]))
                .Select(u => (
                    User: u,
                    Count: counts[u.UserId],
                    Fair: fair[u.UserId],
                    CountSurplus: counts[u.UserId] - fair[u.UserId],
                    HourSurplus: GetHourSurplus(u, worked[u.UserId])))
                .Where(x => fillRequiredFirst
                    ? x.HourSurplus > HourTolerance
                    : x.CountSurplus > SurplusTolerance)
                .OrderByDescending(x => fillRequiredFirst ? x.HourSurplus : x.CountSurplus)
                .ThenByDescending(x => distributionType == 1 ? x.User.ExperienceYears : -x.User.ExperienceYears)
                .ToList();

            if (donorCandidates.Count == 0)
            {
                return;
            }

            var moved = false;
            foreach (var donor in donorCandidates)
            {
                var receivers = fillRequiredFirst
                    ? hourDeficitUsers
                        .Where(u => u.UserId != donor.User.UserId)
                        .OrderByDescending(u => GetHourDeficit(u, worked[u.UserId]))
                        .ThenBy(u => distributionType == 1 ? u.ExperienceYears : -u.ExperienceYears)
                        .ToList()
                    : eligible
                        .Where(u => u.UserId != donor.User.UserId)
                        .Where(u => fair[u.UserId] - counts[u.UserId] > SurplusTolerance)
                        .OrderByDescending(u => fair[u.UserId] - counts[u.UserId])
                        .ThenBy(u => distributionType == 1 ? u.ExperienceYears : -u.ExperienceYears)
                        .ToList();

                foreach (var receiver in receivers)
                {
                    if (TryTransferDayShift(
                            solution,
                            constraints,
                            lookup,
                            morningReq,
                            eveningReq,
                            donor.User,
                            receiver,
                            worked[donor.User.UserId]))
                    {
                        moved = true;
                        break;
                    }
                }

                if (moved)
                {
                    break;
                }
            }

            if (!moved)
            {
                return;
            }
        }
    }

    private static void EnforceLabel(
        ShiftSolution solution,
        ShiftConstraints constraints,
        ShiftLabel label,
        int distributionType,
        Func<UserConstraint, bool> isEligibleForSoftDistribution)
    {
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
        var monthDays = Math.Max(1, (constraints.EndDate.Date - constraints.StartDate.Date).Days + 1);
        var planLookup = ProductivityWorkedHoursCalculator.BuildProductivityPlanLookup(constraints.UserConstraints);

        for (var pass = 0; pass < MaxPasses; pass++)
        {
            var worked = BuildWorkedLookup(solution, constraints, lookup, planLookup, eligible);
            var hourDeficitUsers = eligible.Where(u => GetHourDeficit(u, worked[u.UserId]) > HourTolerance).ToList();
            var fillRequiredFirst = hourDeficitUsers.Count > 0;

            var counts = eligible.ToDictionary(u => u.UserId, u => CountLabel(solution, u.UserId, label));
            var total = counts.Values.Sum();
            if (total == 0)
            {
                return;
            }

            var fair = label == ShiftLabel.Night
                ? BuildFairShares(eligible, total, distributionType, constraints.SeniorityDistributionSlope)
                : BuildDayFairShares(
                    solution, eligible, total, distributionType, constraints.SeniorityDistributionSlope, monthDays);

            var donorCandidates = eligible
                .Where(u => HasDonatableHourSurplus(u, worked[u.UserId]))
                .Select(u => (
                    User: u,
                    Count: counts[u.UserId],
                    Fair: fair[u.UserId],
                    CountSurplus: counts[u.UserId] - fair[u.UserId],
                    HourSurplus: GetHourSurplus(u, worked[u.UserId])))
                .Where(x => fillRequiredFirst
                    ? x.HourSurplus > HourTolerance
                    : x.CountSurplus > SurplusTolerance)
                .OrderByDescending(x => fillRequiredFirst ? x.HourSurplus : x.CountSurplus)
                .ThenByDescending(x => distributionType == 1 ? x.User.ExperienceYears : -x.User.ExperienceYears)
                .ToList();

            if (donorCandidates.Count == 0)
            {
                return;
            }

            var moved = false;
            foreach (var donor in donorCandidates)
            {
                var receivers = fillRequiredFirst
                    ? hourDeficitUsers
                        .Where(u => u.UserId != donor.User.UserId)
                        .OrderByDescending(u => GetHourDeficit(u, worked[u.UserId]))
                        .ThenBy(u => distributionType == 1 ? u.ExperienceYears : -u.ExperienceYears)
                        .ToList()
                    : eligible
                        .Where(u => u.UserId != donor.User.UserId)
                        .Where(u => fair[u.UserId] - counts[u.UserId] > SurplusTolerance)
                        .OrderByDescending(u => fair[u.UserId] - counts[u.UserId])
                        .ThenBy(u => distributionType == 1 ? u.ExperienceYears : -u.ExperienceYears)
                        .ToList();

                foreach (var receiver in receivers)
                {
                    if (TryTransferOne(
                            solution,
                            constraints,
                            lookup,
                            shiftReq,
                            label,
                            donor.User,
                            receiver,
                            worked[donor.User.UserId]))
                    {
                        moved = true;
                        break;
                    }
                }

                if (moved)
                {
                    break;
                }
            }

            if (!moved)
            {
                return;
            }
        }
    }

    private static Dictionary<int, double> BuildDayFairShares(
        ShiftSolution solution,
        List<UserConstraint> eligible,
        int total,
        int distributionType,
        double slope,
        int monthDays)
    {
        var weights = eligible.ToDictionary(
            u => u.UserId,
            u =>
            {
                var seniority = ResolveWeight(u.ExperienceYears, distributionType, slope);
                var nights = CountLabel(solution, u.UserId, ShiftLabel.Night);
                var dayCapacity = Math.Max(1, monthDays - nights);
                // خنثی: سهم متناسب با ظرفیت روز بعد از شب‌ها
                // اولویت سابقه: وزن سابقه × ظرفیت تا هدف غیرواقعی برای شب‌کارها ساخته نشود
                return distributionType == 2
                    ? dayCapacity
                    : seniority * dayCapacity;
            });

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

    private static Dictionary<int, double> BuildWorkedLookup(
        ShiftSolution solution,
        ShiftConstraints constraints,
        IReadOnlyDictionary<int, ProductivityWorkedHoursCalculator.ShiftWorkInfo> lookup,
        Func<int, bool> planLookup,
        List<UserConstraint> eligible)
    {
        return eligible.ToDictionary(
            u => u.UserId,
            u => ProductivityWorkedHoursCalculator.CalculateEffectiveWorkedHours(
                solution.GetUserAllAssignments(u.UserId),
                lookup,
                constraints.IsHoliday,
                planLookup));
    }

    private static bool HasDonatableHourSurplus(UserConstraint user, double worked) =>
        GetHourSurplus(user, worked) > HourTolerance
        || !user.IncludedInProductivityPlan
        || !user.ProductivityRequiredHours.HasValue;

    private static double GetHourSurplus(UserConstraint user, double worked)
    {
        if (!user.IncludedInProductivityPlan || !user.ProductivityRequiredHours.HasValue)
        {
            return 0;
        }

        return Math.Max(0, worked - (double)user.ProductivityRequiredHours.Value);
    }

    private static double GetHourDeficit(UserConstraint user, double worked)
    {
        if (!user.IncludedInProductivityPlan || !user.ProductivityRequiredHours.HasValue)
        {
            return 0;
        }

        return Math.Max(0, (double)user.ProductivityRequiredHours.Value - worked);
    }

    private static bool WouldLeaveDonorBelowRequired(
        UserConstraint donor,
        double donorWorked,
        SaShiftAssignment assignment,
        IReadOnlyDictionary<int, ProductivityWorkedHoursCalculator.ShiftWorkInfo> lookup,
        ShiftConstraints constraints)
    {
        if (!donor.IncludedInProductivityPlan || !donor.ProductivityRequiredHours.HasValue)
        {
            return false;
        }

        var shiftHours = ProductivityWorkedHoursCalculator.EstimateAssignmentHours(
            assignment,
            lookup,
            constraints.IsHoliday(assignment.Date),
            donor.IncludedInProductivityPlan);
        return donorWorked - shiftHours < (double)donor.ProductivityRequiredHours.Value - HourTolerance;
    }

    private static bool TryTransferDayShift(
        ShiftSolution solution,
        ShiftConstraints constraints,
        IReadOnlyDictionary<int, ProductivityWorkedHoursCalculator.ShiftWorkInfo> lookup,
        ShiftRequirement? morningReq,
        ShiftRequirement? eveningReq,
        UserConstraint donor,
        UserConstraint receiver,
        double donorWorked)
    {
        foreach (var assignment in solution.GetUserAllAssignments(donor.UserId)
                     .Where(a => !a.IsOnCall
                                 && (a.ShiftLabel == ShiftLabel.Morning || a.ShiftLabel == ShiftLabel.Evening))
                     .Where(a => !IsProtected(donor, a))
                     .OrderBy(a => a.ShiftLabel == ShiftLabel.Evening ? 0 : 1)
                     .ThenBy(a => a.Date))
        {
            var shiftReq = assignment.ShiftLabel == ShiftLabel.Morning ? morningReq : eveningReq;
            if (shiftReq == null)
            {
                continue;
            }

            if (!ShiftEligibilityResolver.MayEverTakeLabel(receiver, assignment.ShiftLabel))
            {
                continue;
            }

            if (assignment.ShiftLabel == ShiftLabel.Morning && receiver.HasExactMorningQuota)
            {
                continue;
            }

            if (assignment.ShiftLabel == ShiftLabel.Evening && receiver.HasExactEveningQuota)
            {
                continue;
            }

            if (WouldLeaveDonorBelowRequired(donor, donorWorked, assignment, lookup, constraints))
            {
                continue;
            }

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

    private static bool TryTransferOne(
        ShiftSolution solution,
        ShiftConstraints constraints,
        IReadOnlyDictionary<int, ProductivityWorkedHoursCalculator.ShiftWorkInfo> lookup,
        ShiftRequirement shiftReq,
        ShiftLabel label,
        UserConstraint donor,
        UserConstraint receiver,
        double donorWorked)
    {
        foreach (var assignment in solution.GetUserAllAssignments(donor.UserId)
                     .Where(a => a.ShiftLabel == label && !a.IsOnCall)
                     .Where(a => !IsProtected(donor, a))
                     .OrderByDescending(a => constraints.IsHoliday(a.Date) ? 0 : 1)
                     .ThenBy(a => a.Date))
        {
            if (WouldLeaveDonorBelowRequired(donor, donorWorked, assignment, lookup, constraints))
            {
                continue;
            }

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

    private static int CountDayShifts(ShiftSolution solution, int userId) =>
        solution.GetUserAllAssignments(userId)
            .Count(a => !a.IsOnCall
                        && (a.ShiftLabel == ShiftLabel.Morning || a.ShiftLabel == ShiftLabel.Evening));

    private static int CountLabel(ShiftSolution solution, int userId, ShiftLabel label) =>
        solution.GetUserAllAssignments(userId).Count(a => a.ShiftLabel == label && !a.IsOnCall);

    private static bool IsProtected(UserConstraint user, SaShiftAssignment assignment) =>
        user.RequiredShiftSlots.Any(s =>
            s.Date.Date == assignment.Date.Date && s.ShiftLabel == assignment.ShiftLabel);
}
