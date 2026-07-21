using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;
using static ShiftYar.Domain.Enums.UserModel.UserEnums;

namespace ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;

/// <summary>
/// تلاش برای نزدیک کردن ساعات مؤثر کاربران به سقف موظفی و تعادل بین پرسنل،
/// بدون ایجاد تراکم هفتگی/روزهای متوالی و بدون به‌هم‌ریختن تعادل صبح/عصر.
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
        BalanceMorningEveningPeers(solution, constraints, lookup, productivityUsers);
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
                         .Where(a => ExactNightQuotaGuard.CanDonateNight(solution, constraints, donor, a))
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
        var allDates = Enumerable.Range(0, (constraints.EndDate.Date - constraints.StartDate.Date).Days + 1)
            .Select(i => constraints.StartDate.Date.AddDays(i))
            .ToList();

        foreach (var user in productivityUsers.OrderByDescending(u =>
                     GetDeficit(u, CalculateWorked(solution, u.UserId, lookup, constraints))))
        {
            for (var attempt = 0; attempt < allDates.Count * 2; attempt++)
            {
                var deficit = GetDeficit(user, CalculateWorked(solution, user.UserId, lookup, constraints));
                if (deficit <= DeficitToleranceHours)
                {
                    break;
                }

                var date = PickBestFillDate(solution, constraints, user, allDates);
                if (date == null)
                {
                    break;
                }

                var added = false;
                // اول تک‌برچسب برای جلوگیری از پر کردن هر روز با صبح+عصر
                foreach (var label in PreferLabelsForUser(solution, user))
                {
                    if (TryAddLabel(solution, constraints, lookup, user, date.Value, label))
                    {
                        added = true;
                        break;
                    }
                }

                // فقط اگر هنوز کسری زیاد است و روز خالی مانده، صبح+عصر همان روز
                if (!added && deficit > 10)
                {
                    var m = TryAddLabel(solution, constraints, lookup, user, date.Value, ShiftLabel.Morning);
                    var e = TryAddLabel(solution, constraints, lookup, user, date.Value, ShiftLabel.Evening);
                    added = m || e;
                }

                if (!added)
                {
                    // این تاریخ را دیگر امتحان نکن
                    allDates.Remove(date.Value);
                    if (allDates.Count == 0)
                    {
                        break;
                    }
                }
            }
        }
    }

    private static IEnumerable<ShiftLabel> PreferLabelsForUser(ShiftSolution solution, UserConstraint user)
    {
        var ua = solution.GetUserAllAssignments(user.UserId);
        var m = ua.Count(a => a.ShiftLabel == ShiftLabel.Morning && !a.IsOnCall);
        var e = ua.Count(a => a.ShiftLabel == ShiftLabel.Evening && !a.IsOnCall);
        if (m <= e)
        {
            return [ShiftLabel.Morning, ShiftLabel.Evening, ShiftLabel.Night];
        }

        return [ShiftLabel.Evening, ShiftLabel.Morning, ShiftLabel.Night];
    }

    private static DateTime? PickBestFillDate(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        List<DateTime> dates)
    {
        var workDates = solution.GetUserAllAssignments(user.UserId)
            .Where(a => !a.IsOnCall)
            .Select(a => a.Date.Date)
            .ToHashSet();

        return dates
            .OrderBy(d => ScoreFillDate(workDates, user, d))
            .ThenBy(d => CountLabelOnDate(solution, d, ShiftLabel.Morning) + CountLabelOnDate(solution, d, ShiftLabel.Evening))
            .Select(d => (DateTime?)d)
            .FirstOrDefault();
    }

    private static int ScoreFillDate(HashSet<DateTime> workDates, UserConstraint user, DateTime date)
    {
        if (workDates.Contains(date.Date))
        {
            return 0; // همان روز قبلاً شیفت دارد — برای تکمیل صبح/عصر خوب است
        }

        var prevRun = 0;
        var cursor = date.Date.AddDays(-1);
        while (workDates.Contains(cursor))
        {
            prevRun++;
            cursor = cursor.AddDays(-1);
        }

        var nextRun = 0;
        cursor = date.Date.AddDays(1);
        while (workDates.Contains(cursor))
        {
            nextRun++;
            cursor = cursor.AddDays(1);
        }

        var projectedRun = prevRun + 1 + nextRun;
        var weekCount = workDates.Count(d => SameWeek(d, date)) + 1;
        var score = 0;
        if (projectedRun > user.MaxConsecutiveShifts)
        {
            score += 1000 + projectedRun * 50;
        }
        else
        {
            score += projectedRun * 10;
        }

        if (weekCount > user.MaxShiftsPerWeek)
        {
            score += 500 + weekCount * 40;
        }
        else
        {
            score += weekCount * 5;
        }

        // فاصله از نزدیک‌ترین روز کاری موجود — هرچه دورتر بهتر
        if (workDates.Count > 0)
        {
            var minGap = workDates.Min(d => Math.Abs((d - date.Date).Days));
            score -= Math.Min(minGap, 7) * 3;
        }

        return score;
    }

    private static int CountLabelOnDate(ShiftSolution solution, DateTime date, ShiftLabel label) =>
        solution.Assignments.Values.Count(a => a.Date.Date == date.Date && a.ShiftLabel == label && !a.IsOnCall);

    private static void BalanceMorningEveningPeers(
        ShiftSolution solution,
        ShiftConstraints constraints,
        IReadOnlyDictionary<int, ProductivityWorkedHoursCalculator.ShiftWorkInfo> lookup,
        List<UserConstraint> productivityUsers)
    {
        foreach (var label in new[] { ShiftLabel.Morning, ShiftLabel.Evening })
        {
            for (var pass = 0; pass < 24; pass++)
            {
                var ranked = productivityUsers
                    .Where(u => ShiftEligibilityResolver.IsLabelAllowed(u.AllowedShiftLabels, label))
                    .Select(u => (
                        User: u,
                        Count: solution.GetUserAllAssignments(u.UserId).Count(a => a.ShiftLabel == label && !a.IsOnCall)))
                    .OrderByDescending(x => x.Count)
                    .ToList();
                if (ranked.Count < 2)
                {
                    break;
                }

                var donor = ranked.First();
                var receiver = ranked.Last();
                if (donor.Count - receiver.Count < 2)
                {
                    break;
                }

                var moved = false;
                foreach (var assignment in solution.GetUserAllAssignments(donor.User.UserId)
                             .Where(a => a.ShiftLabel == label && !a.IsOnCall)
                             .Where(a => !IsProtectedAssignment(constraints, a))
                             .OrderByDescending(a => CountConsecutiveEnding(solution, donor.User.UserId, a.Date.Date)))
                {
                    if (!CanUserTakeShift(solution, constraints, lookup, receiver.User, assignment, ignoreShiftId: null))
                    {
                        continue;
                    }

                    // جابه‌جایی نباید کسری شدید ساعت برای اهداکننده بسازد
                    var donorHours = CalculateWorked(solution, donor.User.UserId, lookup, constraints);
                    var shiftHours = EstimateShiftHours(assignment, lookup, constraints);
                    if (donor.User.ProductivityRequiredHours.HasValue &&
                        donorHours - shiftHours < (double)donor.User.ProductivityRequiredHours.Value - 8)
                    {
                        continue;
                    }

                    solution.RemoveAssignment(donor.User.UserId, assignment.ShiftId, assignment.Date);
                    solution.AddAssignment(
                        receiver.User.UserId,
                        assignment.ShiftId,
                        assignment.Date,
                        assignment.ShiftLabel,
                        assignment.IsOnCall);
                    moved = true;
                    break;
                }

                if (!moved)
                {
                    break;
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

        // سقف هفته/متوالی فقط ترجیح نرم در ScoreFillDate است؛ اینجا بلاک سخت نمی‌کنیم
        // تا پر کردن موظفی و پوشش ظرفیت مختل نشود.

        if (assignment.ShiftLabel == ShiftLabel.Night)
        {
            var nights = solution.GetUserAllAssignments(user.UserId)
                .Where(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall)
                .ToList();

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

    private static bool SameWeek(DateTime a, DateTime b)
    {
        // هفته از شنبه — هم‌تراز GetWeekNumber در SimulatedAnnealingScheduler
        static DateTime WeekStart(DateTime d)
        {
            var offset = ((int)d.DayOfWeek - (int)DayOfWeek.Saturday + 7) % 7;
            return d.Date.AddDays(-offset);
        }

        return WeekStart(a) == WeekStart(b);
    }

    private static int CountConsecutiveEnding(ShiftSolution solution, int userId, DateTime endDate)
    {
        var workDates = solution.GetUserAllAssignments(userId)
            .Where(a => !a.IsOnCall)
            .Select(a => a.Date.Date)
            .ToHashSet();
        var count = 0;
        var cursor = endDate.Date;
        while (workDates.Contains(cursor))
        {
            count++;
            cursor = cursor.AddDays(-1);
        }

        return count;
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
