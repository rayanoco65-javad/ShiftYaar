using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;
using static ShiftYar.Domain.Enums.UserModel.UserEnums;

namespace ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;

/// <summary>
/// تلاش برای نزدیک کردن ساعات مؤثر کاربران به سقف موظفی و تعادل بین پرسنل.
/// </summary>
public static class ProductivityHourFillGuard
{
    private const double DeficitToleranceHours = 2.0;

    public static void Enforce(ShiftSolution solution, ShiftConstraints constraints)
    {
        var lookup = ProductivityWorkedHoursCalculator.BuildShiftInfoLookup(constraints.ShiftRequirements);
        var productivityUsers = constraints.UserConstraints
            .Where(u => u.IsActive && u.ShiftType != ShiftTypes.FixedShift)
            .Where(u => u.IncludedInProductivityPlan && u.ProductivityRequiredHours.HasValue)
            .ToList();

        if (productivityUsers.Count == 0)
        {
            return;
        }

        BalanceByReassignment(solution, constraints, lookup, productivityUsers);
        FillUnderstaffedSlots(solution, constraints, lookup, productivityUsers);
        BalanceByReassignment(solution, constraints, lookup, productivityUsers);
    }

    private static void BalanceByReassignment(
        ShiftSolution solution,
        ShiftConstraints constraints,
        IReadOnlyDictionary<int, ProductivityWorkedHoursCalculator.ShiftWorkInfo> lookup,
        List<UserConstraint> productivityUsers)
    {
        for (var pass = 0; pass < 48; pass++)
        {
            var hours = productivityUsers.ToDictionary(
                u => u.UserId,
                u => CalculateWorked(solution, u.UserId, lookup, constraints));
            var avg = hours.Values.Average();
            var donor = productivityUsers
                .Where(u => !IsProtectedUser(u))
                .Where(u => hours[u.UserId] > avg + 4)
                .OrderByDescending(u => hours[u.UserId] - (double)u.ProductivityRequiredHours!.Value)
                .FirstOrDefault();
            var receiver = productivityUsers
                .Where(u => GetDeficit(u, hours[u.UserId]) > DeficitToleranceHours)
                .OrderByDescending(u => GetDeficit(u, hours[u.UserId]))
                .FirstOrDefault();

            if (donor == null || receiver == null)
            {
                return;
            }

            var moved = false;
            foreach (var assignment in solution.GetUserAllAssignments(donor.UserId)
                         .Where(a => !a.IsOnCall && !IsProtectedAssignment(constraints, a))
                         .OrderByDescending(a => EstimateShiftHours(a, lookup, constraints)))
            {
                if (!CanUserTakeShift(solution, constraints, lookup, receiver, assignment, ignoreShiftId: null))
                {
                    continue;
                }

                if (!WouldImproveBalance(hours, donor.UserId, receiver.UserId, assignment, lookup, constraints))
                {
                    continue;
                }

                solution.RemoveAssignment(donor.UserId, assignment.ShiftId, assignment.Date);
                solution.AddAssignment(
                    receiver.UserId,
                    assignment.ShiftId,
                    assignment.Date,
                    assignment.ShiftLabel,
                    assignment.IsOnCall);
                moved = true;
                break;
            }

            if (!moved)
            {
                return;
            }
        }
    }

