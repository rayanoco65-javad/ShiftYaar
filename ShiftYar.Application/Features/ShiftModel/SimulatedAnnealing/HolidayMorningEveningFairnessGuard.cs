using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;
using static ShiftYar.Domain.Enums.UserModel.UserEnums;

namespace ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;

/// <summary>
/// مدیریت و پخش عادلانهٔ شیفت‌های روزهای تعطیل بین پرسنل گردشی بخش.
/// پشتیبانی از ۳ مود دپارتمان:
/// ۰ = تعداد مساوی (Equal)
/// ۱ = سابقه بیشتر شیفت تعطیل بیشتر (SeniorityDescending)
/// ۲ = سابقه بیشتر شیفت تعطیل کمتر (SeniorityAscending)
/// اولویت قطعی با درخواست‌های تأییدشده پرسنل (مرخصی/حضور) و سهمیه‌های انفرادی سوپروایزر است.
/// </summary>
public static class HolidayMorningEveningFairnessGuard
{
    private const double HourTolerance = 0.25;

    public static void Enforce(ShiftSolution solution, ShiftConstraints constraints)
    {
        if (!constraints.EnableHolidayShiftDistribution)
        {
            return;
        }

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
            .Where(u => ShiftEligibilityResolver.HasInherentPermission(u, label))
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

        var distributionType = constraints.HolidayShiftDistributionType;
        var slope = constraints.HolidaySeniorityDistributionSlope;
        var fairShares = BuildHolidayFairShares(eligible, totalHolidayShifts, constraints.StartDate, distributionType, slope, label);

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
                    var exactQuota = GetExactHolidayQuota(u, label);
                    var fallback = GetHolidayFallbackParticipation(u, label);
                    return (User: u, Count: count, Fair: fair, Years: years, Worked: worked, Deficit: deficit, Surplus: surplus, ExactQuota: exactQuota, Fallback: fallback);
                })
                .ToList();

            // انتخاب اهداکنندگان (Donors):
            // ۱. کاربرانی که سهمیه تعطیل‌شان ۰ است و شیفت دارند در بالاترین اولویت اهدا هستند
            // ۲. سایر کاربران بر اساس مود دپارتمان سورت می‌شوند
            var donorQuery = userStats
                .Where(x => x.Count > 0)
                .Where(x => x.ExactQuota == 0 || x.User.ExactHolidayShiftCount == 0 || !x.ExactQuota.HasValue || x.Count > x.ExactQuota.Value)
                .Where(x => x.ExactQuota == 0 || x.User.ExactHolidayShiftCount == 0 || x.Count > x.Fair + 0.35 || x.Surplus > 2.0 || (distributionType != 0 && x.Count >= 1));

            var donorCandidates = distributionType switch
            {
                1 => donorQuery // سابقه بیشتر شیفت بیشتر -> جوان‌ترها اهداکننده باشند
                    .OrderByDescending(x => (x.ExactQuota == 0 || x.User.ExactHolidayShiftCount == 0) ? 1 : 0)
                    .ThenBy(x => x.Years)
                    .ThenByDescending(x => x.Count - x.Fair)
                    .ThenByDescending(x => x.Surplus)
                    .ToList(),
                2 => donorQuery // سابقه بیشتر شیفت کمتر -> با‌سابقه‌ها اهداکننده باشند
                    .OrderByDescending(x => (x.ExactQuota == 0 || x.User.ExactHolidayShiftCount == 0) ? 1 : 0)
                    .ThenByDescending(x => x.Years)
                    .ThenByDescending(x => x.Count - x.Fair)
                    .ThenByDescending(x => x.Surplus)
                    .ToList(),
                _ => donorQuery // مساوی (Equal)
                    .OrderByDescending(x => (x.ExactQuota == 0 || x.User.ExactHolidayShiftCount == 0) ? 1 : 0)
                    .ThenByDescending(x => x.Surplus > 5.0 ? 1 : 0)
                    .ThenByDescending(x => x.Count - x.Fair)
                    .ThenBy(x => x.Years)
                    .ThenByDescending(x => x.Surplus)
                    .ToList()
            };

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

                // انتخاب گیرندگان (Receivers):
                // ۱. کاربران با سهمیه ۰ هرگز گیرنده شیفت تعطیل نمی‌شوند
                // ۲. کاربرانی که به سقف سهمیه کلی تعطیل خود رسیده‌اند و fallback ندارند حذف می‌شوند
                // ۳. انصراف صریح از مازاد نداشته باشند
                // ۴. نیاز به شیفت داشته باشند بر اساس مود انتخابی
                var receiverQuery = userStats
                    .Where(x => x.User.UserId != donorEntry.User.UserId)
                    .Where(x => x.ExactQuota != 0 && x.User.ExactHolidayShiftCount != 0)
                    .Where(x => !(x.User.ExactHolidayShiftCount.HasValue && HolidayQuotaRules.CountTotalHolidayShifts(solution, constraints, x.User.UserId) >= x.User.ExactHolidayShiftCount.Value && x.Fallback != true))
                    .Where(x => !(x.Fallback == false && x.Count >= (x.ExactQuota ?? 0)))
                    .Where(x => x.Deficit > 1.0 || x.Fair - x.Count > 0.35 || (distributionType == 1 && x.Years > donorEntry.Years && x.Count <= donorEntry.Count - 1) || (distributionType == 2 && x.Years < donorEntry.Years && x.Count <= donorEntry.Count - 1));

                var receiverCandidates = distributionType switch
                {
                    1 => receiverQuery // سابقه بیشتر شیفت بیشتر -> با‌سابقه‌ها گیرنده باشند
                        .OrderByDescending(x => x.Years)
                        .ThenByDescending(x => x.Fair - x.Count)
                        .ThenByDescending(x => x.Deficit)
                        .ToList(),
                    2 => receiverQuery // سابقه بیشتر شیفت کمتر -> جوان‌ترها گیرنده باشند
                        .OrderBy(x => x.Years)
                        .ThenByDescending(x => x.Fair - x.Count)
                        .ThenByDescending(x => x.Deficit)
                        .ToList(),
                    _ => receiverQuery // مساوی
                        .OrderByDescending(x => x.Deficit > 2.0 ? 1 : 0)
                        .ThenByDescending(x => x.Deficit)
                        .ThenByDescending(x => x.Fair - x.Count)
                        .ThenByDescending(x => x.Years)
                        .ToList()
                };

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

                        if (WouldLeaveDonorBelowRequired(donorEntry.User, donorEntry.Worked, assignment, lookup, constraints))
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

    public static Dictionary<int, double> BuildHolidayFairShares(
        List<UserConstraint> eligible,
        int totalShifts,
        DateTime referenceDate,
        int distributionType = 0,
        double slope = 1.0,
        ShiftLabel? label = null)
    {
        bool IsZeroQuotaUser(UserConstraint u) =>
            u.ExactHolidayShiftCount == 0 ||
            (label == ShiftLabel.Morning && u.ExactHolidayMorningShiftCount == 0) ||
            (label == ShiftLabel.Evening && u.ExactHolidayEveningShiftCount == 0);

        var activeEligible = eligible.Where(u => !IsZeroQuotaUser(u)).ToList();
        var zeroUsers = eligible.Where(IsZeroQuotaUser).ToList();

        if (activeEligible.Count == 0)
        {
            return eligible.ToDictionary(u => u.UserId, _ => 0.0);
        }

        Dictionary<int, double> shares;
        if (distributionType == 0 || totalShifts == 0)
        {
            var equal = totalShifts / (double)activeEligible.Count;
            shares = activeEligible.ToDictionary(u => u.UserId, _ => equal);
        }
        else
        {
            var weights = activeEligible.ToDictionary(
                u => u.UserId,
                u =>
                {
                    var years = Math.Clamp(ResolveSeniorityYears(u, referenceDate), 0, 40);
                    var s = Math.Max(0.1, slope);
                    return distributionType switch
                    {
                        1 => Math.Pow(Math.Max(1, years + 1), s),    // سابقه بیشتر = شیفت بیشتر
                        2 => Math.Pow(Math.Max(1, 40 - years), s),   // سابقه بیشتر = شیفت کمتر (کم‌سابقه بیشتر)
                        _ => 1.0
                    };
                });

            var totalWeight = weights.Values.Sum();
            if (totalWeight <= 0)
            {
                var equal = totalShifts / (double)activeEligible.Count;
                shares = activeEligible.ToDictionary(u => u.UserId, _ => equal);
            }
            else
            {
                shares = activeEligible.ToDictionary(
                    u => u.UserId,
                    u => totalShifts * weights[u.UserId] / totalWeight);
            }
        }

        foreach (var zu in zeroUsers)
        {
            shares[zu.UserId] = 0.0;
        }

        return shares;
    }

    public static int ResolveSeniorityYears(UserConstraint user, DateTime referenceDate)
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

    private static int? GetExactHolidayQuota(UserConstraint user, ShiftLabel label) =>
        label == ShiftLabel.Morning ? user.ExactHolidayMorningShiftCount : user.ExactHolidayEveningShiftCount;

    private static bool? GetHolidayFallbackParticipation(UserConstraint user, ShiftLabel label) =>
        label == ShiftLabel.Morning ? user.MorningHolidayFallbackParticipation : user.EveningHolidayFallbackParticipation;

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
        if (HolidayQuotaRules.IsHolidayShiftForbidden(user, constraints, date, label, shiftId))
        {
            return false;
        }

        if (user.ExactHolidayShiftCount.HasValue)
        {
            var total = HolidayQuotaRules.CountTotalHolidayShifts(solution, constraints, user.UserId);
            var fallback = GetHolidayFallbackParticipation(user, label);
            if (total >= user.ExactHolidayShiftCount.Value && fallback != true)
            {
                return false;
            }
        }

        if (!ShiftEligibilityResolver.MayTakeLabelOnDate(user, label, date, constraints.StartDate))
        {
            return false;
        }

        // درخواست‌های مرخصی و عدم حضور تأییدشده بر همه چیز اولویت قطعی دارند
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
        var approvedCount = user.RequiredShiftSlots.Count(s => s.Date.Date == date.Date);
        var effectiveMax = Math.Max(maxPerDay, approvedCount);
        if (!DailyAssignmentRules.CanAddShift(
                existing,
                label,
                effectiveMax,
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

    /// <summary>
    /// بررسی محافظت قطعی شیفت به دلیل درخواست تأییدشده کاربر
    /// </summary>
    private static bool IsRequestProtected(UserConstraint user, SaShiftAssignment assignment)
    {
        // حضور تأییدشده در اسلات مشخص اولویت قطعی دارد
        if (ApprovedRequestGuard.IsApprovedRequiredSlot(user, assignment.Date, assignment.ShiftLabel, assignment.ShiftId))
        {
            return true;
        }

        if (user.RequiredShiftSlots.Any(s =>
                s.Date.Date == assignment.Date.Date && s.ShiftLabel == assignment.ShiftLabel))
        {
            return true;
        }

        return !assignment.IsOnCall &&
               user.RequiredPresenceDates.Any(d => d.Date == assignment.Date.Date);
    }
}
