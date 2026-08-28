using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;

/// <summary>
/// تضمین حداقل تعداد شیفت شب (و شب‌های تعطیل/آخرهفته) و توزیع یکنواخت آن‌ها در طول بازه.
/// اگر ظرفیت روز پر باشد، شب را از کاربری که هنوز بالای حداقل خودش است می‌گیرد.
/// وقتی مجموع سهمیه‌ها = ظرفیت ماه، با زنجیرهٔ دو مرحله‌ای و پاک‌کردن تداخل عصر/صبح فردا جابه‌جا می‌کند.
/// </summary>
public static class ExactNightQuotaGuard
{
    public static void Enforce(ShiftSolution solution, ShiftConstraints constraints)
    {
        var nightShift = constraints.ShiftRequirements.FirstOrDefault(s => s.ShiftLabel == ShiftLabel.Night);
        if (nightShift == null)
        {
            return;
        }

        // اول کسری‌های شدیدتر (شب تعطیل، سپس کل)، بعد سهمیه‌های کوچک‌تر تا روی صندلی‌های کمیاب گیر نکنند
        for (var pass = 0; pass < 6; pass++)
        {
            foreach (var user in OrderUsersByDeficit(solution, constraints))
            {
                EnforceForUser(solution, constraints, user, nightShift);
            }
        }

        // پخش شب‌ها برای همه سهمیه‌دارها (حتی اگر حداقلشان پر باشد)
        foreach (var user in constraints.UserConstraints
                     .Where(u => u.HasExactNightQuota || u.ExactHolidayWeekendNightShiftCount.HasValue))
        {
            ImproveNightSpread(solution, constraints, user, nightShift);
        }

        // پخش ممکن است جای خالی برای کسری باقی‌مانده باز کند — یک دور نهایی جبران
        for (var pass = 0; pass < 4; pass++)
        {
            var deficitUsers = OrderUsersByDeficit(solution, constraints).ToList();
            if (deficitUsers.Count == 0)
            {
                break;
            }

            foreach (var user in deficitUsers)
            {
                EnforceForUser(solution, constraints, user, nightShift);
            }
        }

        // وقتی مجموع سهمیه = ظرفیت ماه است، رزرو شب تعطیل نباید مانع تکمیل حداقل کل شود
        ForceFillRemainingTotalIgnoringHolidayReservation(solution, constraints, nightShift);
    }

    /// <summary>جبران فوری سهمیه شب یک کاربر پس از جابجایی برای مسئول شیفت.</summary>
    public static void EnforceExactNightQuotaForUser(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user)
    {
        var nightShift = constraints.ShiftRequirements.FirstOrDefault(s => s.ShiftLabel == ShiftLabel.Night);
        if (nightShift == null)
        {
            return;
        }

        EnforceForUser(solution, constraints, user, nightShift);
    }

    /// <summary>
    /// آخرین تلاش: اگر سهمیه کل هنوز کامل نشده، رزرو «باقی‌مانده برای تعطیل» را نادیده بگیر
    /// و از زنجیرهٔ دو مرحله‌ای برای جابه‌جایی در ماه پر استفاده کن.
    /// </summary>
    private static void ForceFillRemainingTotalIgnoringHolidayReservation(
        ShiftSolution solution,
        ShiftConstraints constraints,
        ShiftRequirement nightShift)
    {
        for (var pass = 0; pass < 3; pass++)
        {
            var anyProgress = false;
            foreach (var user in OrderUsersByDeficit(solution, constraints))
            {
                if (!user.ExactNightShiftCount.HasValue)
                {
                    continue;
                }

                var remaining = user.ExactNightShiftCount.Value - CountNights(solution, user.UserId);
                if (remaining <= 0)
                {
                    continue;
                }

                var minGap = ResolveNightSpacingGap(constraints, user);
                var before = CountNights(solution, user.UserId);
                TryFillNights(solution, constraints, user, nightShift, remaining, holidayOnly: false);
                var stillNeed = user.ExactNightShiftCount.Value - CountNights(solution, user.UserId);
                if (stillNeed > 0)
                {
                    ClaimNightsFromDonors(solution, constraints, user, nightShift, stillNeed, holidayOnly: false, minGap, ignoreHolidayReservation: true);
                    stillNeed = user.ExactNightShiftCount.Value - CountNights(solution, user.UserId);
                }

                if (stillNeed > 0)
                {
                    ClaimViaTwoHopChain(solution, constraints, user, nightShift, stillNeed, holidayOnly: false, minGap, ignoreHolidayReservation: true);
                }

                if (CountNights(solution, user.UserId) > before)
                {
                    anyProgress = true;
                }
            }

            if (!anyProgress)
            {
                break;
            }
        }
    }

    private static IEnumerable<UserConstraint> OrderUsersByDeficit(
        ShiftSolution solution,
        ShiftConstraints constraints)
    {
        return constraints.UserConstraints
            .Where(u => u.HasExactNightQuota || u.ExactHolidayWeekendNightShiftCount.HasValue)
            .Select(u =>
            {
                var nights = GetNights(solution, u.UserId);
                var holiday = nights.Count(a => constraints.IsHolidayWeekendNight(a.Date));
                var holidayDeficit = Math.Max(0, (u.ExactHolidayWeekendNightShiftCount ?? 0) - holiday);
                var totalDeficit = Math.Max(0, (u.ExactNightShiftCount ?? 0) - nights.Count);
                return (User: u, HolidayDeficit: holidayDeficit, TotalDeficit: totalDeficit);
            })
            .Where(x => x.HolidayDeficit > 0 || x.TotalDeficit > 0)
            .OrderByDescending(x => x.HolidayDeficit)
            .ThenByDescending(x => x.TotalDeficit)
            .ThenBy(x => x.User.ExactNightShiftCount ?? 0)
            .Select(x => x.User);
    }

    private static void EnforceForUser(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        ShiftRequirement nightShift)
    {
        var targetTotal = user.ExactNightShiftCount;
        var targetHoliday = user.ExactHolidayWeekendNightShiftCount;

        var nights = GetNights(solution, user.UserId);
        var holidayNights = nights.Where(a => constraints.IsHolidayWeekendNight(a.Date)).ToList();

        if (targetHoliday.HasValue)
        {
            var holidayNeed = targetHoliday.Value - holidayNights.Count;
            if (holidayNeed > 0)
            {
                // اول تعویض شب عادی↔تعطیل (تعداد کل ثابت می‌ماند)
                var minGap = ResolveNightSpacingGap(constraints, user);
                SwapWeekdayForHoliday(solution, constraints, user, nightShift, holidayNeed, minGap);
                nights = GetNights(solution, user.UserId);
                holidayNights = nights.Where(a => constraints.IsHolidayWeekendNight(a.Date)).ToList();
                holidayNeed = targetHoliday.Value - holidayNights.Count;
            }

            if (holidayNeed > 0)
            {
                // فقط اگر هنوز زیر سقف/حداقل کل هستیم، شب تعطیل اضافه کن — هرگز از ExactNight بالاتر نرو
                var roomForAdd = targetTotal.HasValue
                    ? Math.Max(0, targetTotal.Value - nights.Count)
                    : holidayNeed;
                if (roomForAdd > 0)
                {
                    TryFillNights(solution, constraints, user, nightShift, Math.Min(holidayNeed, roomForAdd), holidayOnly: true);
                    nights = GetNights(solution, user.UserId);
                    holidayNights = nights.Where(a => constraints.IsHolidayWeekendNight(a.Date)).ToList();
                }
            }
        }

        // اگر به‌اشتباه بالای ExactNight رفته‌ایم، مازاد غیرمحافظت‌شده را بردار
        TrimExcessNightsAboveExact(solution, constraints, user);

        nights = GetNights(solution, user.UserId);
        holidayNights = nights.Where(a => constraints.IsHolidayWeekendNight(a.Date)).ToList();

        if (targetTotal.HasValue)
        {
            var remaining = targetTotal.Value - nights.Count;
            if (remaining > 0)
            {
                var stillNeedHoliday = targetHoliday.HasValue
                    ? Math.Max(0, targetHoliday.Value - holidayNights.Count)
                    : 0;
                if (stillNeedHoliday > 0)
                {
                    // فقط در سقف باقی‌ماندهٔ کل
                    TryFillNights(solution, constraints, user, nightShift, Math.Min(stillNeedHoliday, remaining), holidayOnly: true);
                    remaining = targetTotal.Value - CountNights(solution, user.UserId);
                }

                if (remaining > 0)
                {
                    TryFillNights(solution, constraints, user, nightShift, remaining, holidayOnly: false);
                }
            }
        }

        TrimExcessNightsAboveExact(solution, constraints, user);
    }

