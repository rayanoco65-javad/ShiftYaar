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
            foreach (var label in new[] { ShiftLabel.Morning, ShiftLabel.Evening, ShiftLabel.Night })
            {
                RedistributeLabel(solution, constraints, lookup, peers, holidayDates, label);
            }

            RedistributeTotalHolidayShifts(solution, constraints, lookup, peers, holidayDates);
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

            var donorQuery = userStats
                .Where(x => x.Count > 0)
                .Where(x => x.ExactQuota == 0 || x.User.ExactHolidayShiftCount == 0 || !x.ExactQuota.HasValue || x.Count > x.ExactQuota.Value)
                .Where(x => x.ExactQuota == 0 || x.User.ExactHolidayShiftCount == 0 || x.Count > x.Fair + 0.35 || x.Surplus > 2.0 || (distributionType != 0 && x.Count >= 1));

            var donorCandidates = distributionType switch
            {
                1 => donorQuery
                    .OrderByDescending(x => (x.ExactQuota == 0 || x.User.ExactHolidayShiftCount == 0) ? 1 : 0)
                    .ThenBy(x => x.Years)
                    .ThenByDescending(x => x.Count - x.Fair)
                    .ThenByDescending(x => x.Surplus)
                    .ToList(),
                2 => donorQuery
                    .OrderByDescending(x => (x.ExactQuota == 0 || x.User.ExactHolidayShiftCount == 0) ? 1 : 0)
                    .ThenByDescending(x => x.Years)
                    .ThenByDescending(x => x.Count - x.Fair)
                    .ThenByDescending(x => x.Surplus)
                    .ToList(),
                _ => donorQuery
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
                    .Where(a => CanDonateHolidayAssignment(solution, constraints, donorEntry.User, a))
                    .OrderByDescending(a => a.Date)
                    .ToList();

                if (donorHolidayAssignments.Count == 0)
                {
                    continue;
                }

                var receiverQuery = userStats
                    .Where(x => x.User.UserId != donorEntry.User.UserId)
                    .Where(x => x.ExactQuota != 0 && x.User.ExactHolidayShiftCount != 0)
                    .Where(x => !(x.User.ExactHolidayShiftCount.HasValue && HolidayQuotaRules.CountTotalHolidayShifts(solution, constraints, x.User.UserId) >= x.User.ExactHolidayShiftCount.Value && x.Fallback != true))
                    .Where(x => !(x.Fallback == false && x.Count >= (x.ExactQuota ?? 0)))
                    .Where(x => x.Deficit > 1.0 || x.Fair - x.Count > 0.35 || (distributionType == 1 && x.Years > donorEntry.Years && x.Count <= donorEntry.Count - 1) || (distributionType == 2 && x.Years < donorEntry.Years && x.Count <= donorEntry.Count - 1));

                var receiverCandidates = distributionType switch
                {
                    1 => receiverQuery
                        .OrderByDescending(x => x.Years)
                        .ThenByDescending(x => x.Fair - x.Count)
                        .ThenByDescending(x => x.Deficit)
                        .ToList(),
                    2 => receiverQuery
                        .OrderBy(x => x.Years)
                        .ThenByDescending(x => x.Fair - x.Count)
                        .ThenByDescending(x => x.Deficit)
                        .ToList(),
                    _ => receiverQuery
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
                        // ۱. تلاش برای معاوضه (Swap) با شیفت روز غیرتعطیل گیرنده (هم‌برچسب)
                        var receiverWeekdayAssignments = solution.GetUserAllAssignments(receiverEntry.User.UserId)
                            .Where(a => !a.IsOnCall && !constraints.IsHoliday(a.Date) && a.ShiftLabel == label)
                            .Where(a => !IsRequestProtected(receiverEntry.User, a))
                            .OrderBy(a => a.Date)
                            .ToList();

                        foreach (var wAsg in receiverWeekdayAssignments)
                        {
                            if (CanSwapHolidayWithWeekday(
                                    solution, constraints, donorEntry.User, assignment, receiverEntry.User, wAsg))
                            {
                                ExecuteSwap(solution, donorEntry.User, assignment, receiverEntry.User, wAsg);
                                moved = true;
                                break;
                            }
                        }

                        if (moved)
                        {
                            break;
                        }

                        // ۲. انتقال یک‌طرفه (در صورت عدم امکان معاوضه و عدم افت موظفی)
                        if (solution.HasAssignment(receiverEntry.User.UserId, assignment.ShiftId, assignment.Date))
                        {
                            continue;
                        }

                        if (!CanTakeAssignment(
                                solution, constraints, donorEntry.User, receiverEntry.User, assignment))
                        {
                            continue;
                        }

                        if (WouldLeaveDonorBelowRequired(donorEntry.User, donorEntry.Worked, assignment, lookup, constraints))
                        {
                            continue;
                        }

                        ExecuteTransfer(solution, donorEntry.User, receiverEntry.User, assignment);
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

    /// <summary>
    /// بازتوزیع و تعادل مجموع کل شیفت‌های روزهای تعطیل (فارغ از برچسب صبح/عصر/شب) بین پرسنل هم‌تخصص.
    /// این متد تضمین می‌کند پرسنلی به دلیل دریافت ترکیبی از صبح/عصر/شب روی تعطیلات، بار مضاعفی دریافت نکند.
    /// </summary>
    private static void RedistributeTotalHolidayShifts(
        ShiftSolution solution,
        ShiftConstraints constraints,
        IReadOnlyDictionary<int, ProductivityWorkedHoursCalculator.ShiftWorkInfo> lookup,
        List<UserConstraint> peers,
        List<DateTime> holidayDates)
    {
        var eligible = peers
            .Where(u => u.IsActive)
            .ToList();
        if (eligible.Count < 2)
        {
            return;
        }

        var totalHolidayShifts = eligible.Sum(u => CountTotalHolidayShifts(solution, constraints, u.UserId));
        if (totalHolidayShifts == 0)
        {
            return;
        }

        var distributionType = constraints.HolidayShiftDistributionType;
        var slope = constraints.HolidaySeniorityDistributionSlope;
        var fairShares = BuildHolidayFairShares(eligible, totalHolidayShifts, constraints.StartDate, distributionType, slope, label: null);

        for (var pass = 0; pass < 64; pass++)
        {
            var userStats = eligible
                .Select(u =>
                {
                    var count = CountTotalHolidayShifts(solution, constraints, u.UserId);
                    var worked = CalculateWorked(solution, u.UserId, lookup, constraints);
                    var deficit = GetDeficit(u, worked);
                    var surplus = GetSurplus(u, worked);
                    var fair = fairShares.TryGetValue(u.UserId, out var f) ? f : 0;
                    var years = ResolveSeniorityYears(u, constraints.StartDate);
                    var exactQuota = u.ExactHolidayShiftCount;
                    return (User: u, Count: count, Fair: fair, Years: years, Worked: worked, Deficit: deficit, Surplus: surplus, ExactQuota: exactQuota);
                })
                .ToList();

            var minCount = userStats.Min(x => x.Count);
            var maxCount = userStats.Max(x => x.Count);

            // در مود مساوی (Equal): اگر اختلاف بیشترین و کمترین شیفت تعطیل حداکثر ۱ باشد و سهمیه نقض نشده باشد، تعادل کامل است
            if (distributionType == 0 && maxCount - minCount <= 1)
            {
                var hasQuotaViolation = userStats.Any(x => x.ExactQuota.HasValue && x.Count != x.ExactQuota.Value);
                if (!hasQuotaViolation)
                {
                    break;
                }
            }

            var donorQuery = userStats
                .Where(x => x.Count > 0)
                .Where(x => x.ExactQuota == 0 || x.Count > GetMinRequiredHolidayCount(x.User))
                .Where(x => x.ExactQuota == 0
                            || (distributionType == 0 && (x.Count > Math.Ceiling(x.Fair) || x.Count >= minCount + 2))
                            || (distributionType != 0 && x.Count > x.Fair + 0.35));

            var donorCandidates = distributionType switch
            {
                1 => donorQuery
                    .OrderByDescending(x => x.ExactQuota == 0 ? 1 : 0)
                    .ThenBy(x => x.Years)
                    .ThenByDescending(x => x.Count - x.Fair)
                    .ThenByDescending(x => x.Surplus)
                    .ToList(),
                2 => donorQuery
                    .OrderByDescending(x => x.ExactQuota == 0 ? 1 : 0)
                    .ThenByDescending(x => x.Years)
                    .ThenByDescending(x => x.Count - x.Fair)
                    .ThenByDescending(x => x.Surplus)
                    .ToList(),
                _ => donorQuery
                    .OrderByDescending(x => x.ExactQuota == 0 ? 1 : 0)
                    .ThenByDescending(x => x.Count - x.Fair)
                    .ThenByDescending(x => x.Count)
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
                    .Where(a => !a.IsOnCall && constraints.IsHoliday(a.Date))
                    .Where(a => !IsRequestProtected(donorEntry.User, a))
                    .Where(a => CanDonateHolidayAssignment(solution, constraints, donorEntry.User, a))
                    .OrderByDescending(a => a.Date)
                    .ToList();

                if (donorHolidayAssignments.Count == 0)
                {
                    continue;
                }

                var receiverQuery = userStats
                    .Where(x => x.User.UserId != donorEntry.User.UserId)
                    .Where(x => x.ExactQuota != 0)
                    .Where(x => !(x.ExactQuota.HasValue && x.Count >= x.ExactQuota.Value))
                    .Where(x => (distributionType == 0 && (x.Count < Math.Floor(x.Fair) || x.Count <= donorEntry.Count - 2))
                                || (distributionType == 1 && (x.Years > donorEntry.Years && x.Count < donorEntry.Count))
                                || (distributionType == 2 && (x.Years < donorEntry.Years && x.Count < donorEntry.Count))
                                || (x.Fair - x.Count > 0.35));

                var receiverCandidates = distributionType switch
                {
                    1 => receiverQuery
                        .OrderByDescending(x => x.Years)
                        .ThenBy(x => x.Count)
                        .ThenByDescending(x => x.Fair - x.Count)
                        .ToList(),
                    2 => receiverQuery
                        .OrderBy(x => x.Years)
                        .ThenBy(x => x.Count)
                        .ThenByDescending(x => x.Fair - x.Count)
                        .ToList(),
                    _ => receiverQuery
                        .OrderBy(x => x.Count)
                        .ThenByDescending(x => x.Fair - x.Count)
                        .ThenByDescending(x => x.Deficit)
                        .ToList()
                };

                foreach (var receiverEntry in receiverCandidates)
                {
                    foreach (var hAsg in donorHolidayAssignments)
                    {
                        // ۱) اولویت قطعی: معاوضه (Swap) با یک شیفت روز عادی گیرنده تا موظفی و تعداد شیفت هر دو نفر حفظ شود
                        var receiverWeekdayAssignments = solution.GetUserAllAssignments(receiverEntry.User.UserId)
                            .Where(a => !a.IsOnCall && !constraints.IsHoliday(a.Date))
                            .Where(a => !IsRequestProtected(receiverEntry.User, a))
                            .OrderBy(a => a.ShiftLabel == hAsg.ShiftLabel ? 0 : 1)
                            .ThenBy(a => a.Date)
                            .ToList();

                        foreach (var wAsg in receiverWeekdayAssignments)
                        {
                            if (CanSwapHolidayWithWeekday(
                                    solution, constraints, donorEntry.User, hAsg, receiverEntry.User, wAsg))
                            {
                                ExecuteSwap(solution, donorEntry.User, hAsg, receiverEntry.User, wAsg);
                                moved = true;
                                break;
                            }
                        }

                        if (moved)
                        {
                            break;
                        }

                        // ۲) انتقال یک‌طرفه در صورت امکان و عدم افت موظفی اهداکننده
                        if (solution.HasAssignment(receiverEntry.User.UserId, hAsg.ShiftId, hAsg.Date))
                        {
                            continue;
                        }

                        if (!CanTakeAssignment(
                                solution, constraints, donorEntry.User, receiverEntry.User, hAsg))
                        {
                            continue;
                        }

                        if (WouldLeaveDonorBelowRequired(donorEntry.User, donorEntry.Worked, hAsg, lookup, constraints))
                        {
                            continue;
                        }

                        ExecuteTransfer(solution, donorEntry.User, receiverEntry.User, hAsg);
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
            (label == ShiftLabel.Evening && u.ExactHolidayEveningShiftCount == 0) ||
            (label == ShiftLabel.Night && u.ExactHolidayWeekendNightShiftCount == 0);

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
        label switch
        {
            ShiftLabel.Morning => user.ExactHolidayMorningShiftCount,
            ShiftLabel.Evening => user.ExactHolidayEveningShiftCount,
            ShiftLabel.Night => user.ExactHolidayWeekendNightShiftCount,
            _ => null
        };

    private static bool? GetHolidayFallbackParticipation(UserConstraint user, ShiftLabel label) =>
        label switch
        {
            ShiftLabel.Morning => user.MorningHolidayFallbackParticipation,
            ShiftLabel.Evening => user.EveningHolidayFallbackParticipation,
            _ => null
        };

    private static int GetMinRequiredHolidayCount(UserConstraint user)
    {
        if (user.ExactHolidayShiftCount.HasValue)
        {
            return user.ExactHolidayShiftCount.Value;
        }

        var sum = 0;
        if (user.ExactHolidayMorningShiftCount.HasValue) sum += user.ExactHolidayMorningShiftCount.Value;
        if (user.ExactHolidayEveningShiftCount.HasValue) sum += user.ExactHolidayEveningShiftCount.Value;
        if (user.ExactHolidayWeekendNightShiftCount.HasValue) sum += user.ExactHolidayWeekendNightShiftCount.Value;
        return sum;
    }

    private static bool CanDonateHolidayAssignment(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        SaShiftAssignment assignment)
    {
        if (user.ExactHolidayShiftCount.HasValue)
        {
            var total = CountTotalHolidayShifts(solution, constraints, user.UserId);
            if (total <= user.ExactHolidayShiftCount.Value)
            {
                return false;
            }
        }

        var exactLabelQuota = GetExactHolidayQuota(user, assignment.ShiftLabel);
        if (exactLabelQuota.HasValue)
        {
            var count = CountHolidayLabel(solution, constraints, user.UserId, assignment.ShiftLabel);
            if (count <= exactLabelQuota.Value)
            {
                return false;
            }
        }

        return true;
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

    private static bool CanSwapHolidayWithWeekday(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint donor,
        SaShiftAssignment donorHolidayAsg,
        UserConstraint receiver,
        SaShiftAssignment receiverWeekdayAsg)
    {
        if (donorHolidayAsg.Date.Date == receiverWeekdayAsg.Date.Date)
        {
            return false;
        }

        if (IsRequestProtected(donor, donorHolidayAsg) || IsRequestProtected(receiver, receiverWeekdayAsg))
        {
            return false;
        }

        // بررسی محدودیت‌های شیفت شب
        if (donorHolidayAsg.ShiftLabel == ShiftLabel.Night || receiverWeekdayAsg.ShiftLabel == ShiftLabel.Night)
        {
            if (donorHolidayAsg.ShiftLabel != receiverWeekdayAsg.ShiftLabel)
            {
                if (donor.HasExactNightQuota || receiver.HasExactNightQuota)
                {
                    return false;
                }
            }
            else
            {
                if (donor.ExactHolidayWeekendNightShiftCount.HasValue)
                {
                    var donorHn = solution.GetUserAllAssignments(donor.UserId)
                        .Count(a => !a.IsOnCall && a.ShiftLabel == ShiftLabel.Night && constraints.IsHoliday(a.Date));
                    if (donorHn <= donor.ExactHolidayWeekendNightShiftCount.Value)
                    {
                        return false;
                    }
                }

                if (receiver.ExactHolidayWeekendNightShiftCount.HasValue)
                {
                    var receiverHn = solution.GetUserAllAssignments(receiver.UserId)
                        .Count(a => !a.IsOnCall && a.ShiftLabel == ShiftLabel.Night && constraints.IsHoliday(a.Date));
                    if (receiverHn >= receiver.ExactHolidayWeekendNightShiftCount.Value)
                    {
                        return false;
                    }
                }
            }
        }

        // آیا گیرنده می‌تواند شیفت تعطیل اهداکننده را بردارد؟ (با فرض حذف شیفت روز عادی‌اش)
        if (!CanTakeAssignment(
                solution,
                constraints,
                donor,
                receiver,
                donorHolidayAsg,
                toRemove: new[] { receiverWeekdayAsg }))
        {
            return false;
        }

        // آیا اهداکننده می‌تواند شیفت روز عادی گیرنده را بردارد؟ (با فرض حذف شیفت تعطیلش)
        if (!CanTakeAssignment(
                solution,
                constraints,
                receiver,
                donor,
                receiverWeekdayAsg,
                toRemove: new[] { donorHolidayAsg }))
        {
            return false;
        }

        return true;
    }

    private static void ExecuteSwap(
        ShiftSolution solution,
        UserConstraint donor,
        SaShiftAssignment donorHolidayAsg,
        UserConstraint receiver,
        SaShiftAssignment receiverWeekdayAsg)
    {
        var isDonorSkeleton = solution.IsLockedSkeleton(donor.UserId, donorHolidayAsg.ShiftId, donorHolidayAsg.Date)
                              || donorHolidayAsg.IsSkeleton;
        var isReceiverSkeleton = solution.IsLockedSkeleton(receiver.UserId, receiverWeekdayAsg.ShiftId, receiverWeekdayAsg.Date)
                                || receiverWeekdayAsg.IsSkeleton;

        solution.RemoveAssignment(donor.UserId, donorHolidayAsg.ShiftId, donorHolidayAsg.Date);
        solution.AddAssignment(receiver.UserId, donorHolidayAsg.ShiftId, donorHolidayAsg.Date, donorHolidayAsg.ShiftLabel, donorHolidayAsg.IsOnCall);
        if (isDonorSkeleton)
        {
            var asg = solution.GetUserAssignments(receiver.UserId, donorHolidayAsg.Date)
                .FirstOrDefault(a => a.ShiftId == donorHolidayAsg.ShiftId);
            if (asg != null) asg.IsSkeleton = true;
        }

        solution.RemoveAssignment(receiver.UserId, receiverWeekdayAsg.ShiftId, receiverWeekdayAsg.Date);
        solution.AddAssignment(donor.UserId, receiverWeekdayAsg.ShiftId, receiverWeekdayAsg.Date, receiverWeekdayAsg.ShiftLabel, receiverWeekdayAsg.IsOnCall);
        if (isReceiverSkeleton)
        {
            var asg = solution.GetUserAssignments(donor.UserId, receiverWeekdayAsg.Date)
                .FirstOrDefault(a => a.ShiftId == receiverWeekdayAsg.ShiftId);
            if (asg != null) asg.IsSkeleton = true;
        }
    }

    private static void ExecuteTransfer(
        ShiftSolution solution,
        UserConstraint donor,
        UserConstraint receiver,
        SaShiftAssignment assignment)
    {
        var isSkeleton = solution.IsLockedSkeleton(donor.UserId, assignment.ShiftId, assignment.Date)
                         || assignment.IsSkeleton;

        solution.RemoveAssignment(donor.UserId, assignment.ShiftId, assignment.Date);
        solution.AddAssignment(receiver.UserId, assignment.ShiftId, assignment.Date, assignment.ShiftLabel, assignment.IsOnCall);

        if (isSkeleton)
        {
            var newAsg = solution.GetUserAssignments(receiver.UserId, assignment.Date)
                .FirstOrDefault(a => a.ShiftId == assignment.ShiftId);
            if (newAsg != null) newAsg.IsSkeleton = true;
        }
    }

    public static int CountHolidayLabel(
        ShiftSolution solution,
        ShiftConstraints constraints,
        int userId,
        ShiftLabel label) =>
        solution.GetUserAllAssignments(userId)
            .Count(a => !a.IsOnCall && a.ShiftLabel == label && constraints.IsHoliday(a.Date));

    public static int CountTotalHolidayShifts(
        ShiftSolution solution,
        ShiftConstraints constraints,
        int userId) =>
        solution.GetUserAllAssignments(userId)
            .Count(a => !a.IsOnCall && constraints.IsHoliday(a.Date));

    private static bool CanTakeAssignment(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint donor,
        UserConstraint receiver,
        SaShiftAssignment assignment,
        IReadOnlyList<SaShiftAssignment>? toRemove = null)
    {
        var date = assignment.Date;
        var label = assignment.ShiftLabel;
        var shiftId = assignment.ShiftId;

        if (constraints.IsHoliday(date))
        {
            if (HolidayQuotaRules.IsHolidayShiftForbidden(receiver, constraints, date, label, shiftId))
            {
                return false;
            }

            if (receiver.ExactHolidayShiftCount.HasValue)
            {
                var currentTotal = solution.GetUserAllAssignments(receiver.UserId)
                    .Count(a => !a.IsOnCall && constraints.IsHoliday(a.Date));
                if (toRemove != null)
                {
                    currentTotal -= toRemove.Count(r => !r.IsOnCall && constraints.IsHoliday(r.Date));
                }

                var fallback = GetHolidayFallbackParticipation(receiver, label);
                if (currentTotal >= receiver.ExactHolidayShiftCount.Value && fallback != true)
                {
                    return false;
                }
            }

            var exactLabelQuota = GetExactHolidayQuota(receiver, label);
            if (exactLabelQuota.HasValue)
            {
                var currentLabel = solution.GetUserAllAssignments(receiver.UserId)
                    .Count(a => !a.IsOnCall && a.ShiftLabel == label && constraints.IsHoliday(a.Date));
                if (toRemove != null)
                {
                    currentLabel -= toRemove.Count(r => !r.IsOnCall && r.ShiftLabel == label && constraints.IsHoliday(r.Date));
                }

                var fallback = GetHolidayFallbackParticipation(receiver, label);
                if (currentLabel >= exactLabelQuota.Value && fallback != true)
                {
                    return false;
                }
            }
        }

        if (!ShiftEligibilityResolver.MayTakeLabelOnDate(receiver, label, date, constraints.StartDate))
        {
            return false;
        }

        if (receiver.UnavailableDates.Any(d => d.Date == date.Date))
        {
            return false;
        }

        if (receiver.UnavailableShiftSlots.Any(s => s.Date.Date == date.Date && s.ShiftLabel == label))
        {
            return false;
        }

        var removeKeys = (toRemove ?? Array.Empty<SaShiftAssignment>())
            .Select(r => (r.ShiftId, r.Date.Date))
            .ToHashSet();

        if (solution.HasAssignment(receiver.UserId, shiftId, date) && !removeKeys.Contains((shiftId, date.Date)))
        {
            return false;
        }

        var remaining = solution.GetUserAllAssignments(receiver.UserId)
            .Where(a => !removeKeys.Contains((a.ShiftId, a.Date.Date)))
            .ToList();

        var existingLabelsOnDate = remaining
            .Where(a => a.Date.Date == date.Date)
            .Select(a => a.ShiftLabel);

        var maxPerDay = constraints.HardRules.EnforceMaxShiftsPerDay
            ? Math.Max(1, constraints.GlobalConstraints.MaxShiftsPerDay)
            : 2;
        var approvedCount = receiver.RequiredShiftSlots.Count(s => s.Date.Date == date.Date);
        var effectiveMax = Math.Max(maxPerDay, approvedCount);

        if (!DailyAssignmentRules.CanAddShift(
                existingLabelsOnDate,
                label,
                effectiveMax,
                constraints.HardRules.ForbidDuplicateDailyAssignments))
        {
            return false;
        }

        if (AdjacentShiftRestRules.WouldConflict(remaining, date, label, constraints))
        {
            return false;
        }

        if (MaxConsecutiveWorkdayRules.WouldExceedMaxConsecutiveWorkdays(
                remaining, receiver, date, constraints.HardRules.EnforceMaxConsecutiveShifts))
        {
            return false;
        }

        var isSkeleton = solution.IsLockedSkeleton(donor.UserId, shiftId, date) || assignment.IsSkeleton;
        if (isSkeleton)
        {
            var shiftReq = constraints.ShiftRequirements.FirstOrDefault(s => s.ShiftId == shiftId);
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

    private static bool IsRequestProtected(UserConstraint user, SaShiftAssignment assignment)
    {
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
