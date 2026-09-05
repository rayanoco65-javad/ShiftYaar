using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;
using static ShiftYar.Domain.Enums.UserModel.UserEnums;

namespace ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;

/// <summary>
/// پخش عادلانهٔ شیفت صبح/عصر روزهای تعطیل بین پرسنل گردشی.
/// بدون این پاس، تعادل ماهانهٔ M/E می‌تواند همزمان با تمرکز تعطیلات روی چند نفر برقرار باشد.
/// </summary>
public static class HolidayMorningEveningFairnessGuard
{
    public static void Enforce(ShiftSolution solution, ShiftConstraints constraints)
    {
        var holidayDates = Enumerable.Range(0, (constraints.EndDate.Date - constraints.StartDate.Date).Days + 1)
            .Select(i => constraints.StartDate.Date.AddDays(i))
            .Where(constraints.IsHoliday)
            .ToList();

        if (holidayDates.Count == 0)
        {
            return;
        }

        foreach (var specialtyGroup in constraints.UserConstraints
                     .Where(u => u.IsActive && u.ShiftType != ShiftTypes.FixedShift)
                     .GroupBy(u => u.SpecialtyId))
        {
            var peers = specialtyGroup.ToList();
            if (peers.Count < 2)
            {
                continue;
            }

            var lookup = ProductivityWorkedHoursCalculator.BuildShiftInfoLookup(constraints.ShiftRequirements);
            foreach (var label in new[] { ShiftLabel.Morning, ShiftLabel.Evening })
            {
                RedistributeLabel(solution, constraints, lookup, peers, holidayDates, label);
            }
        }
    }