    /// <summary>
    /// ExactNight حداقل است ولی نباید با افزودن شب تعطیل از آن بیشتر شویم وقتی ماه بدون مازاد ظرفیت است.
    /// شب‌های مازاد غیرمحافظت‌شده حذف می‌شوند (ترجیحاً غیرتعطیل اگر سهمیه تعطیل حفظ شود).
    /// </summary>
    private static void TrimExcessNightsAboveExact(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user)
    {
        var maxTotal = NightQuotaEligibility.GetMaxAllowedTotal(user);
        if (maxTotal == int.MaxValue)
        {
            return;
        }

        var targetHoliday = user.ExactHolidayWeekendNightShiftCount ?? 0;

        while (CountNights(solution, user.UserId) > maxTotal)
        {
            var nights = GetNights(solution, user.UserId);
            var holidayCount = nights.Count(a => constraints.IsHolidayWeekendNight(a.Date));

            var removable = nights
                .Where(a => !IsProtected(constraints, user.UserId, a))
                .Select(a =>
                {
                    var isHol = constraints.IsHolidayWeekendNight(a.Date);
                    // حذف شب تعطیل فقط اگر بعد از حذف هنوز ≥ سهمیه تعطیل بمانیم
                    var canRemoveHoliday = !isHol || holidayCount - 1 >= targetHoliday;
                    return (Assignment: a, IsHol: isHol, CanRemove: canRemoveHoliday);
                })
                .Where(x => x.CanRemove)
                // ترجیح حذف غیرتعطیل تا سهمیه تعطیل حفظ شود؛ وگرنه تعطیل
                .OrderBy(x => x.IsHol ? 1 : 0)
                .ThenByDescending(x => x.Assignment.Date)
                .Select(x => x.Assignment)
                .FirstOrDefault();

            if (removable == null)
            {
                break;
            }

            solution.RemoveAssignment(removable.UserId, removable.ShiftId, removable.Date);
        }
    }

    private static List<SaShiftAssignment> GetNights(ShiftSolution solution, int userId) =>
        solution.GetUserAllAssignments(userId)
            .Where(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall)
            .OrderBy(a => a.Date)
            .ToList();

    private static int CountNights(ShiftSolution solution, int userId) =>
        GetNights(solution, userId).Count;

    private static void TryFillNights(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        ShiftRequirement nightShift,
        int needed,
        bool holidayOnly)
    {
        if (needed <= 0)
        {
            return;
        }

        MakeRoomForMorningNightComboQuota(solution, constraints, user);

        // صبح/عصر روز بعد از شب‌های موجود (مثلاً از ProductivityHourFill) مانع افزودن شب جدید می‌شود
        ClearStalePostNightConflicts(solution, constraints, user);

        var minGap = ResolveNightSpacingGap(constraints, user);
        var filled = 0;

        // ۱) صندلی‌های خالی
        filled += FillIntoOpenCapacity(solution, constraints, user, nightShift, needed, holidayOnly, minGap);
        needed -= filled;
        if (needed <= 0)
        {
            return;
        }

        // ۲) تبدیل شب عادی به شب تعطیل (بدون کم‌کردن تعداد کل اهداکننده)
        if (holidayOnly)
        {
            filled = SwapWeekdayForHoliday(solution, constraints, user, nightShift, needed, minGap);
            needed -= filled;
            if (needed <= 0)
            {
                return;
            }
        }

        // ۳) گرفتن شب از اهداکننده‌ای که بالای حداقل خودش است
        var beforeClaim = CountNights(solution, user.UserId);
        ClaimNightsFromDonors(solution, constraints, user, nightShift, needed, holidayOnly, minGap);
        needed -= CountNights(solution, user.UserId) - beforeClaim;
        if (needed <= 0)
        {
            return;
        }

        // ۴) زنجیره دو مرحله‌ای: صندلی مناسب گیرنده را از نفر روی کف بگیر،
        //    و او را با شب مازاد اهداکنندهٔ دیگر جبران کن (سناریوی sum(quota)=ظرفیت)
        var beforeChain = CountNights(solution, user.UserId);
        ClaimViaTwoHopChain(solution, constraints, user, nightShift, needed, holidayOnly, minGap);
        needed -= CountNights(solution, user.UserId) - beforeChain;
        if (needed <= 0)
        {
            return;
        }

        // ۵) جابه‌جایی شب خود گیرنده برای باز کردن فاصله، سپس ادعای مجدد
        var beforeRelocate = CountNights(solution, user.UserId);
        RelocateOwnNightsThenClaim(solution, constraints, user, nightShift, needed, holidayOnly, minGap);
        needed -= CountNights(solution, user.UserId) - beforeRelocate;
        if (needed <= 0)
        {
            return;
        }

        // ۶) اگر سهمیه تعطیل هنوز ناقص مانده ولی ظرفیت ماه پر است، برای تکمیل کل شب
        //    رزرو تعطیل را موقتاً نادیده بگیر (کسری تعطیل در پاس holidayOnly جداگانه پیگیری می‌شود)
        if (!holidayOnly && user.ExactHolidayWeekendNightShiftCount.HasValue && needed > 0)
        {
            var beforeRelax = CountNights(solution, user.UserId);
            ClaimNightsFromDonors(solution, constraints, user, nightShift, needed, holidayOnly: false, minGap, ignoreHolidayReservation: true);
            needed -= CountNights(solution, user.UserId) - beforeRelax;
            if (needed > 0)
            {
                beforeRelax = CountNights(solution, user.UserId);
                ClaimViaTwoHopChain(solution, constraints, user, nightShift, needed, holidayOnly: false, minGap, ignoreHolidayReservation: true);
                needed -= CountNights(solution, user.UserId) - beforeRelax;
            }
        }
    }

    /// <summary>
    /// وقتی کاربر شب عادی دارد ولی شب تعطیل کم دارد، شب عادی‌اش را با شب تعطیل اهداکننده‌ای که مازاد تعطیل دارد عوض می‌کند.
    /// تعداد کل شب هر دو ثابت می‌ماند — حتی اگر اهداکننده روی ExactNight باشد.
    /// </summary>
    private static int SwapWeekdayForHoliday(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        ShiftRequirement nightShift,
        int needed,
        int minGap)
    {
        var swapped = 0;
        while (swapped < needed)
        {
            var weekdayNight = GetNights(solution, user.UserId)
                .Where(a => !constraints.IsHolidayWeekendNight(a.Date))
                .Where(a => !IsProtected(constraints, user.UserId, a))
                .OrderBy(a => a.Date)
                .FirstOrDefault();
            if (weekdayNight == null)
            {
                break;
            }

            var weekdayDate = weekdayNight.Date.Date;
            var holidayDates = AllCandidateDates(constraints, holidayOnly: true)
                .Where(d => d != weekdayDate)
                .Where(d => IsPersonallyFeasibleNightDate(solution, constraints, user, nightShift, d, holidayOnly: true))
                .Where(d =>
                {
                    var otherNights = GetNights(solution, user.UserId)
                        .Where(a => a.Date.Date != weekdayDate)
                        .Select(a => a.Date.Date);
                    return !otherNights.Any(o => Math.Abs((o - d).Days) < minGap);
                })
                .ToList();

            SaShiftAssignment? bestDonorAssignment = null;
            UserConstraint? bestDonor = null;
            DateTime? bestHolidayDate = null;
            var bestScore = int.MaxValue;

            foreach (var holidayDate in holidayDates)
            {
                var occupants = solution.GetShiftAssignments(nightShift.ShiftId, holidayDate)
                    .Where(a => !a.IsOnCall && a.UserId != user.UserId)
                    .ToList();

                foreach (var occupant in occupants)
                {
                    var donor = constraints.UserConstraints.FirstOrDefault(u => u.UserId == occupant.UserId);
                    if (donor == null || !CanDonateHolidayViaSwap(solution, constraints, donor, occupant))
                    {
                        continue;
                    }

                    if (!CanSwapNight(solution, constraints, user, donor, weekdayDate, holidayDate, nightShift, minGap))
                    {
                        continue;
                    }

                    var donorNights = GetNights(solution, donor.UserId);
                    var holidaySurplus = donorNights.Count(a => constraints.IsHolidayWeekendNight(a.Date))
                                         - (donor.ExactHolidayWeekendNightShiftCount ?? 0);
                    var score = -(holidaySurplus * 100 + donorNights.Count);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestDonorAssignment = occupant;
                        bestDonor = donor;
                        bestHolidayDate = holidayDate;
                    }
                }
            }

            if (bestDonorAssignment == null || bestDonor == null || bestHolidayDate == null)
            {
                break;
            }

            ClearConflictingForNight(solution, constraints, user, bestHolidayDate.Value);
            ClearConflictingForNight(solution, constraints, bestDonor, weekdayDate);

            solution.RemoveAssignment(user.UserId, nightShift.ShiftId, weekdayDate);
            solution.RemoveAssignment(bestDonor.UserId, nightShift.ShiftId, bestHolidayDate.Value);
            solution.AddAssignment(user.UserId, nightShift.ShiftId, bestHolidayDate.Value, ShiftLabel.Night, false);
            solution.AddAssignment(bestDonor.UserId, nightShift.ShiftId, weekdayDate, ShiftLabel.Night, false);
            swapped++;
        }

