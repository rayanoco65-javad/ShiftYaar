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
    private const int ReassignmentPasses = 96;
    private const int FinalBalancePasses = 3;
    private const int CrossTierReassignmentPasses = 128;

    public static void Enforce(ShiftSolution solution, ShiftConstraints constraints)
    {
        var lookup = ProductivityWorkedHoursCalculator.BuildShiftInfoLookup(constraints.ShiftRequirements);
        var productivityUsers = GetProductivityUsers(constraints);
        if (productivityUsers.Count == 0)
        {
            return;
        }

        RunProductivityFillPhases(solution, constraints, lookup, productivityUsers);
        EnforceCrossTierPriorityBalance(solution, constraints, lookup, productivityUsers);
        StripProjectPersonnelOvertime(solution, constraints, lookup, productivityUsers);
        EnforceCrossTierPriorityBalance(solution, constraints, lookup, productivityUsers);
    }

    /// <summary>
    /// پس از ForceApply و ظرفیت: جابجایی ساعات از مازاد به کسری بدون دست‌زدن به ON تأییدشده.
    /// </summary>
    public static void EnforceFinalBalance(ShiftSolution solution, ShiftConstraints constraints)
    {
        var lookup = ProductivityWorkedHoursCalculator.BuildShiftInfoLookup(constraints.ShiftRequirements);
        var productivityUsers = GetProductivityUsers(constraints);
        if (productivityUsers.Count == 0)
        {
            return;
        }

        for (var pass = 0; pass < FinalBalancePasses; pass++)
        {
            RunProductivityFillPhases(solution, constraints, lookup, productivityUsers);
            EnforceCrossTierPriorityBalance(solution, constraints, lookup, productivityUsers);
            StripProjectPersonnelOvertime(solution, constraints, lookup, productivityUsers);
            EnforceCrossTierPriorityBalance(solution, constraints, lookup, productivityUsers);
        }

        ShiftCoverageGuard.StripExcessCoverage(solution, constraints);
        ApprovedRequestGuard.ForceApply(solution, constraints);
    }

    private static List<UserConstraint> GetProductivityUsers(ShiftConstraints constraints) =>
        constraints.UserConstraints
            .Where(u => u.IsActive && u.ShiftType != ShiftTypes.FixedShift)
            .Where(u => u.IncludedInProductivityPlan && u.ProductivityRequiredHours.HasValue)
            .ToList();

    private static void RunProductivityFillPhases(
        ShiftSolution solution,
        ShiftConstraints constraints,
        IReadOnlyDictionary<int, ProductivityWorkedHoursCalculator.ShiftWorkInfo> lookup,
        List<UserConstraint> productivityUsers)
    {
        var nonProject = productivityUsers
            .Where(u => !ProjectPersonnelProductivityPriority.IsProjectPersonnel(u))
            .ToList();
        var project = productivityUsers
            .Where(u => ProjectPersonnelProductivityPriority.IsProjectPersonnel(u))
            .ToList();

        if (nonProject.Count > 0)
        {
            RunBalanceCycle(
                solution,
                constraints,
                lookup,
                nonProject,
                productivityUsers,
                ProjectPersonnelProductivityPriority.CrossTierToleranceHours);
            EnforceCrossTierPriorityBalance(solution, constraints, lookup, productivityUsers);
        }

        if (project.Count > 0 && !HasNonProjectDeficit(solution, lookup, constraints, nonProject))
        {
            RunBalanceCycle(
                solution,
                constraints,
                lookup,
                project,
                productivityUsers,
                DeficitToleranceHours);
        }
    }

    /// <summary>
    /// تا وقتی غیرطرحی کسری دارد: از مازاد پرسنل طرحی به آن‌ها شیفت بدهید یا جای خالی پر کنید.
    /// </summary>
    private static void EnforceCrossTierPriorityBalance(
        ShiftSolution solution,
        ShiftConstraints constraints,
        IReadOnlyDictionary<int, ProductivityWorkedHoursCalculator.ShiftWorkInfo> lookup,
        List<UserConstraint> productivityUsers)
    {
        var nonProject = productivityUsers
            .Where(u => !ProjectPersonnelProductivityPriority.IsProjectPersonnel(u))
            .ToList();
        var project = productivityUsers
            .Where(u => ProjectPersonnelProductivityPriority.IsProjectPersonnel(u))
            .ToList();
        if (nonProject.Count == 0 || project.Count == 0)
        {
            return;
        }

        var crossTier = ProjectPersonnelProductivityPriority.CrossTierToleranceHours;
        for (var pass = 0; pass < CrossTierReassignmentPasses; pass++)
        {
            if (!HasNonProjectDeficit(solution, lookup, constraints, nonProject, crossTier))
            {
                return;
            }

            var hours = productivityUsers.ToDictionary(
                u => u.UserId,
                u => CalculateWorked(solution, u.UserId, lookup, constraints));

            var receiver = nonProject
                .Where(u => GetDeficit(u, hours[u.UserId]) > crossTier)
                .OrderByDescending(u => GetDeficit(u, hours[u.UserId]))
                .FirstOrDefault();
            if (receiver == null)
            {
                return;
            }

            FillUnderstaffedSlots(
                solution,
                constraints,
                lookup,
                [receiver],
                crossTier);

            hours = productivityUsers.ToDictionary(
                u => u.UserId,
                u => CalculateWorked(solution, u.UserId, lookup, constraints));

            if (GetDeficit(receiver, hours[receiver.UserId]) <= crossTier)
            {
                continue;
            }

            var donor = project
                .Where(u => hours[u.UserId] > (double)u.ProductivityRequiredHours!.Value + crossTier)
                .OrderByDescending(u => hours[u.UserId] - (double)u.ProductivityRequiredHours!.Value)
                .ThenByDescending(u => hours[u.UserId])
                .FirstOrDefault();
            if (donor == null)
            {
                return;
            }

            var moved = false;
            foreach (var assignment in solution.GetUserAllAssignments(donor.UserId)
                         .Where(a => !a.IsOnCall && !IsProtectedAssignment(constraints, a))
                         .Where(a => ExactNightQuotaGuard.CanDonateNight(solution, constraints, donor, a))
                         .OrderBy(a => DonationPriority(a))
                         .ThenByDescending(a => EstimateShiftHours(a, lookup, constraints)))
            {
                if (!CanUserTakeShift(solution, constraints, lookup, receiver, assignment, ignoreShiftId: null))
                {
                    continue;
                }

                var shiftHours = EstimateShiftHours(assignment, lookup, constraints);
                if (!IsBeneficialCrossTierMove(
                        donor,
                        receiver,
                        hours[donor.UserId],
                        hours[receiver.UserId],
                        shiftHours,
                        crossTier))
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

    /// <summary>
    /// پرسنل طرحی نباید بالاتر از ساعت موظفی بمانند؛ مازاد به غیرطرحی منتقل یا حذف می‌شود.
    /// </summary>
    private static void StripProjectPersonnelOvertime(
        ShiftSolution solution,
        ShiftConstraints constraints,
        IReadOnlyDictionary<int, ProductivityWorkedHoursCalculator.ShiftWorkInfo> lookup,
        List<UserConstraint> productivityUsers)
    {
        var project = productivityUsers
            .Where(ProjectPersonnelProductivityPriority.IsProjectPersonnel)
            .ToList();
        if (project.Count == 0)
        {
            return;
        }

        var nonProject = productivityUsers
            .Where(u => !ProjectPersonnelProductivityPriority.IsProjectPersonnel(u))
            .ToList();
        var crossTier = ProjectPersonnelProductivityPriority.CrossTierToleranceHours;

        for (var pass = 0; pass < CrossTierReassignmentPasses; pass++)
        {
            var changed = false;
            foreach (var user in project.OrderByDescending(u =>
                         CalculateWorked(solution, u.UserId, lookup, constraints) -
                         (double)u.ProductivityRequiredHours!.Value))
            {
                var worked = CalculateWorked(solution, user.UserId, lookup, constraints);
                var required = (double)user.ProductivityRequiredHours!.Value;
                if (worked <= required + crossTier)
                {
                    continue;
                }

                foreach (var assignment in solution.GetUserAllAssignments(user.UserId)
                             .Where(a => !a.IsOnCall && !IsProtectedAssignment(constraints, a))
                             .Where(a => ExactNightQuotaGuard.CanDonateNight(solution, constraints, user, a))
                             .OrderBy(a => DonationPriority(a))
                             .ThenByDescending(a => EstimateShiftHours(a, lookup, constraints)))
                {
                    var receiver = nonProject
                        .Where(u => GetDeficit(u, CalculateWorked(solution, u.UserId, lookup, constraints)) > crossTier)
                        .OrderByDescending(u => GetDeficit(u, CalculateWorked(solution, u.UserId, lookup, constraints)))
                        .FirstOrDefault(u =>
                            CanUserTakeShift(solution, constraints, lookup, u, assignment, ignoreShiftId: null));

                    if (receiver != null)
                    {
                        solution.RemoveAssignment(user.UserId, assignment.ShiftId, assignment.Date);
                        solution.AddAssignment(
                            receiver.UserId,
                            assignment.ShiftId,
                            assignment.Date,
                            assignment.ShiftLabel,
                            assignment.IsOnCall);
                    }
                    else
                    {
                        solution.RemoveAssignment(user.UserId, assignment.ShiftId, assignment.Date);
                    }

                    changed = true;
                    break;
                }
            }

            if (!changed)
            {
                return;
            }
        }
    }

    private static bool HasNonProjectDeficit(
        ShiftSolution solution,
        IReadOnlyDictionary<int, ProductivityWorkedHoursCalculator.ShiftWorkInfo> lookup,
        ShiftConstraints constraints,
        List<UserConstraint> nonProject,
        double tolerance = ProjectPersonnelProductivityPriority.CrossTierToleranceHours) =>
        nonProject.Any(u =>
            GetDeficit(u, CalculateWorked(solution, u.UserId, lookup, constraints)) > tolerance);

    private static bool IsBeneficialCrossTierMove(
        UserConstraint donor,
        UserConstraint receiver,
        double donorWorked,
        double receiverWorked,
        double shiftHours,
        double crossTierTolerance)
    {
        if (!ProjectPersonnelProductivityPriority.IsProjectPersonnel(donor) ||
            ProjectPersonnelProductivityPriority.IsProjectPersonnel(receiver))
        {
            return false;
        }

        if (!donor.ProductivityRequiredHours.HasValue || !receiver.ProductivityRequiredHours.HasValue)
        {
            return false;
        }

        var donorRequired = (double)donor.ProductivityRequiredHours.Value;
        var receiverRequired = (double)receiver.ProductivityRequiredHours.Value;
        var donorAfter = donorWorked - shiftHours;
        if (donorAfter < donorRequired - crossTierTolerance)
        {
            return false;
        }

        var deficitBefore = Math.Max(0, receiverRequired - receiverWorked);
        var deficitAfter = Math.Max(0, receiverRequired - (receiverWorked + shiftHours));
        return deficitAfter + 0.01 < deficitBefore;
    }

    private static void RunBalanceCycle(
        ShiftSolution solution,
        ShiftConstraints constraints,
        IReadOnlyDictionary<int, ProductivityWorkedHoursCalculator.ShiftWorkInfo> lookup,
        List<UserConstraint> targetUsers,
        List<UserConstraint> productivityUsers,
        double fillDeficitStopTolerance)
    {
        BalanceByReassignment(solution, constraints, lookup, targetUsers, productivityUsers);
        FillUnderstaffedSlots(solution, constraints, lookup, targetUsers, fillDeficitStopTolerance);
        BalanceMorningEveningPeers(solution, constraints, lookup, targetUsers);
        BalanceShiftLabelOverload(solution, constraints, lookup, targetUsers, productivityUsers);
        BalanceByReassignment(solution, constraints, lookup, targetUsers, productivityUsers);
    }

    private static void BalanceByReassignment(
        ShiftSolution solution,
        ShiftConstraints constraints,
        IReadOnlyDictionary<int, ProductivityWorkedHoursCalculator.ShiftWorkInfo> lookup,
        List<UserConstraint> targetUsers,
        List<UserConstraint> productivityUsers)
    {
        for (var pass = 0; pass < ReassignmentPasses; pass++)
        {
            var hours = productivityUsers.ToDictionary(
                u => u.UserId,
                u => CalculateWorked(solution, u.UserId, lookup, constraints));

            var donor = productivityUsers
                .Where(u => GetSurplusHours(u, hours[u.UserId]) > 0)
                .OrderByDescending(u => GetSurplusHours(u, hours[u.UserId]))
                .ThenByDescending(u => ProjectPersonnelProductivityPriority.FillTier(u))
                .ThenByDescending(u => hours[u.UserId])
                .FirstOrDefault();

            var receiver = targetUsers
                .Where(u => GetDeficit(u, hours[u.UserId]) > DeficitToleranceHours)
                .OrderBy(u => ProjectPersonnelProductivityPriority.FillTier(u))
                .ThenByDescending(u => GetDeficit(u, hours[u.UserId]))
                .FirstOrDefault();

            if (donor == null || receiver == null)
            {
                return;
            }

            var moved = false;
            foreach (var assignment in solution.GetUserAllAssignments(donor.UserId)
                         .Where(a => !a.IsOnCall && !IsProtectedAssignment(constraints, a))
                         .Where(a => ExactNightQuotaGuard.CanDonateNight(solution, constraints, donor, a))
                         .OrderBy(a => DonationPriority(a))
                         .ThenByDescending(a => EstimateShiftHours(a, lookup, constraints)))
            {
                if (!CanUserTakeShift(solution, constraints, lookup, receiver, assignment, ignoreShiftId: null))
                {
                    continue;
                }

                if (!WouldImproveRatioBalance(hours, productivityUsers, donor, receiver, assignment, lookup, constraints))
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

    private static int DonationPriority(SaShiftAssignment assignment) =>
        assignment.ShiftLabel switch
        {
            ShiftLabel.Morning => 0,
            ShiftLabel.Evening => 1,
            _ => 2
        };

    private static double GetSurplusHours(UserConstraint user, double worked)
    {
        if (!user.ProductivityRequiredHours.HasValue)
        {
            return 0;
        }

        return Math.Max(0, worked - (double)user.ProductivityRequiredHours.Value - DeficitToleranceHours);
    }

    private static double CalculateRatioSpread(
        IReadOnlyDictionary<int, double> hours,
        IEnumerable<UserConstraint> productivityUsers)
    {
        var ratios = productivityUsers
            .Select(u =>
            {
                var required = (double)u.ProductivityRequiredHours!.Value;
                if (!hours.TryGetValue(u.UserId, out var worked))
                {
                    return 0d;
                }

                return required > 0 ? worked / required : 0;
            })
            .ToList();

        if (ratios.Count < 2)
        {
            return 0;
        }

        var avg = ratios.Average();
        return ratios.Sum(r => Math.Abs(r - avg));
    }

    private static bool WouldImproveRatioBalance(
        Dictionary<int, double> hours,
        List<UserConstraint> ratioCohort,
        UserConstraint donor,
        UserConstraint receiver,
        SaShiftAssignment assignment,
        IReadOnlyDictionary<int, ProductivityWorkedHoursCalculator.ShiftWorkInfo> lookup,
        ShiftConstraints constraints)
    {
        var shiftHours = EstimateShiftHours(assignment, lookup, constraints);
        var before = CalculateRatioSpread(hours, ratioCohort);

        var beforeImbalance = CalculateTotalImbalance(hours, ratioCohort);

        var donorAfter = hours[donor.UserId] - shiftHours;
        if (donor.ProductivityRequiredHours.HasValue &&
            donorAfter < (double)donor.ProductivityRequiredHours.Value - DeficitToleranceHours)
        {
            return false;
        }

        var afterHours = new Dictionary<int, double>(hours)
        {
            [donor.UserId] = donorAfter,
            [receiver.UserId] = hours[receiver.UserId] + shiftHours
        };

        var receiverRequired = (double)receiver.ProductivityRequiredHours!.Value;
        if (receiverRequired > 0 &&
            afterHours[receiver.UserId] / receiverRequired > 1.15 &&
            !receiver.OvertimeConsent)
        {
            return false;
        }

        var after = CalculateRatioSpread(afterHours, ratioCohort);
        var afterImbalance = CalculateTotalImbalance(afterHours, ratioCohort);
        return after < before - 0.001 || afterImbalance < beforeImbalance - 0.25;
    }

    private static double CalculateTotalImbalance(
        IReadOnlyDictionary<int, double> hours,
        IEnumerable<UserConstraint> ratioCohort) =>
        ratioCohort.Sum(u =>
        {
            if (!hours.TryGetValue(u.UserId, out var worked))
            {
                return 0d;
            }

            return GetDeficit(u, worked) + GetSurplusHours(u, worked);
        });

    private static void FillUnderstaffedSlots(
        ShiftSolution solution,
        ShiftConstraints constraints,
        IReadOnlyDictionary<int, ProductivityWorkedHoursCalculator.ShiftWorkInfo> lookup,
        List<UserConstraint> targetUsers,
        double deficitStopTolerance = DeficitToleranceHours)
    {
        var allDates = Enumerable.Range(0, (constraints.EndDate.Date - constraints.StartDate.Date).Days + 1)
            .Select(i => constraints.StartDate.Date.AddDays(i))
            .ToList();

        foreach (var user in targetUsers
                     .OrderBy(u => ProjectPersonnelProductivityPriority.FillTier(u))
                     .ThenByDescending(u => GetDeficit(u, CalculateWorked(solution, u.UserId, lookup, constraints))))
        {
            for (var attempt = 0; attempt < allDates.Count * 2; attempt++)
            {
                var deficit = GetDeficit(user, CalculateWorked(solution, user.UserId, lookup, constraints));
                if (deficit <= deficitStopTolerance)
                {
                    break;
                }

                var date = PickBestFillDate(solution, constraints, user, allDates);
                if (date == null)
                {
                    break;
                }

                var added = false;
                foreach (var label in PreferLabelsForUser(solution, constraints, user, deficit))
                {
                    if (TryAddLabel(solution, constraints, lookup, user, date.Value, label))
                    {
                        added = true;
                        break;
                    }
                }

                if (!added && deficit > 10)
                {
                    var m = TryAddLabel(solution, constraints, lookup, user, date.Value, ShiftLabel.Morning);
                    var e = TryAddLabel(solution, constraints, lookup, user, date.Value, ShiftLabel.Evening);
                    added = m || e;
                }

                if (!added)
                {
                    allDates.Remove(date.Value);
                    if (allDates.Count == 0)
                    {
                        break;
                    }
                }
            }
        }
    }

    private static IEnumerable<ShiftLabel> PreferLabelsForUser(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        double deficitHours)
    {
        var ua = solution.GetUserAllAssignments(user.UserId);
        var m = ua.Count(a => a.ShiftLabel == ShiftLabel.Morning && !a.IsOnCall);
        var e = ua.Count(a => a.ShiftLabel == ShiftLabel.Evening && !a.IsOnCall);
        var n = ua.Count(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall);
        var limits = MorningEveningBalanceGuard.GetDepartmentSpreadLimits(
            solution,
            constraints.UserConstraints
                .Where(u => u.IsActive && u.ShiftType != ShiftTypes.FixedShift)
                .Where(u => ShiftEligibilityResolver.IsLabelAllowed(u.AllowedShiftLabels, ShiftLabel.Morning) &&
                            ShiftEligibilityResolver.IsLabelAllowed(u.AllowedShiftLabels, ShiftLabel.Evening)));

        // اگر کسری زیاد است، شب (ساعت مؤثر بیشتر) را زودتر امتحان کن
        if (deficitHours > 12 &&
            ShiftEligibilityResolver.IsLabelAllowed(user.AllowedShiftLabels, ShiftLabel.Night) &&
            (!user.HasExactNightQuota || n < user.ExactNightShiftCount))
        {
            return [ShiftLabel.Night, ShiftLabel.Evening, ShiftLabel.Morning];
        }

        var delta = m - e;
        if (delta > limits.MaxMorningSurplus)
        {
            return [ShiftLabel.Evening, ShiftLabel.Morning, ShiftLabel.Night];
        }

        if (-delta > limits.MaxEveningSurplus)
        {
            return [ShiftLabel.Morning, ShiftLabel.Evening, ShiftLabel.Night];
        }

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
            return 0;
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
        List<UserConstraint> targetUsers)
    {
        foreach (var label in new[] { ShiftLabel.Morning, ShiftLabel.Evening })
        {
            for (var pass = 0; pass < 24; pass++)
            {
                var ranked = targetUsers
                    .Where(u => ShiftEligibilityResolver.IsLabelAllowed(u.AllowedShiftLabels, label))
                    .Select(u => (
                        User: u,
                        Count: solution.GetUserAllAssignments(u.UserId).Count(a => a.ShiftLabel == label && !a.IsOnCall),
                        HolidayCount: HolidayMorningEveningFairnessGuard.CountHolidayLabel(
                            solution, constraints, u.UserId, label),
                        Worked: CalculateWorked(solution, u.UserId, lookup, constraints)))
                    .OrderByDescending(x => x.HolidayCount)
                    .ThenByDescending(x => GetSurplusHours(x.User, x.Worked))
                    .ThenByDescending(x => x.Count)
                    .ToList();
                if (ranked.Count < 2)
                {
                    break;
                }

                var donor = ranked.First();
                var receiver = ranked.Last();
                var holidayGap = donor.HolidayCount - receiver.HolidayCount;
                if (holidayGap < 2 && donor.Count - receiver.Count < 2 &&
                    GetSurplusHours(donor.User, donor.Worked) <= 0 &&
                    GetDeficit(receiver.User, receiver.Worked) <= DeficitToleranceHours)
                {
                    break;
                }

                var preferHoliday = holidayGap >= 2;
                var moved = false;
                foreach (var assignment in solution.GetUserAllAssignments(donor.User.UserId)
                             .Where(a => a.ShiftLabel == label && !a.IsOnCall)
                             .Where(a => !IsProtectedAssignment(constraints, a))
                             .Where(a => !preferHoliday || constraints.IsHoliday(a.Date))
                             .OrderByDescending(a => CountConsecutiveEnding(solution, donor.User.UserId, a.Date.Date)))
                {
                    if (!CanUserTakeShift(solution, constraints, lookup, receiver.User, assignment, ignoreShiftId: null))
                    {
                        continue;
                    }

                    var donorHours = CalculateWorked(solution, donor.User.UserId, lookup, constraints);
                    var shiftHours = EstimateShiftHours(assignment, lookup, constraints);
                    if (donor.User.ProductivityRequiredHours.HasValue &&
                        donorHours - shiftHours < (double)donor.User.ProductivityRequiredHours.Value - DeficitToleranceHours)
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

    /// <summary>
    /// کاهش تمرکز صبح/عصر روی یک نفر (مثلاً ۹ عصر) با جابجایی به کسری‌موظفی‌ها.
    /// </summary>
    private static void BalanceShiftLabelOverload(
        ShiftSolution solution,
        ShiftConstraints constraints,
        IReadOnlyDictionary<int, ProductivityWorkedHoursCalculator.ShiftWorkInfo> lookup,
        List<UserConstraint> targetUsers,
        List<UserConstraint> productivityUsers)
    {
        foreach (var label in new[] { ShiftLabel.Evening, ShiftLabel.Morning })
        {
            for (var pass = 0; pass < 32; pass++)
            {
                var snapshot = targetUsers
                    .Where(u => ShiftEligibilityResolver.IsLabelAllowed(u.AllowedShiftLabels, label))
                    .Select(u => (
                        User: u,
                        Count: solution.GetUserAllAssignments(u.UserId).Count(a => a.ShiftLabel == label && !a.IsOnCall),
                        Worked: CalculateWorked(solution, u.UserId, lookup, constraints)))
                    .ToList();
                if (snapshot.Count < 2)
                {
                    break;
                }

                var total = snapshot.Sum(x => x.Count);
                if (total == 0)
                {
                    break;
                }

                var weights = snapshot.ToDictionary(
                    x => x.User.UserId,
                    x => x.User.ProductivityRequiredHours.HasValue && x.User.ProductivityRequiredHours > 0
                        ? (double)x.User.ProductivityRequiredHours.Value
                        : 1.0);
                var totalWeight = weights.Values.Sum();
                if (totalWeight <= 0)
                {
                    totalWeight = snapshot.Count;
                }

                var scored = snapshot
                    .Select(x =>
                    {
                        var fair = total * weights[x.User.UserId] / totalWeight;
                        var surplus = GetSurplusHours(x.User, x.Worked);
                        var deficit = GetDeficit(x.User, x.Worked);
                        return (x.User, x.Count, fair, surplus, deficit);
                    })
                    .ToList();

                var donorEntry = scored
                    .Where(x => x.surplus > 0 || x.Count > x.fair + 1.5)
                    .OrderByDescending(x => x.Count - x.fair)
                    .ThenByDescending(x => x.surplus)
                    .ThenByDescending(x => ProjectPersonnelProductivityPriority.FillTier(x.User))
                    .FirstOrDefault();
                var receiverEntry = scored
                    .Where(x => x.deficit > DeficitToleranceHours || x.Count < x.fair - 1)
                    .OrderBy(x => ProjectPersonnelProductivityPriority.FillTier(x.User))
                    .ThenByDescending(x => x.deficit)
                    .ThenBy(x => x.Count)
                    .FirstOrDefault();

                if (donorEntry.User == null || receiverEntry.User == null ||
                    donorEntry.User.UserId == receiverEntry.User.UserId)
                {
                    break;
                }

                var moved = false;
                foreach (var assignment in solution.GetUserAllAssignments(donorEntry.User.UserId)
                             .Where(a => a.ShiftLabel == label && !a.IsOnCall)
                             .Where(a => !IsProtectedAssignment(constraints, a))
                             .Where(a => ExactNightQuotaGuard.CanDonateNight(solution, constraints, donorEntry.User, a))
                             .OrderByDescending(a => constraints.IsHoliday(a.Date)))
                {
                    if (!CanUserTakeShift(solution, constraints, lookup, receiverEntry.User, assignment, ignoreShiftId: null))
                    {
                        continue;
                    }

                    var hours = productivityUsers.ToDictionary(
                        u => u.UserId,
                        u => CalculateWorked(solution, u.UserId, lookup, constraints));
                    if (!WouldImproveRatioBalance(
                            hours, targetUsers, donorEntry.User, receiverEntry.User, assignment, lookup, constraints))
                    {
                        continue;
                    }

                    solution.RemoveAssignment(donorEntry.User.UserId, assignment.ShiftId, assignment.Date);
                    solution.AddAssignment(
                        receiverEntry.User.UserId,
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
                constraints,
                ignoreShiftId))
        {
            return false;
        }

        if (assignment.ShiftLabel == ShiftLabel.Night)
        {
            var nights = solution.GetUserAllAssignments(user.UserId)
                .Where(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall)
                .ToList();

            if (user.HasExactNightQuota &&
                nights.Count >= user.ExactNightShiftCount)
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
        return !ProjectPersonnelProductivityPriority.WouldExceedSchedulingCap(user, worked);
    }

    private static bool SameWeek(DateTime a, DateTime b)
    {
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