    private static void FillUnderstaffedSlots(
        ShiftSolution solution,
        ShiftConstraints constraints,
        IReadOnlyDictionary<int, ProductivityWorkedHoursCalculator.ShiftWorkInfo> lookup,
        List<UserConstraint> productivityUsers)
    {
        var dates = Enumerable.Range(0, (constraints.EndDate.Date - constraints.StartDate.Date).Days + 1)
            .Select(i => constraints.StartDate.Date.AddDays(i))
            .ToList();

        foreach (var user in productivityUsers.OrderByDescending(u =>
                     GetDeficit(u, CalculateWorked(solution, u.UserId, lookup, constraints))))
        {
            foreach (var date in dates)
            {
                var deficit = GetDeficit(user, CalculateWorked(solution, user.UserId, lookup, constraints));
                if (deficit <= DeficitToleranceHours)
                {
                    break;
                }

                // صبح+عصر همان روز بیشترین ساعت مؤثر را می‌دهد
                if (TryAddLabel(solution, constraints, lookup, user, date, ShiftLabel.Morning) &&
                    TryAddLabel(solution, constraints, lookup, user, date, ShiftLabel.Evening))
                {
                    continue;
                }

                foreach (var label in new[] { ShiftLabel.Morning, ShiftLabel.Evening, ShiftLabel.Night })
                {
                    if (TryAddLabel(solution, constraints, lookup, user, date, label))
                    {
                        break;
                    }
                }
            }
        }
    }

    private static bool TryAddLabel(
        ShiftSolution solution,
        ShiftConstraints constraints,
        IReadOnlyDictionary<int, ProductivityWorkedHoursCalculator.ShiftWorkInfo> lookup,
        UserConstraint user,
        DateTime date,
        ShiftLabel label)
    {
        if (!ShiftEligibilityResolver.IsLabelAllowed(user.AllowedShiftLabels, label))
        {
            return false;
        }

        var shiftReq = constraints.ShiftRequirements
            .Where(s => s.ShiftLabel == label)
            .OrderByDescending(s => s.SpecialtyRequirements
                .FirstOrDefault(r => r.SpecialtyId == user.SpecialtyId)?.RequiredTotalCount ?? 0)
            .FirstOrDefault();
        if (shiftReq == null)
        {
            return false;
        }

        if (solution.HasAssignment(user.UserId, shiftReq.ShiftId, date))
        {
            return false;
        }

        var pseudo = new SaShiftAssignment
        {
            UserId = user.UserId,
            ShiftId = shiftReq.ShiftId,
            Date = date,
            ShiftLabel = label,
            IsOnCall = false
        };

        if (!CanUserTakeShift(solution, constraints, lookup, user, pseudo, ignoreShiftId: null))
        {
            return false;
        }

        if (!HasSpecialtyCapacity(solution, constraints, shiftReq, date, user.SpecialtyId))
        {
            return false;
        }

        solution.AddAssignment(user.UserId, shiftReq.ShiftId, date, label, isOnCall: false);
        return true;
    }

    private static bool CanUserTakeShift(
        ShiftSolution solution,
        ShiftConstraints constraints,
        IReadOnlyDictionary<int, ProductivityWorkedHoursCalculator.ShiftWorkInfo> lookup,
        UserConstraint user,
        SaShiftAssignment assignment,
        int? ignoreShiftId)
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