        return swapped;
    }

    /// <summary>
    /// اهداکننده می‌تواند شب تعطیل را در قالب تعویض بدهد اگر بعد از آن زیر حداقل تعطیل نرود.
    /// </summary>
    private static bool CanDonateHolidayViaSwap(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint donor,
        SaShiftAssignment assignment)
    {
        if (assignment.ShiftLabel != ShiftLabel.Night || assignment.IsOnCall)
        {
            return false;
        }

        if (!constraints.IsHolidayWeekendNight(assignment.Date))
        {
            return false;
        }

        if (IsProtected(constraints, donor.UserId, assignment))
        {
            return false;
        }

        if (!donor.ExactHolidayWeekendNightShiftCount.HasValue)
        {
            return true;
        }

        var holidayNights = GetNights(solution, donor.UserId)
            .Count(a => constraints.IsHolidayWeekendNight(a.Date));
        return holidayNights - 1 >= donor.ExactHolidayWeekendNightShiftCount.Value;
    }

    private static int FillIntoOpenCapacity(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        ShiftRequirement nightShift,
        int needed,
        bool holidayOnly,
        int minGap)
    {
        var existingNightDates = GetNights(solution, user.UserId).Select(a => a.Date.Date).ToList();
        var candidates = AllCandidateDates(constraints, holidayOnly)
            .Where(d => IsFeasibleNightDate(solution, constraints, user, nightShift, d, holidayOnly, ignoreUserNightOnDate: false))
            .ToList();

        var picks = PickSpreadDates(candidates, existingNightDates, needed, minGap);
        var added = 0;
        foreach (var date in picks)
        {
            if (added >= needed)
            {
                break;
            }

            if (!IsFeasibleNightDate(solution, constraints, user, nightShift, date, holidayOnly, ignoreUserNightOnDate: false))
            {
                continue;
            }

            if (ViolatesNightSpacing(solution, constraints, user, date, minGap))
            {
                continue;
            }

            ClearConflictingForNight(solution, constraints, user, date);
            solution.AddAssignment(user.UserId, nightShift.ShiftId, date, ShiftLabel.Night, isOnCall: false);
            added++;
        }

        return added;
    }

    private static void ClaimNightsFromDonors(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        ShiftRequirement nightShift,
        int needed,
        bool holidayOnly,
        int minGap,
        bool ignoreHolidayReservation = false)
    {
        var claimed = 0;
        var candidateDates = PickSpreadDates(
            AllCandidateDates(constraints, holidayOnly).ToList(),
            GetNights(solution, user.UserId).Select(a => a.Date.Date).ToList(),
            needed * 4,
            minGap);

        // اگر spread کافی نبود، همه تاریخ‌های مجاز را هم امتحان کن
        var dates = candidateDates
            .Concat(AllCandidateDates(constraints, holidayOnly))
            .Distinct()
            .ToList();

        foreach (var date in dates)
        {
            if (claimed >= needed)
            {
                break;
            }

            if (!IsPersonallyFeasibleNightDate(solution, constraints, user, nightShift, date, holidayOnly, ignoreHolidayReservation))
            {
                continue;
            }

            if (ViolatesNightSpacing(solution, constraints, user, date, minGap))
            {
                continue;
            }

            if (!CanAcceptNightAfterClearing(solution, constraints, user, nightShift, date))
            {
                continue;
            }

            if (HasSpecialtyCapacity(solution, constraints, nightShift, date, user.SpecialtyId))
            {
                ClearConflictingForNight(solution, constraints, user, date);
                solution.AddAssignment(user.UserId, nightShift.ShiftId, date, ShiftLabel.Night, isOnCall: false);
                claimed++;
                continue;
            }

            var donorAssignment = FindBestDonorAssignment(solution, constraints, nightShift, date, user.UserId, holidayOnly);
            if (donorAssignment == null)
            {
                continue;
            }

            ClearConflictingForNight(solution, constraints, user, date);
            solution.RemoveAssignment(donorAssignment.UserId, donorAssignment.ShiftId, donorAssignment.Date);
            solution.AddAssignment(user.UserId, nightShift.ShiftId, date, ShiftLabel.Night, isOnCall: false);
            claimed++;
        }
    }

    /// <summary>
    /// وقتی صندلی مناسب گیرنده در اختیار نفر روی کف سهمیه است، او را با شب مازاد فرد سوم جبران می‌کند.
    /// R ← date (از O)، O ← Ds (از S مازاد)، S از دست می‌دهد.
    /// </summary>
    private static void ClaimViaTwoHopChain(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        ShiftRequirement nightShift,
        int needed,
        bool holidayOnly,
        int minGap,
        bool ignoreHolidayReservation = false)
    {
        var claimed = 0;
        var dates = AllCandidateDates(constraints, holidayOnly).ToList();

        while (claimed < needed)
        {
            var progress = false;

            foreach (var date in dates)
            {
                if (claimed >= needed)
                {
                    break;
                }

                if (!IsPersonallyFeasibleNightDate(solution, constraints, user, nightShift, date, holidayOnly, ignoreHolidayReservation)
                    || ViolatesNightSpacing(solution, constraints, user, date, minGap)
                    || !CanAcceptNightAfterClearing(solution, constraints, user, nightShift, date))
                {
                    continue;
                }

                var occupant = solution.GetShiftAssignments(nightShift.ShiftId, date)
                    .FirstOrDefault(a => !a.IsOnCall && a.UserId != user.UserId);
                if (occupant == null)
                {
                    continue;
                }

                var bridge = constraints.UserConstraints.FirstOrDefault(u => u.UserId == occupant.UserId);
                if (bridge == null || IsProtected(constraints, bridge.UserId, occupant))
                {
                    continue;
                }

                // اگر bridge خودش می‌تواند اهدا کند، ادعای مستقیم کافی است (اینجا فقط زنجیره)
                if (CanDonateNight(solution, constraints, bridge, occupant, forHolidayClaim: holidayOnly))
                {
                    ClearConflictingForNight(solution, constraints, user, date);
                    solution.RemoveAssignment(occupant.UserId, occupant.ShiftId, occupant.Date);
                    solution.AddAssignment(user.UserId, nightShift.ShiftId, date, ShiftLabel.Night, false);
                    claimed++;
                    progress = true;
                    break;
                }

                var surplusNight = FindSurplusNightForBridge(
                    solution, constraints, nightShift, bridge, user.UserId, date, holidayOnly);
                if (surplusNight == null)
                {
                    continue;
                }

                var surplusDonor = constraints.UserConstraints.FirstOrDefault(u => u.UserId == surplusNight.UserId);
                if (surplusDonor == null
                    || !CanDonateNight(solution, constraints, surplusDonor, surplusNight, forHolidayClaim: holidayOnly))
                {
                    continue;
                }

                var ds = surplusNight.Date.Date;
                if (!CanUserAcceptNightAfterLeaving(solution, constraints, bridge, nightShift, leaveDate: date, takeDate: ds, minGap)
                    || !HasSpecialtyCapacityIgnoring(solution, constraints, nightShift, ds, bridge.SpecialtyId, surplusNight.UserId)
                    || !HasSpecialtyCapacityIgnoring(solution, constraints, nightShift, date, user.SpecialtyId, bridge.UserId))
                {
                    continue;
                }

                ClearConflictingForNight(solution, constraints, bridge, ds);
                ClearConflictingForNight(solution, constraints, user, date);
                solution.RemoveAssignment(surplusNight.UserId, surplusNight.ShiftId, ds);
                solution.RemoveAssignment(bridge.UserId, nightShift.ShiftId, date);
                solution.AddAssignment(bridge.UserId, nightShift.ShiftId, ds, ShiftLabel.Night, false);
                solution.AddAssignment(user.UserId, nightShift.ShiftId, date, ShiftLabel.Night, false);
                claimed++;
                progress = true;
                break;
            }

            if (!progress)
            {
                break;
            }
        }
    }

    /// <summary>
    /// اگر فاصلهٔ شب‌های خود گیرنده مانع گرفتن شب مازاد است، یکی از شب‌های مسدودکننده را جابه‌جا می‌کند.
    /// </summary>
    private static void RelocateOwnNightsThenClaim(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        ShiftRequirement nightShift,
        int needed,
        bool holidayOnly,
        int minGap)
    {
        var surplusDates = constraints.UserConstraints
            .SelectMany(donor =>
            {
                var nights = GetNights(solution, donor.UserId);
                var surplus = nights.Count - (donor.ExactNightShiftCount ?? int.MaxValue);
                if (surplus <= 0 && donor.HasExactNightQuota)
                {
                    return Array.Empty<(UserConstraint Donor, SaShiftAssignment Night)>();
                }

                if (!donor.HasExactNightQuota)
                {
                    surplus = nights.Count;
                }

                return nights
                    .Where(a => !IsProtected(constraints, donor.UserId, a))
                    .Where(a => CanDonateNight(solution, constraints, donor, a, forHolidayClaim: holidayOnly))
                    .OrderByDescending(a => DonorPriority(solution, constraints, donor, a, holidayOnly))
                    .Take(Math.Max(1, surplus))
                    .Select(a => (Donor: donor, Night: a));
            })
            .ToList();

        foreach (var (donor, surplusNight) in surplusDates)
        {
            if (needed <= 0)
            {
                break;
            }

            var date = surplusNight.Date.Date;
            if (holidayOnly && !constraints.IsHolidayWeekendNight(date))
            {
                continue;
            }

            if (!IsPersonallyFeasibleNightDate(solution, constraints, user, nightShift, date, holidayOnly)
                || !CanAcceptNightAfterClearing(solution, constraints, user, nightShift, date))
            {
                continue;
            }

            if (!ViolatesNightSpacing(solution, constraints, user, date, minGap))
            {
                // فاصله OK — ادعای مستقیم
                ClearConflictingForNight(solution, constraints, user, date);
                solution.RemoveAssignment(surplusNight.UserId, surplusNight.ShiftId, date);
                solution.AddAssignment(user.UserId, nightShift.ShiftId, date, ShiftLabel.Night, false);
                needed--;
                continue;
            }

            // یکی از شب‌های نزدیک را جابه‌جا کن تا فاصله باز شود
            var blocking = GetNights(solution, user.UserId)
                .Where(a => Math.Abs((a.Date.Date - date).Days) <= minGap)
                .Where(a => !IsProtected(constraints, user.UserId, a))
                .OrderBy(a => Math.Abs((a.Date.Date - date).Days))
                .FirstOrDefault();
            if (blocking == null)
            {
                continue;
            }

            var moved = TryMoveNightToAnyFeasibleDate(
                solution, constraints, user, nightShift, blocking.Date.Date, excludeDate: date, minGap);
            if (!moved)
            {
                continue;
            }

            if (ViolatesNightSpacing(solution, constraints, user, date, minGap)
                || !CanAcceptNightAfterClearing(solution, constraints, user, nightShift, date)
                || !CanDonateNight(solution, constraints, donor, surplusNight, forHolidayClaim: holidayOnly))
            {
                continue;
            }

            ClearConflictingForNight(solution, constraints, user, date);
            solution.RemoveAssignment(surplusNight.UserId, surplusNight.ShiftId, date);
            solution.AddAssignment(user.UserId, nightShift.ShiftId, date, ShiftLabel.Night, false);
            needed--;
        }

        if (needed > 0)
        {
            var before = CountNights(solution, user.UserId);
            ClaimNightsFromDonors(solution, constraints, user, nightShift, needed, holidayOnly, minGap);
            needed -= CountNights(solution, user.UserId) - before;
            if (needed > 0)
            {
                ClaimViaTwoHopChain(solution, constraints, user, nightShift, needed, holidayOnly, minGap);
            }
        }
    }

    private static SaShiftAssignment? FindSurplusNightForBridge(
        ShiftSolution solution,
        ShiftConstraints constraints,
        ShiftRequirement nightShift,
        UserConstraint bridge,
        int receiverUserId,
        DateTime bridgeLeaveDate,
        bool holidayClaim)
    {
        return constraints.UserConstraints
            .Where(d => d.UserId != bridge.UserId && d.UserId != receiverUserId)
            .SelectMany(donor => GetNights(solution, donor.UserId)
                .Where(a => a.Date.Date != bridgeLeaveDate.Date)
                .Where(a => !IsProtected(constraints, donor.UserId, a))
                .Where(a => CanDonateNight(solution, constraints, donor, a, forHolidayClaim: holidayClaim))
                .Select(a => (Donor: donor, Night: a)))
            .OrderBy(x => DonorPriority(solution, constraints, x.Donor, x.Night, holidayClaim))
            .Select(x => x.Night)
            .FirstOrDefault();
    }

    private static bool TryMoveNightToAnyFeasibleDate(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        ShiftRequirement nightShift,
        DateTime fromDate,
        DateTime excludeDate,
        int minGap)
    {
        var candidates = AllCandidateDates(constraints, holidayOnly: false)
            .Where(d => d != fromDate && d != excludeDate)
            .Where(d => !GetNights(solution, user.UserId).Any(n => n.Date.Date == d))
            .Where(d =>
            {
                var others = GetNights(solution, user.UserId)
                    .Where(n => n.Date.Date != fromDate)
                    .Select(n => n.Date.Date);
                return !others.Any(o => Math.Abs((o - d).Days) <= minGap);
            })
            .OrderBy(d => Math.Abs((d - fromDate).Days))
            .ToList();

        foreach (var target in candidates)
        {
            // نادیده گرفتن شب فعلی از تاریخ from برای بررسی تداخل
            if (AdjacentShiftRestRules.WouldConflict(
                    AssignmentsIgnoringClearableForNight(solution, constraints, user, target)
                        .Where(a => !(a.ShiftLabel == ShiftLabel.Night && a.Date.Date == fromDate)),
                    target,
                    ShiftLabel.Night,
                    constraints))
            {
                continue;
            }

            if (user.UnavailableDates.Any(d => d.Date == target)
                || user.UnavailableShiftSlots.Any(s => s.Date.Date == target && s.ShiftLabel == ShiftLabel.Night)
                || !ShiftEligibilityResolver.MayEverTakeLabel(user, ShiftLabel.Night))
            {
                continue;
            }

            if (HasSpecialtyCapacity(solution, constraints, nightShift, target, user.SpecialtyId))
            {
                ClearConflictingForNight(solution, constraints, user, target);
                solution.RemoveAssignment(user.UserId, nightShift.ShiftId, fromDate);
                solution.AddAssignment(user.UserId, nightShift.ShiftId, target, ShiftLabel.Night, false);
                return true;
            }

            var donorAssignment = FindBestDonorAssignment(solution, constraints, nightShift, target, user.UserId, holidayClaim: false);
            if (donorAssignment == null)
            {
                continue;
            }

            ClearConflictingForNight(solution, constraints, user, target);
            solution.RemoveAssignment(user.UserId, nightShift.ShiftId, fromDate);
            solution.RemoveAssignment(donorAssignment.UserId, donorAssignment.ShiftId, donorAssignment.Date);
            solution.AddAssignment(user.UserId, nightShift.ShiftId, target, ShiftLabel.Night, false);
            return true;
        }

        return false;
    }

    private static bool CanUserAcceptNightAfterLeaving(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        ShiftRequirement nightShift,
        DateTime leaveDate,
        DateTime takeDate,
        int minGap)
    {
        if (user.UnavailableDates.Any(d => d.Date == takeDate.Date)
            || user.UnavailableShiftSlots.Any(s => s.Date.Date == takeDate.Date && s.ShiftLabel == ShiftLabel.Night)
            || !ShiftEligibilityResolver.MayEverTakeLabel(user, ShiftLabel.Night)
            || solution.HasAssignment(user.UserId, nightShift.ShiftId, takeDate))
        {
            return false;
        }

        var otherNights = GetNights(solution, user.UserId)
            .Where(a => a.Date.Date != leaveDate.Date)
            .Select(a => a.Date.Date);
        if (otherNights.Any(d => Math.Abs((d - takeDate.Date).Days) <= minGap))
        {
            return false;
        }

        var assignments = AssignmentsIgnoringClearableForNight(solution, constraints, user, takeDate)
            .Where(a => !(a.ShiftLabel == ShiftLabel.Night && a.Date.Date == leaveDate.Date));
        if (AdjacentShiftRestRules.WouldConflict(assignments, takeDate, ShiftLabel.Night, constraints))
        {
            return false;
        }

        var labels = solution.GetUserAssignments(user.UserId, takeDate)
            .Where(a => !(a.ShiftLabel == ShiftLabel.Night && a.Date.Date == takeDate.Date))
            .Select(a => a.ShiftLabel);
        return DailyAssignmentRules.CanAddShift(labels, ShiftLabel.Night, GetMaxShiftsPerDay(constraints));
    }

    private static bool HasSpecialtyCapacityIgnoring(
        ShiftSolution solution,
        ShiftConstraints constraints,
        ShiftRequirement nightShift,
        DateTime date,
        int specialtyId,
        int ignoreUserId)
    {
        var specialtyReq = nightShift.SpecialtyRequirements.FirstOrDefault(r => r.SpecialtyId == specialtyId)
                           ?? nightShift.SpecialtyRequirements.FirstOrDefault();
        if (specialtyReq == null)
        {
            return true;
        }

        var day = specialtyReq.ForDay(constraints.IsHoliday(date));
        var current = solution.GetShiftAssignments(nightShift.ShiftId, date)
            .Count(a => !a.IsOnCall
                        && a.UserId != ignoreUserId
                        && GetSpecialty(constraints, a.UserId) == specialtyReq.SpecialtyId);
        return current < Math.Max(day.RequiredTotalCount, 1);
    }

    private static SaShiftAssignment? FindBestDonorAssignment(
        ShiftSolution solution,
        ShiftConstraints constraints,
        ShiftRequirement nightShift,
        DateTime date,
        int receiverUserId,
        bool holidayClaim)
    {
        var occupants = solution.GetShiftAssignments(nightShift.ShiftId, date)
            .Where(a => !a.IsOnCall && a.UserId != receiverUserId)
            .ToList();

        return occupants
            .Select(a =>
            {
                var donor = constraints.UserConstraints.FirstOrDefault(u => u.UserId == a.UserId);
                return (Assignment: a, Donor: donor);
            })
            .Where(x => x.Donor != null && CanDonateNight(solution, constraints, x.Donor!, x.Assignment, forHolidayClaim: holidayClaim))
            .OrderBy(x => DonorPriority(solution, constraints, x.Donor!, x.Assignment, holidayClaim))
            .Select(x => x.Assignment)
            .FirstOrDefault();
    }

    private static int DonorPriority(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint donor,
        SaShiftAssignment assignment,
        bool holidayClaim)
    {
        var nights = GetNights(solution, donor.UserId);
        var holiday = nights.Count(a => constraints.IsHolidayWeekendNight(a.Date));
        var totalSurplus = nights.Count - (donor.ExactNightShiftCount ?? 0);
        var holidaySurplus = holiday - (donor.ExactHolidayWeekendNightShiftCount ?? 0);

        // بدون سهمیه = بهترین اهداکننده
        if (!donor.HasExactNightQuota && !donor.ExactHolidayWeekendNightShiftCount.HasValue)
        {
            return -10000 - nights.Count;
        }

        if (holidayClaim)
        {
            return -(holidaySurplus * 100 + totalSurplus * 10 + nights.Count);
        }

        return -(totalSurplus * 100 + holidaySurplus * 10 + nights.Count);
    }

    /// <summary>
    /// آیا اهداکننده می‌تواند این شب را از دست بدهد بدون افت زیر حداقل؟
    /// برای ادعای شب تعطیل، اگر مازاد تعطیل دارد حتی روی کف ExactNight هم می‌تواند اهدا کند
    /// (کسری ExactNight در پاس بعدی Enforce جبران می‌شود).
    /// </summary>
    public static bool CanDonateNight(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        SaShiftAssignment assignment,
        bool forHolidayClaim = false)
    {
        if (assignment.ShiftLabel != ShiftLabel.Night || assignment.IsOnCall)
        {
            return true;
        }

        if (IsProtected(constraints, user.UserId, assignment))
        {
            return false;
        }

        var nights = GetNights(solution, user.UserId);
        var isHolidayNight = constraints.IsHolidayWeekendNight(assignment.Date);
        var holidayNights = nights.Count(a => constraints.IsHolidayWeekendNight(a.Date));
        var holidaySurplus = user.ExactHolidayWeekendNightShiftCount.HasValue
            ? holidayNights - user.ExactHolidayWeekendNightShiftCount.Value
            : holidayNights;

        var remainingTotal = nights.Count - 1;
        if (user.ExactNightShiftCount.HasValue && remainingTotal < user.ExactNightShiftCount.Value)
        {
            // مازاد شب تعطیل را برای رفع کسری تعطیل دیگران قفل نکن
            if (!(forHolidayClaim && isHolidayNight && holidaySurplus > 0))
            {
                return false;
            }
        }

        if (user.ExactHolidayWeekendNightShiftCount.HasValue && isHolidayNight)
        {
            if (holidayNights - 1 < user.ExactHolidayWeekendNightShiftCount.Value)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// اگر سقف سهمیه ترکیبی صبح/شب (fallback=false) پر است، برای افزودن شب جدید ابتدا صبح غیرمحافظت‌شده حذف می‌شود.
    /// </summary>
    private static void MakeRoomForMorningNightComboQuota(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user)
    {
        if (!ComboShiftQuotaEligibility.HasComboQuotaConfigured(user))
        {
            return;
        }

        var userStub = new Domain.Entities.UserModel.User
        {
            Id = user.UserId,
            ShiftType = user.ShiftType,
            ShiftSubType = user.ShiftSubType,
            TwoShiftRotationPattern = user.TwoShiftRotationPattern
        };
        if (!ShiftQuotaTypeValidator.CanUseMorningNightPattern(userStub, user.AllowedShiftPermissions))
        {
            return;
        }

        var maxTotal = ComboShiftQuotaEligibility.GetMaxAllowedMorningNightTotal(user);
        if (maxTotal == int.MaxValue)
        {
            return;
        }

        while (ComboShiftQuotaEligibility.CountMorningNightAssignments(solution, user.UserId) >= maxTotal)
        {
            var removableMorning = solution.GetUserAllAssignments(user.UserId)
                .Where(a => !a.IsOnCall && a.ShiftLabel == ShiftLabel.Morning)
                .Where(a => !IsProtected(constraints, user.UserId, a))
                .OrderByDescending(a => a.Date)
                .FirstOrDefault();
            if (removableMorning != null)
            {
                solution.RemoveAssignment(removableMorning.UserId, removableMorning.ShiftId, removableMorning.Date);
                continue;
            }

            break;
        }
    }

    /// <summary>
    /// شیفت‌های غیرمحافظت‌شدهٔ روز بعد از شب‌های فعلی را پاک می‌کند تا افزودن شب جدید مسدود نشود.
    /// </summary>
    private static void ClearStalePostNightConflicts(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user)
    {
        foreach (var night in GetNights(solution, user.UserId))
        {
            RemoveForbiddenOnDayAfterNight(solution, constraints, user, night.Date.Date.AddDays(1));
        }
    }

    private static void RemoveForbiddenOnDayAfterNight(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        DateTime date)
    {
        foreach (var assignment in solution.GetUserAssignments(user.UserId, date).ToList())
        {
            if (IsProtected(constraints, user.UserId, assignment))
            {
                continue;
            }

            if (constraints.HardRules.IsForbiddenOnDayAfterNight(assignment.ShiftLabel))
            {
                solution.RemoveAssignment(assignment.UserId, assignment.ShiftId, assignment.Date);
            }
        }
    }

    private static void ClearConflictingForNight(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        DateTime date)
    {
        var day = date.Date;
        var next = day.AddDays(1);
        var maxPerDay = GetMaxShiftsPerDay(constraints);

        // عصر همان روز با شب متوالی است
        RemoveClearableAssignments(solution, constraints, user, day, ShiftLabel.Evening);

        // با سقف ۱ شیفت در روز (یا بدون مجوز صبح+شب)، هر شیفت دیگر همان روز باید کنار برود
        var canKeepSameDayMorning = maxPerDay >= 2
                                    && ShiftEligibilityResolver.SupportsMorningNightCombo(user)
                                    && ShiftEligibilityResolver.MayEverTakeLabel(user, ShiftLabel.Morning);
        if (!canKeepSameDayMorning)
        {
            RemoveClearableAssignments(solution, constraints, user, day, ShiftLabel.Morning);
        }

        // روز بعد از شب
        RemoveForbiddenOnDayAfterNight(solution, constraints, user, next);

        // شب روز قبل (شب متوالی)
        if (!constraints.HardRules.AllowNightShiftAfterNightShift)
        {
            RemoveClearableAssignments(solution, constraints, user, day.AddDays(-1), ShiftLabel.Night);
        }
    }

    private static void RemoveClearableAssignments(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        DateTime date,
        ShiftLabel label)
    {
        var toRemove = solution.GetUserAssignments(user.UserId, date)
            .Where(a => !a.IsOnCall)
            .Where(a => a.ShiftLabel == label)
            .Where(a => !IsProtected(constraints, user.UserId, a))
            .ToList();

        foreach (var a in toRemove)
        {
            solution.RemoveAssignment(a.UserId, a.ShiftId, a.Date);
        }
    }

    private static void RemoveAllClearableOnDate(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        DateTime date)
    {
        var toRemove = solution.GetUserAssignments(user.UserId, date)
            .Where(a => !a.IsOnCall)
            .Where(a => !IsProtected(constraints, user.UserId, a))
            .ToList();

        foreach (var a in toRemove)
        {
            solution.RemoveAssignment(a.UserId, a.ShiftId, a.Date);
        }
    }

    private static IEnumerable<SaShiftAssignment> AssignmentsIgnoringClearableForNight(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        DateTime nightDate)
    {
        var day = nightDate.Date;
        var next = day.AddDays(1);
        var userNightDates = GetNights(solution, user.UserId).Select(a => a.Date.Date).ToHashSet();
        var maxPerDay = GetMaxShiftsPerDay(constraints);
        var canKeepSameDayMorning = maxPerDay >= 2
                                    && ShiftEligibilityResolver.SupportsMorningNightCombo(user)
                                    && ShiftEligibilityResolver.MayEverTakeLabel(user, ShiftLabel.Morning);

        return solution.GetUserAllAssignments(user.UserId)
            .Where(a =>
            {
                if (a.IsOnCall)
                {
                    return true;
                }

                if (IsProtected(constraints, user.UserId, a))
                {
                    return true;
                }

                if (a.Date.Date == day && a.ShiftLabel == ShiftLabel.Evening)
                {
                    return false;
                }

                if (a.Date.Date == day
                    && a.ShiftLabel == ShiftLabel.Morning
                    && !canKeepSameDayMorning)
                {
                    return false;
                }

                if (a.Date.Date == day.AddDays(-1)
                    && a.ShiftLabel == ShiftLabel.Night
                    && !constraints.HardRules.AllowNightShiftAfterNightShift)
                {
                    return false;
                }

                // روز بعد از شبِ کاندید یا شب موجود
                if (a.Date.Date == next || userNightDates.Contains(a.Date.Date.AddDays(-1)))
                {
                    if (constraints.HardRules.IsForbiddenOnDayAfterNight(a.ShiftLabel))
                    {
                        return false;
                    }
                }

                return true;
            });
    }

    private static bool CanAcceptNightAfterClearing(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        ShiftRequirement nightShift,
        DateTime date)
    {
        var protectedEvening = solution.GetUserAssignments(user.UserId, date)
            .Where(a => !a.IsOnCall)
            .Where(a => a.ShiftLabel == ShiftLabel.Evening)
            .Any(a => IsProtected(constraints, user.UserId, a));
        if (protectedEvening)
        {
            return false;
        }

        var nextDay = date.Date.AddDays(1);
        var protectedBlockingNextDay = solution.GetUserAssignments(user.UserId, nextDay)
            .Where(a => !a.IsOnCall)
            .Where(a => IsProtected(constraints, user.UserId, a))
            .Any(a => constraints.HardRules.IsForbiddenOnDayAfterNight(a.ShiftLabel));
        if (protectedBlockingNextDay)
        {
            return false;
        }

        var prevDay = date.Date.AddDays(-1);
        var protectedPrevNight = solution.GetUserAssignments(user.UserId, prevDay)
            .Where(a => !a.IsOnCall)
            .Where(a => a.ShiftLabel == ShiftLabel.Night)
            .Any(a => IsProtected(constraints, user.UserId, a));
        if (protectedPrevNight && !constraints.HardRules.AllowNightShiftAfterNightShift)
        {
            return false;
        }

        if (AdjacentShiftRestRules.WouldConflict(
                AssignmentsIgnoringClearableForNight(solution, constraints, user, date),
                date,
                ShiftLabel.Night,
                constraints))
        {
            return false;
        }

        var maxPerDay = GetMaxShiftsPerDay(constraints);
        var canKeepSameDayMorning = maxPerDay >= 2
                                    && ShiftEligibilityResolver.SupportsMorningNightCombo(user)
                                    && ShiftEligibilityResolver.MayEverTakeLabel(user, ShiftLabel.Morning);
        var labels = solution.GetUserAssignments(user.UserId, date)
            .Where(a =>
            {
                if (a.IsOnCall)
                {
                    return true;
                }

                if (a.Date.Date == date.Date && a.ShiftLabel == ShiftLabel.Evening && !IsProtected(constraints, user.UserId, a))
                {
                    return false;
                }

                if (a.Date.Date == date.Date && a.ShiftLabel == ShiftLabel.Morning && !canKeepSameDayMorning && !IsProtected(constraints, user.UserId, a))
                {
                    return false;
                }

                return true;
            })
            .Select(a => a.ShiftLabel);
        if (!DailyAssignmentRules.CanAddShift(labels, ShiftLabel.Night, maxPerDay))
        {
            return false;
        }

        return ShiftEligibilityResolver.MayEverTakeLabel(user, ShiftLabel.Night)
               && !user.UnavailableDates.Any(d => d.Date == date.Date)
               && !user.UnavailableShiftSlots.Any(s => s.Date.Date == date.Date && s.ShiftLabel == ShiftLabel.Night)
               && !solution.HasAssignment(user.UserId, nightShift.ShiftId, date)
               && !MaxConsecutiveWorkdayRules.WouldExceedMaxConsecutiveWorkdays(
                   solution, constraints, user, date);
    }

    private static IEnumerable<DateTime> AllCandidateDates(ShiftConstraints constraints, bool holidayOnly) =>
        Enumerable.Range(0, (constraints.EndDate.Date - constraints.StartDate.Date).Days + 1)
            .Select(offset => constraints.StartDate.Date.AddDays(offset))
            .Where(d => !holidayOnly || constraints.IsHolidayWeekendNight(d));

    /// <summary>
    /// جابه‌جایی شب‌های موجود به تاریخ‌های خلوت‌تر بدون خالی گذاشتن ظرفیت (در صورت نیاز swap).
    /// </summary>
    private static void ImproveNightSpread(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        ShiftRequirement nightShift)
    {
        var minGap = ResolveNightSpacingGap(constraints, user);

        for (var iter = 0; iter < 24; iter++)
        {
            var nights = GetNights(solution, user.UserId)
                .Where(a => !IsProtected(constraints, user.UserId, a))
                .ToList();
            if (nights.Count < 2)
            {
                return;
            }

            var currentDates = GetNights(solution, user.UserId).Select(a => a.Date.Date).ToList();
            var currentPenalty = CalculateSpreadPenalty(currentDates, constraints.StartDate, constraints.EndDate);

            var allDates = Enumerable.Range(0, (constraints.EndDate.Date - constraints.StartDate.Date).Days + 1)
                .Select(offset => constraints.StartDate.Date.AddDays(offset))
                .ToList();

            SaShiftAssignment? bestFrom = null;
            DateTime? bestTo = null;
            int? swapUserId = null;
            var bestPenalty = currentPenalty;

            foreach (var night in nights)
            {
                foreach (var target in allDates)
                {
                    if (target == night.Date.Date || currentDates.Contains(target))
                    {
                        continue;
                    }

                    if (user.ExactHolidayWeekendNightShiftCount.HasValue)
                    {
                        var fromHol = constraints.IsHolidayWeekendNight(night.Date);
                        var toHol = constraints.IsHolidayWeekendNight(target);
                        if (fromHol != toHol)
                        {
                            var holidayCount = currentDates.Count(d => constraints.IsHolidayWeekendNight(d));
                            var projected = holidayCount - (fromHol ? 1 : 0) + (toHol ? 1 : 0);
                            if (projected < user.ExactHolidayWeekendNightShiftCount.Value)
                            {
                                continue;
                            }
                        }
                    }

                    var projectedDates = currentDates.Where(d => d != night.Date.Date).Append(target).ToList();
                    if (projectedDates.Where(d => d != target).Any(d => Math.Abs((d - target).Days) <= minGap))
                    {
                        continue;
                    }

                    var projectedPenalty = CalculateSpreadPenalty(projectedDates, constraints.StartDate, constraints.EndDate);
                    if (projectedPenalty >= bestPenalty - 0.01)
                    {
                        continue;
                    }

                    if (IsFeasibleNightDate(solution, constraints, user, nightShift, target, holidayOnly: false, ignoreUserNightOnDate: false)
                        && HasSpecialtyCapacity(solution, constraints, nightShift, target, user.SpecialtyId))
                    {
                        bestPenalty = projectedPenalty;
                        bestFrom = night;
                        bestTo = target;
                        swapUserId = null;
                        continue;
                    }

                    var occupant = solution.GetShiftAssignments(nightShift.ShiftId, target)
                        .FirstOrDefault(a => !a.IsOnCall && a.UserId != user.UserId);
                    if (occupant == null)
                    {
                        continue;
                    }

                    var other = constraints.UserConstraints.FirstOrDefault(u => u.UserId == occupant.UserId);
                    if (other == null || !CanDonateNight(solution, constraints, other, occupant))
                    {
                        continue;
                    }

                    if (!CanSwapNight(solution, constraints, user, other, night.Date.Date, target, nightShift, minGap))
                    {
                        continue;
                    }

                    bestPenalty = projectedPenalty;
                    bestFrom = night;
                    bestTo = target;
                    swapUserId = other.UserId;
                }
            }

            if (bestFrom == null || bestTo == null)
            {
                return;
            }

            var fromDate = bestFrom.Date.Date;
            var toDate = bestTo.Value.Date;

            if (swapUserId.HasValue)
            {
                solution.RemoveAssignment(user.UserId, nightShift.ShiftId, fromDate);
                solution.RemoveAssignment(swapUserId.Value, nightShift.ShiftId, toDate);
                solution.AddAssignment(user.UserId, nightShift.ShiftId, toDate, ShiftLabel.Night, false);
                solution.AddAssignment(swapUserId.Value, nightShift.ShiftId, fromDate, ShiftLabel.Night, false);
            }
            else
            {
                solution.RemoveAssignment(user.UserId, nightShift.ShiftId, fromDate);
                solution.AddAssignment(user.UserId, nightShift.ShiftId, toDate, ShiftLabel.Night, false);
            }
        }
    }

    private static bool CanSwapNight(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        UserConstraint other,
        DateTime userFrom,
        DateTime userTo,
        ShiftRequirement nightShift,
        int minGap)
    {
        if (other.UnavailableDates.Any(d => d.Date == userFrom) ||
            user.UnavailableDates.Any(d => d.Date == userTo))
        {
            return false;
        }

        if (AdjacentShiftRestRules.WouldConflict(
                solution.GetUserAllAssignments(user.UserId).Where(a => !(a.ShiftLabel == ShiftLabel.Night && a.Date.Date == userFrom)),
                userTo, ShiftLabel.Night, constraints))
        {
            return false;
        }

        if (AdjacentShiftRestRules.WouldConflict(
                solution.GetUserAllAssignments(other.UserId).Where(a => !(a.ShiftLabel == ShiftLabel.Night && a.Date.Date == userTo)),
                userFrom, ShiftLabel.Night, constraints))
        {
            return false;
        }

        var userOtherNights = GetNights(solution, user.UserId)
            .Where(a => a.Date.Date != userFrom)
            .Select(a => a.Date.Date);
        if (userOtherNights.Any(d => Math.Abs((d - userTo).Days) <= minGap))
        {
            return false;
        }

        var otherLabelsAtFrom = solution.GetUserAssignments(other.UserId, userFrom)
            .Where(a => !(a.ShiftLabel == ShiftLabel.Night && a.Date.Date == userFrom))
            .Select(a => a.ShiftLabel);
        if (!DailyAssignmentRules.CanAddShift(otherLabelsAtFrom, ShiftLabel.Night, GetMaxShiftsPerDay(constraints)))
        {
            return false;
        }

        var userLabelsAtTo = solution.GetUserAssignments(user.UserId, userTo)
            .Where(a => !(a.ShiftId == nightShift.ShiftId && a.Date.Date == userTo))
            .Select(a => a.ShiftLabel);
        if (!DailyAssignmentRules.CanAddShift(userLabelsAtTo, ShiftLabel.Night, GetMaxShiftsPerDay(constraints)))
        {
            return false;
        }

        return ShiftEligibilityResolver.MayEverTakeLabel(other, ShiftLabel.Night)
               && ShiftEligibilityResolver.MayEverTakeLabel(user, ShiftLabel.Night);
    }

    public static List<DateTime> PickSpreadDates(
        IReadOnlyList<DateTime> candidates,
        IReadOnlyList<DateTime> alreadyOccupied,
        int needed,
        int minGapDays)
    {
        var result = new List<DateTime>();
        if (needed <= 0 || candidates.Count == 0)
        {
            return result;
        }

        var sorted = candidates.Select(d => d.Date).Distinct().OrderBy(d => d).ToList();
        var occupied = alreadyOccupied.Select(d => d.Date).ToHashSet();

        for (var i = 0; i < needed; i++)
        {
            var idealIdx = needed == 1
                ? (sorted.Count - 1) / 2.0
                : i * (sorted.Count - 1.0) / Math.Max(1, needed - 1);

            var pick = sorted
                .Select((d, idx) => (d, idx))
                .Where(x => !occupied.Contains(x.d) && !result.Contains(x.d))
                .Where(x => occupied.Concat(result).All(o => Math.Abs((x.d - o).Days) > minGapDays))
                .OrderBy(x => Math.Abs(x.idx - idealIdx))
                .ThenByDescending(x =>
                {
                    var others = occupied.Concat(result).ToList();
                    return others.Count == 0 ? 1000 : others.Min(o => Math.Abs((x.d - o).Days));
                })
                .Select(x => (DateTime?)x.d)
                .FirstOrDefault();

            if (pick == null)
            {
                pick = sorted
                    .Select((d, idx) => (d, idx))
                    .Where(x => !occupied.Contains(x.d) && !result.Contains(x.d))
                    .OrderBy(x => Math.Abs(x.idx - idealIdx))
                    .Select(x => (DateTime?)x.d)
                    .FirstOrDefault();
            }

            if (pick == null)
            {
                break;
            }

            result.Add(pick.Value);
        }

        return result;
    }

    private static bool IsFeasibleNightDate(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        ShiftRequirement nightShift,
        DateTime date,
        bool holidayOnly,
        bool ignoreUserNightOnDate)
    {
        if (!IsPersonallyFeasibleNightDate(solution, constraints, user, nightShift, date, holidayOnly))
        {
            return false;
        }

        if (!ignoreUserNightOnDate && solution.HasAssignment(user.UserId, nightShift.ShiftId, date))
        {
            return false;
        }

        var existingLabels = solution.GetUserAssignments(user.UserId, date)
            .Where(a => !(ignoreUserNightOnDate && a.ShiftLabel == ShiftLabel.Night))
            .Where(a => !(a.Date.Date == date.Date && a.ShiftLabel == ShiftLabel.Evening && !IsProtected(constraints, user.UserId, a)))
            .Select(a => a.ShiftLabel);
        if (!DailyAssignmentRules.CanAddShift(existingLabels, ShiftLabel.Night, GetMaxShiftsPerDay(constraints)))
        {
            return false;
        }

        var forAdjacency = AssignmentsIgnoringClearableForNight(solution, constraints, user, date)
            .Where(a => !(ignoreUserNightOnDate && a.ShiftLabel == ShiftLabel.Night && a.Date.Date == date.Date));
        if (AdjacentShiftRestRules.WouldConflict(forAdjacency, date, ShiftLabel.Night, constraints))
        {
            return false;
        }

        return HasSpecialtyCapacity(solution, constraints, nightShift, date, user.SpecialtyId);
    }

    private static bool IsPersonallyFeasibleNightDate(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        ShiftRequirement nightShift,
        DateTime date,
        bool holidayOnly,
        bool ignoreHolidayReservation = false)
    {
        if (holidayOnly && !constraints.IsHolidayWeekendNight(date))
        {
            return false;
        }

        // رزرو ظرفیت باقی‌مانده برای تکمیل حداقل شب تعطیل
        if (!ignoreHolidayReservation && !holidayOnly && user.ExactHolidayWeekendNightShiftCount.HasValue)
        {
            var holidayCount = GetNights(solution, user.UserId).Count(a => constraints.IsHolidayWeekendNight(a.Date));
            var totalCount = CountNights(solution, user.UserId);
            var remainingTotal = (user.ExactNightShiftCount ?? int.MaxValue) - totalCount;
            var remainingHoliday = user.ExactHolidayWeekendNightShiftCount.Value - holidayCount;
            if (!constraints.IsHolidayWeekendNight(date) && remainingHoliday > 0 && remainingTotal <= remainingHoliday)
            {
                return false;
            }
        }

        if (solution.HasAssignment(user.UserId, nightShift.ShiftId, date))
        {
            return false;
        }

        if (user.UnavailableDates.Any(d => d.Date == date.Date))
        {
            return false;
        }

        if (user.UnavailableShiftSlots.Any(s => s.Date.Date == date.Date && s.ShiftLabel == ShiftLabel.Night))
        {
            return false;
        }

        return ShiftEligibilityResolver.MayEverTakeLabel(user, ShiftLabel.Night)
               && !MaxConsecutiveWorkdayRules.WouldExceedMaxConsecutiveWorkdays(
                   solution, constraints, user, date);
    }

    private static bool HasSpecialtyCapacity(
        ShiftSolution solution,
        ShiftConstraints constraints,
        ShiftRequirement nightShift,
        DateTime date,
        int specialtyId)
    {
        var specialtyReq = nightShift.SpecialtyRequirements.FirstOrDefault(r => r.SpecialtyId == specialtyId)
                           ?? nightShift.SpecialtyRequirements.FirstOrDefault();
        if (specialtyReq == null)
        {
            return true;
        }

        var day = specialtyReq.ForDay(constraints.IsHoliday(date));
        var current = solution.GetShiftAssignments(nightShift.ShiftId, date)
            .Count(a => !a.IsOnCall && GetSpecialty(constraints, a.UserId) == specialtyReq.SpecialtyId);
        return current < Math.Max(day.RequiredTotalCount, 1);
    }

    private static int GetMaxShiftsPerDay(ShiftConstraints constraints) =>
        constraints.HardRules.EnforceMaxShiftsPerDay
            ? Math.Max(1, constraints.GlobalConstraints.MaxShiftsPerDay)
            : 2;

    private static int GetSpecialty(ShiftConstraints constraints, int userId) =>
        constraints.UserConstraints.FirstOrDefault(u => u.UserId == userId)?.SpecialtyId ?? 0;

    /// <summary>
    /// فاصلهٔ حداقل بین شب‌ها (بر حسب اختلاف روز تقویمی که هنوز مجاز نیست).
    /// اگر «شب روز بعد از شب» مجاز باشد → ۰ (شب متوالی تا سقف MaxConsecutiveNightShifts).
    /// در غیر این صورت حداقل ۱: شب‌های مجاور ممنوع؛ الگوی N / استراحت / N مجاز است.
    /// </summary>
    private static int ResolveNightSpacingGap(ShiftConstraints constraints, UserConstraint user)
    {
        if (constraints.HardRules.AllowNightShiftAfterNightShift)
        {
            return 0;
        }

        return Math.Max(1, user.MinDaysBetweenNightShifts);
    }

    private static bool ViolatesNightSpacing(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        DateTime candidateDate,
        int minGap)
    {
        foreach (var nightDate in GetNights(solution, user.UserId).Select(a => a.Date.Date))
        {
            if (Math.Abs((candidateDate.Date - nightDate).Days) <= minGap)
            {
                return true;
            }
        }

        if (minGap == 0 && WouldExceedMaxConsecutiveNights(solution, constraints, user, candidateDate))
        {
            return true;
        }

        return false;
    }

    private static bool WouldExceedMaxConsecutiveNights(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        DateTime candidateDate)
    {
        var maxRun = Math.Max(1, constraints.GlobalConstraints.MaxConsecutiveNightShifts);
        var nights = GetNights(solution, user.UserId)
            .Select(a => a.Date.Date)
            .Append(candidateDate.Date)
            .ToHashSet();

        var left = 0;
        for (var d = candidateDate.Date.AddDays(-1); nights.Contains(d); d = d.AddDays(-1))
        {
            left++;
        }

        var right = 0;
        for (var d = candidateDate.Date.AddDays(1); nights.Contains(d); d = d.AddDays(1))
        {
            right++;
        }

        return 1 + left + right > maxRun;
    }

    private static bool IsProtected(ShiftConstraints constraints, int userId, SaShiftAssignment assignment)
    {
        var user = constraints.UserConstraints.FirstOrDefault(u => u.UserId == userId);
        if (user == null)
        {
            return false;
        }

        return user.RequiredShiftSlots.Any(s =>
            s.Date.Date == assignment.Date.Date && s.ShiftLabel == assignment.ShiftLabel);
    }

    public static double CalculateSpreadPenalty(
        IReadOnlyList<DateTime> nightDates,
        DateTime rangeStart,
        DateTime rangeEnd)
    {
        if (nightDates.Count < 2)
        {
            return 0;
        }

        var ordered = nightDates.Select(d => d.Date).Distinct().OrderBy(d => d).ToList();
        var spanDays = Math.Max(1, (rangeEnd.Date - rangeStart.Date).Days);
        var idealGap = spanDays / (double)(ordered.Count - 1);
        double penalty = 0;
        for (var i = 1; i < ordered.Count; i++)
        {
            var gap = (ordered[i] - ordered[i - 1]).Days;
            var deficit = idealGap - gap;
            if (deficit > 0)
            {
                penalty += deficit * deficit;
            }
        }

        var mid = rangeStart.Date.AddDays(spanDays / 2.0);
        var inFirstHalf = ordered.Count(d => d < mid);
        var imbalance = Math.Abs(inFirstHalf - (ordered.Count - inFirstHalf));
        penalty += imbalance * 8;

        return penalty;
    }
}