    private static void RedistributeLabel(
        ShiftSolution solution,
        ShiftConstraints constraints,
        IReadOnlyDictionary<int, ProductivityWorkedHoursCalculator.ShiftWorkInfo> lookup,
        List<UserConstraint> peers,
        List<DateTime> holidayDates,
        ShiftLabel label)
    {
        var eligible = peers
            .Where(u => ShiftEligibilityResolver.MayEverTakeLabel(u, label))
            .ToList();
        if (eligible.Count < 2)
        {
            return;
        }

        var totalHolidayShifts = eligible.Sum(u => CountHolidayLabel(solution, constraints, u.UserId, label));
        if (totalHolidayShifts == 0)
        {
            return;
        }

        var fairShares = BuildHolidayFairShares(eligible, totalHolidayShifts, constraints.StartDate);

        for (var pass = 0; pass < 64; pass++)
        {
            var userStats = eligible
                .Select(u =>
                {
                    var count = CountHolidayLabel(solution, constraints, u.UserId, label);
                    var worked = CalculateWorked(solution, u.UserId, lookup, constraints);
                    var deficit = GetDeficit(u, worked);
                    var surplus = GetSurplus(u, worked);
                    var fair = fairShares.TryGetValue(u.UserId, out var f) ? f : 0;
                    var years = ResolveSeniorityYears(u, constraints.StartDate);
                    return (User: u, Count: count, Fair: fair, Years: years, Worked: worked, Deficit: deficit, Surplus: surplus);
                })
                .ToList();

            var donorCandidates = userStats
                .Where(x => x.Count > 0)
                .Where(x => x.Count > x.Fair + 0.35 || x.Surplus > 2.0 || x.Count >= 1)
                .OrderByDescending(x => x.Surplus > 5.0 ? 1 : 0)
                .ThenByDescending(x => x.Count - x.Fair)
                .ThenBy(x => x.Years)
                .ThenByDescending(x => x.Surplus)
                .ToList();

            if (donorCandidates.Count == 0)
            {
                break;
            }

            var moved = false;
            foreach (var donorEntry in donorCandidates)
            {
                var donorHolidayAssignments = solution.GetUserAllAssignments(donorEntry.User.UserId)
                    .Where(a => !a.IsOnCall && a.ShiftLabel == label && constraints.IsHoliday(a.Date))
                    .Where(a => !IsRequestProtected(donorEntry.User, a))
                    .OrderByDescending(a => a.Date)
                    .ToList();

                if (donorHolidayAssignments.Count == 0)
                {
                    continue;
                }

                var receiverCandidates = userStats
                    .Where(x => x.User.UserId != donorEntry.User.UserId)
                    .Where(x => x.Deficit > 1.0 || x.Fair - x.Count > 0.35 || (x.Years > donorEntry.Years && x.Count <= donorEntry.Count - 1))
                    .OrderByDescending(x => x.Deficit > 2.0 ? 1 : 0)
                    .ThenByDescending(x => x.Deficit)
                    .ThenByDescending(x => x.Fair - x.Count)
                    .ThenByDescending(x => x.Years)
                    .ToList();

                foreach (var receiverEntry in receiverCandidates)
                {
                    foreach (var assignment in donorHolidayAssignments)
                    {
                        if (solution.HasAssignment(receiverEntry.User.UserId, assignment.ShiftId, assignment.Date))
                        {
                            continue;
                        }

                        if (!CanTakeHolidayAssignment(
                                solution, constraints, donorEntry.User, receiverEntry.User, assignment))
                        {
                            continue;
                        }

                        var isSkeleton = solution.IsLockedSkeleton(donorEntry.User.UserId, assignment.ShiftId, assignment.Date)
                                         || assignment.IsSkeleton;

                        solution.RemoveAssignment(donorEntry.User.UserId, assignment.ShiftId, assignment.Date);
                        solution.AddAssignment(
                            receiverEntry.User.UserId,
                            assignment.ShiftId,
                            assignment.Date,
                            assignment.ShiftLabel,
                            assignment.IsOnCall);

                        if (isSkeleton)
                        {
                            var newAsg = solution.GetUserAssignments(receiverEntry.User.UserId, assignment.Date)
                                .FirstOrDefault(a => a.ShiftId == assignment.ShiftId);
                            if (newAsg != null)
                            {
                                newAsg.IsSkeleton = true;
                            }
                        }

                        moved = true;
                        break;
                    }

                    if (moved)
                    {
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
                break;
            }
        }
    }

    private static Dictionary<int, double> BuildHolidayFairShares(
        List<UserConstraint> eligible,
        int totalShifts,
        DateTime referenceDate,
        double slope = 1.0)
    {
        var weights = eligible.ToDictionary(
            u => u.UserId,
            u =>
            {
                var years = Math.Clamp(ResolveSeniorityYears(u, referenceDate), 0, 40);
                return Math.Pow(Math.Max(1, years + 1), Math.Max(0.1, slope));
            });

        var totalWeight = weights.Values.Sum();
        if (totalWeight <= 0)
        {
            var equal = totalShifts / (double)eligible.Count;
            return eligible.ToDictionary(u => u.UserId, _ => equal);
        }

        return eligible.ToDictionary(
            u => u.UserId,
            u => totalShifts * weights[u.UserId] / totalWeight);
    }

    private static int ResolveSeniorityYears(UserConstraint user, DateTime referenceDate)
    {
        if (user.ExperienceYears > 0)
        {
            return user.ExperienceYears;
        }

        if (user.DateOfEmployment.HasValue)
        {
            var normalized = ShiftYar.Domain.Entities.ProductivityModel.StaffEmploymentInfo.NormalizeEmploymentDate(user.DateOfEmployment.Value);
            var totalMonths = (referenceDate.Year - normalized.Year) * 12 + (referenceDate.Month - normalized.Month);
            return totalMonths > 0 ? (int)(totalMonths / 12) : 0;
        }

        return 0;
    }

    private static double CalculateWorked(
        ShiftSolution solution,
        int userId,
        IReadOnlyDictionary<int, ProductivityWorkedHoursCalculator.ShiftWorkInfo> lookup,
        ShiftConstraints constraints) =>
        ProductivityWorkedHoursCalculator.CalculateEffectiveWorkedHours(
            solution.GetUserAllAssignments(userId),
            lookup,
            constraints.IsHoliday,
            ProductivityWorkedHoursCalculator.BuildProductivityPlanLookup(constraints.UserConstraints));

    private static double GetDeficit(UserConstraint user, double worked)
    {
        if (!user.ProductivityRequiredHours.HasValue)
        {
            return 0;
        }

        return Math.Max(0, (double)user.ProductivityRequiredHours.Value - worked);
    }

    private static double GetSurplus(UserConstraint user, double worked)
    {
        if (!user.ProductivityRequiredHours.HasValue)
        {
            return 0;
        }

        return Math.Max(0, worked - (double)user.ProductivityRequiredHours.Value);
    }

    private static bool CanTakeHolidayAssignment(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint donor,
        UserConstraint receiver,
        SaShiftAssignment assignment)
    {
        if (!CanTakeHolidayMe(solution, constraints, receiver, assignment.Date, assignment.ShiftLabel, assignment.ShiftId))
        {
            return false;
        }

        var isSkeleton = solution.IsLockedSkeleton(donor.UserId, assignment.ShiftId, assignment.Date) || assignment.IsSkeleton;
        if (isSkeleton)
        {
            var shiftReq = constraints.ShiftRequirements.FirstOrDefault(s => s.ShiftId == assignment.ShiftId);
            if (shiftReq != null && ShiftManagerRules.RequiresAnyManager(shiftReq))
            {
                var isDonorLevel1 = ShiftManagerRules.IsLevel1(donor);
                if (isDonorLevel1 && !ShiftManagerRules.IsLevel1(receiver))
                {
                    return false;
                }

                if (!ShiftManagerRules.IsManager(receiver))
                {
                    return false;
                }
            }
        }

        return true;
    }

    public static int CountHolidayLabel(
        ShiftSolution solution,
        ShiftConstraints constraints,
        int userId,
        ShiftLabel label) =>
        solution.GetUserAllAssignments(userId)
            .Count(a => !a.IsOnCall && a.ShiftLabel == label && constraints.IsHoliday(a.Date));

    private static bool CanTakeHolidayMe(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        DateTime date,
        ShiftLabel label,
        int shiftId)
    {
        if (user.UnavailableDates.Any(d => d.Date == date.Date))
        {
            return false;
        }

        if (user.UnavailableShiftSlots.Any(s => s.Date.Date == date.Date && s.ShiftLabel == label))
        {
            return false;
        }

        if (solution.HasAssignment(user.UserId, shiftId, date))
        {
            return false;
        }

        // روی تعطیل معمولاً شب دارند یا استراحت مجاور — احترام بگذار
        var existing = solution.GetUserAssignments(user.UserId, date)
            .Where(a => !(a.ShiftId == shiftId && a.Date.Date == date.Date))
            .Select(a => a.ShiftLabel);
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
                solution.GetUserAllAssignments(user.UserId),
                date,
                label,
                constraints))
        {
            return false;
        }

        return !MaxConsecutiveWorkdayRules.WouldExceedMaxConsecutiveWorkdays(
            solution, constraints, user, date);
    }

    private static bool IsRequestProtected(UserConstraint user, SaShiftAssignment assignment)
    {
        if (user.RequiredShiftSlots.Any(s =>
                s.Date.Date == assignment.Date.Date && s.ShiftLabel == assignment.ShiftLabel))
        {
            return true;
        }

        return !assignment.IsOnCall &&
               user.RequiredPresenceDates.Any(d => d.Date == assignment.Date.Date);
    }
}