        var existingLabels = solution.GetUserAssignments(user.UserId, assignment.Date)
            .Where(a => !(ignoreShiftId.HasValue && a.ShiftId == ignoreShiftId.Value))
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
                ignoreShiftId))
        {
            return false;
        }

        if (assignment.ShiftLabel == ShiftLabel.Night)
        {
            var nights = solution.GetUserAllAssignments(user.UserId)
                .Where(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall)
                .ToList();
            if (user.ExactNightShiftCount.HasValue && nights.Count >= user.ExactNightShiftCount.Value)
            {
                return false;
            }

            if (user.MinDaysBetweenNightShifts > 0)
            {
                foreach (var n in nights)
                {
                    if (Math.Abs((assignment.Date.Date - n.Date.Date).Days) <= user.MinDaysBetweenNightShifts)
                    {
                        return false;
                    }
                }
            }
        }

        var projected = solution.GetUserAllAssignments(user.UserId).ToList();
        projected.Add(assignment);
        var worked = ProductivityWorkedHoursCalculator.CalculateEffectiveWorkedHours(
            projected, lookup, constraints.IsHoliday);
        var maxAllowed = ProductivityWorkedHoursCalculator.GetMaxAllowedHours(
            user.ProductivityRequiredHours,
            user.OvertimeConsent,
            user.MaxMonthlyOvertimeHours);
        return worked <= maxAllowed + 0.25;
    }

    private static bool HasSpecialtyCapacity(
        ShiftSolution solution,
        ShiftConstraints constraints,
        ShiftRequirement shiftReq,
        DateTime date,
        int specialtyId)
    {
        var specialtyReq = shiftReq.SpecialtyRequirements.FirstOrDefault(r => r.SpecialtyId == specialtyId)
                           ?? shiftReq.SpecialtyRequirements.FirstOrDefault();
        if (specialtyReq == null)
        {
            return true;
        }

        var day = specialtyReq.ForDay(constraints.IsHoliday(date));
        var current = solution.GetShiftAssignments(shiftReq.ShiftId, date)
            .Count(a => !a.IsOnCall && a.UserId > 0 &&
                        constraints.UserConstraints.FirstOrDefault(u => u.UserId == a.UserId)?.SpecialtyId == specialtyId);
        return current < Math.Max(day.RequiredTotalCount, 1);
    }

    private static bool WouldImproveBalance(
        Dictionary<int, double> hours,
        int donorId,
        int receiverId,
        SaShiftAssignment assignment,
        IReadOnlyDictionary<int, ProductivityWorkedHoursCalculator.ShiftWorkInfo> lookup,
        ShiftConstraints constraints)
    {
        var shiftHours = EstimateShiftHours(assignment, lookup, constraints);
        var before = hours.Values.Sum(h => Math.Abs(h - hours.Values.Average()));
        var afterHours = new Dictionary<int, double>(hours)
        {
            [donorId] = hours[donorId] - shiftHours,
            [receiverId] = hours[receiverId] + shiftHours
        };
        var after = afterHours.Values.Sum(h => Math.Abs(h - afterHours.Values.Average()));
        return after < before - 0.01;
    }

    private static double EstimateShiftHours(
        SaShiftAssignment assignment,
        IReadOnlyDictionary<int, ProductivityWorkedHoursCalculator.ShiftWorkInfo> lookup,
        ShiftConstraints constraints)
    {
        var duration = lookup.TryGetValue(assignment.ShiftId, out var info) && info.DurationHours > 0
            ? info.DurationHours
            : 8;
        var weighted = ProductivityWorkedHoursCalculator.DefaultHandoverHours +
                       (constraints.IsHoliday(assignment.Date) || assignment.ShiftLabel == ShiftLabel.Night
                           ? duration * ProductivityWorkedHoursCalculator.DefaultNightHolidayMultiplier
                           : duration);
        return weighted;
    }

    private static double CalculateWorked(
        ShiftSolution solution,
        int userId,
        IReadOnlyDictionary<int, ProductivityWorkedHoursCalculator.ShiftWorkInfo> lookup,
        ShiftConstraints constraints) =>
        ProductivityWorkedHoursCalculator.CalculateEffectiveWorkedHours(
            solution.GetUserAllAssignments(userId), lookup, constraints.IsHoliday);

    private static double GetDeficit(UserConstraint user, double worked)
    {
        if (!user.ProductivityRequiredHours.HasValue)
        {
            return 0;
        }

        return Math.Max(0, (double)user.ProductivityRequiredHours.Value - worked);
    }

    private static bool IsProtectedUser(UserConstraint user) =>
        user.RequiredShiftSlots.Count > 0 || user.RequiredPresenceDates.Count > 0;

    private static bool IsProtectedAssignment(ShiftConstraints constraints, SaShiftAssignment assignment)
    {
        var user = constraints.UserConstraints.FirstOrDefault(u => u.UserId == assignment.UserId);
        if (user == null)
        {
            return false;
        }

        return user.RequiredShiftSlots.Any(s =>
            s.Date.Date == assignment.Date.Date && s.ShiftLabel == assignment.ShiftLabel);
    }
}
