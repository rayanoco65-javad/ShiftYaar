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
            var deficits = OrderUsersByDeficit(solution, constraints).ToList();
            if (deficits.Count == 0) break;
            foreach (var user in deficits)
            {
                EnforceForUser(solution, constraints, user, nightShift);
            }
        }

        OptimizeSpread(solution, constraints);

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
                if (HasNightQuotaDeficit(solution, user))
                {
                    TrySatisfyDeficitViaOpenSlotBridge(solution, constraints, user, nightShift);
                }
            }
        }

        // وقتی مجموع سهمیه = ظرفیت ماه است، رزرو شب تعطیل نباید مانع تکمیل حداقل کل شود
        ForceFillRemainingTotalIgnoringHolidayReservation(solution, constraints, nightShift);
    }

    public static void OptimizeSpread(ShiftSolution solution, ShiftConstraints constraints)
    {
        var nightShift = constraints.ShiftRequirements.FirstOrDefault(s => s.ShiftLabel == ShiftYar.Domain.Enums.ShiftModel.ShiftEnums.ShiftLabel.Night);
        if (nightShift == null) return;
        foreach (var user in constraints.UserConstraints.Where(u => u.HasExactNightQuota || u.ExactHolidayWeekendNightShiftCount.HasValue))
        {
            ImproveNightSpread(solution, constraints, user, nightShift);
        }
    }

    /// <summary>
    /// آخرین تلاش برای تکمیل حداقل سهمیه شب — با اولویت سخت بالاتر از سقف روزهای کاری متوالی.
    /// </summary>
    public static void EnforceMandatoryMinimums(ShiftSolution solution, ShiftConstraints constraints)
    {
        var nightShift = constraints.ShiftRequirements.FirstOrDefault(s => s.ShiftLabel == ShiftLabel.Night);
        if (nightShift == null)
        {
            return;
        }

        for (var pass = 0; pass < 10; pass++)
        {
            var deficits = OrderUsersByDeficit(solution, constraints).ToList();
            if (deficits.Count == 0)
            {
                break;
            }

            foreach (var user in deficits)
            {
                EnforceForUser(solution, constraints, user, nightShift);
                if (HasNightQuotaDeficit(solution, user))
                {
                    TrySatisfyDeficitViaOpenSlotBridge(solution, constraints, user, nightShift);
                }
            }

            ForceFillRemainingTotalIgnoringHolidayReservation(solution, constraints, nightShift);
        }
    }

    private static bool HasNightQuotaDeficit(ShiftSolution solution, UserConstraint user) =>
        user.ExactNightShiftCount.HasValue
        && CountNights(solution, user.UserId) < user.ExactNightShiftCount.Value;

    /// <summary>
    /// آخرین مرحله: تکمیل اجباری حداقل سهمیه با جابجایی زنجیره‌ای (حتی وقتی ظرفیت ماه پر است).
    /// </summary>
    public static void ForceSatisfyAllDeficits(ShiftSolution solution, ShiftConstraints constraints)
    {
        var nightShift = constraints.ShiftRequirements.FirstOrDefault(s => s.ShiftLabel == ShiftLabel.Night);
        if (nightShift == null)
        {
            return;
        }

        FillAllVacantNightSlots(solution, constraints, nightShift);

        var stalePasses = 0;
        for (var pass = 0; pass < 10 && stalePasses < 2; pass++)
        {
            var deficits = OrderUsersByDeficit(solution, constraints).ToList();
            if (deficits.Count == 0) break;
            if (deficits.Count == 0)
            {
                return;
            }

            var allowCriticalDonor = pass % 2 == 1;
            var progress = false;
            foreach (var user in deficits)
            {
                var needed = user.ExactNightShiftCount!.Value - CountNights(solution, user.UserId);
                for (var i = 0; i < needed; i++)
                {
                    if (TryMandatoryAddOneNight(solution, constraints, user, nightShift, allowCriticalDonor))
                    {
                        progress = true;
                    }
                    else if (TrySatisfyDeficitViaOpenSlotBridge(solution, constraints, user, nightShift))
                    {
                        progress = true;
                    }
                    else if (TryClaimViaSurplusRotation(solution, constraints, user, nightShift))
                    {
                        progress = true;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            if (!progress)
            {
                stalePasses++;
            }
            else
            {
                stalePasses = 0;
            }
        }
    }

    /// <summary>
    /// جابجایی سراسری بین کسری/مازاد وقتی ظرفیت ماه پر است — شامل اهداکنندهٔ مسئول بحرانی.
    /// </summary>
    public static void GlobalRebalanceNightQuotas(ShiftSolution solution, ShiftConstraints constraints)
    {
        var nightShift = constraints.ShiftRequirements.FirstOrDefault(s => s.ShiftLabel == ShiftLabel.Night);
        if (nightShift == null)
        {
            return;
        }

        FillAllVacantNightSlots(solution, constraints, nightShift);

        for (var pass = 0; pass < 4; pass++)
        {
            var deficits = OrderUsersByDeficit(solution, constraints).ToList();
            if (deficits.Count == 0)
            {
                return;
            }

            var progress = false;
            foreach (var receiver in deficits)
            {
                if (TryMandatoryAddOneNight(solution, constraints, receiver, nightShift, allowCriticalDonor: true))
                {
                    progress = true;
                    continue;
                }

                if (TrySatisfyDeficitViaOpenSlotBridge(solution, constraints, receiver, nightShift))
                {
                    progress = true;
                    continue;
                }

                if (TryClaimViaSurplusRotation(solution, constraints, receiver, nightShift))
                {
                    progress = true;
                }
            }

            if (!progress)
            {
                return;
            }
        }
    }

    private static bool TryMandatoryAddOneNight(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        ShiftRequirement nightShift,
        bool allowCriticalDonor = false)
    {
        var existingDates = GetNights(solution, user.UserId).Select(a => a.Date.Date).ToList();
        var minGap = ResolveNightSpacingGap(constraints, user);
        var openCapacityDates = AllCandidateDates(constraints, holidayOnly: false)
            .Where(d => HasSpecialtyCapacity(solution, constraints, nightShift, d, user.SpecialtyId))
            .OrderBy(d => existingDates.Any() ? existingDates.Min(e => Math.Abs((e - d).Days)) : 0)
            .ToList();

        var candidates = openCapacityDates
            .Concat(PickSpreadDates(
                AllCandidateDates(constraints, holidayOnly: false).ToList(),
                existingDates,
                needed: Math.Max(8, (user.ExactNightShiftCount ?? 8) * 2),
                minGap))
            .Concat(AllCandidateDates(constraints, holidayOnly: false))
            .Distinct()
            .ToList();

        if (ShiftManagerRules.IsLevel1(user))
        {
            candidates = candidates
                .OrderBy(d => solution.GetShiftAssignments(nightShift.ShiftId, d)
                    .Count(a => !a.IsOnCall && ShiftManagerRules.IsLevel1(constraints.UserConstraints.FirstOrDefault(u => u.UserId == a.UserId))))
                .ThenBy(d => existingDates.Any() ? existingDates.Min(e => Math.Abs((e - d).Days)) : 0)
                .ToList();
        }
        else if (ShiftManagerRules.IsManager(user))
        {
            candidates = candidates
                .OrderBy(d =>
                {
                    var assignees = solution.GetShiftAssignments(nightShift.ShiftId, d).Where(a => !a.IsOnCall).ToList();
                    var mgrCount = assignees.Count(a => ShiftManagerRules.IsManager(constraints.UserConstraints.FirstOrDefault(u => u.UserId == a.UserId)));
                    return mgrCount == 1 ? 0 : mgrCount == 0 ? 1 : 2;
                })
                .ThenBy(d => existingDates.Any() ? existingDates.Min(e => Math.Abs((e - d).Days)) : 0)
                .ToList();
        }
        else
        {
            candidates = candidates
                .OrderBy(d =>
                {
                    var assignees = solution.GetShiftAssignments(nightShift.ShiftId, d).Where(a => !a.IsOnCall).ToList();
                    var mgrCount = assignees.Count(a => ShiftManagerRules.IsManager(constraints.UserConstraints.FirstOrDefault(u => u.UserId == a.UserId)));
                    return mgrCount >= 2 ? 0 : 1;
                })
                .ThenBy(d => existingDates.Any() ? existingDates.Min(e => Math.Abs((e - d).Days)) : 0)
                .ToList();
        }

        foreach (var date in candidates)
        {
            if (TryClaimNightForDeficitUser(
                    solution, constraints, user, nightShift, date, depth: 0, allowCriticalDonor: allowCriticalDonor))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// صندلی‌های خالی شیفت شب (تاریخ‌هایی با کمتر از ظرفیت مصوب) را پر می‌کند.
    /// اولویت با کاربران دارای کسری سهمیه است؛ در نبود آن‌ها، هر کاربری که قیود سخت را نقض نکند
    /// اضافه می‌شود تا مازاد موقت ایجاد کرده و مجموع شب‌های ماه کامل شود.
    /// </summary>
    public static bool FillAllVacantNightSlots(
        ShiftSolution solution,
        ShiftConstraints constraints,
        ShiftRequirement nightShift)
    {
        var totalDeficit = constraints.UserConstraints
            .Where(u => u.HasExactNightQuota)
            .Sum(u => Math.Max(0, u.ExactNightShiftCount!.Value - CountNights(solution, u.UserId)));

        var totalSurplus = constraints.UserConstraints
            .Where(u => u.HasExactNightQuota)
            .Sum(u => Math.Max(0, CountNights(solution, u.UserId) - u.ExactNightShiftCount!.Value));

        var netDeficit = totalDeficit - totalSurplus;
        if (netDeficit <= 0)
        {
            return false;
        }

        var anyAdded = false;
        var dates = AllCandidateDates(constraints, holidayOnly: false).ToList();

        foreach (var vacantDate in dates)
        {
            if (netDeficit <= 0) break;

            var specialtyReq = nightShift.SpecialtyRequirements.FirstOrDefault();
            var day = specialtyReq?.ForDay(constraints.IsHoliday(vacantDate));
            var reqCount = day?.RequiredTotalCount ?? 4;

            var currentAssignees = solution.GetShiftAssignments(nightShift.ShiftId, vacantDate)
                .Count(a => !a.IsOnCall);

            var openSlots = reqCount - currentAssignees;
            if (openSlots <= 0)
            {
                continue;
            }

            for (var s = 0; s < openSlots && netDeficit > 0; s++)
            {
                var candidateUsers = constraints.UserConstraints
                    .Where(u => u.IsActive && ShiftEligibilityResolver.MayEverTakeLabel(u, ShiftLabel.Night))
                    .OrderByDescending(u => HasNightQuotaDeficit(solution, u) ? 100 : 0)
                    .ThenBy(u => CountNights(solution, u.UserId))
                    .ToList();

                var slotFilled = false;
                foreach (var user in candidateUsers)
                {
                    if (solution.HasAssignment(user.UserId, nightShift.ShiftId, vacantDate))
                    {
                        continue;
                    }

                    if (user.UnavailableDates.Any(d => d.Date == vacantDate.Date)
                        || user.UnavailableShiftSlots.Any(slot => slot.Date.Date == vacantDate.Date && slot.ShiftLabel == ShiftLabel.Night))
                    {
                        continue;
                    }

                    var minGap = ResolveNightSpacingGap(constraints, user);
                    if (ViolatesNightSpacing(solution, constraints, user, vacantDate, minGap))
                    {
                        continue;
                    }

                    var backup = solution.Clone();
                    ClearConflictingForNight(solution, constraints, user, vacantDate);
                    AddNightSafely(solution, constraints, user, nightShift, vacantDate);

                    var restOk = !AdjacentShiftRestRules.HasForbiddenAdjacentPair(solution.GetUserAllAssignments(user.UserId), constraints.HardRules);
                    var runOk = !constraints.HardRules.EnforceMaxConsecutiveShifts
                        || MaxConsecutiveWorkdayRules.GetMaxConsecutiveWorkRun(solution, user.UserId) <= user.MaxConsecutiveShifts;
                    var mixOk = IsSlotManagerMixSatisfied(solution, constraints, nightShift, vacantDate);

                    if (restOk && runOk && mixOk)
                    {
                        slotFilled = true;
                        anyAdded = true;
                        netDeficit--;
                        break;
                    }

                    RestoreSolutionFrom(solution, backup);
                }

                if (!slotFilled)
                {
                    break;
                }
            }
        }

        return anyAdded;
    }

    /// <summary>
    /// شب مازاد یک کاربر دیگر را — با جابجایی زنجیره‌ای در صورت نیاز — به گیرنده منتقل می‌کند.
    /// </summary>
    private static bool TryClaimViaSurplusRotation(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint receiver,
        ShiftRequirement nightShift)
    {
        var receiverBefore = CountNights(solution, receiver.UserId);
        var receiverMinGap = ResolveNightSpacingGap(constraints, receiver);

        foreach (var (donor, night) in EnumerateSurplusNights(solution, constraints, excludeUserId: receiver.UserId))
        {
            var date = night.Date.Date;
            if (ViolatesNightSpacing(solution, constraints, receiver, date, receiverMinGap))
            {
                continue;
            }

            if (IsPersonallyFeasibleNightDate(solution, constraints, receiver, nightShift, date, holidayOnly: false)
                && CanAcceptNightAfterClearing(solution, constraints, receiver, nightShift, date))
            {
                var backup = solution.Clone();
                solution.UnlockSkeletonAssignment(donor.UserId, night.ShiftId, night.Date);
                solution.RemoveAssignment(donor.UserId, night.ShiftId, night.Date, force: true);
                AddNightSafely(solution, constraints, receiver, nightShift, date);

                if (CountNights(solution, receiver.UserId) > receiverBefore)
                {
                    var donorMin = donor.ExactNightShiftCount;
                    var donorOk = !donorMin.HasValue || CountNights(solution, donor.UserId) >= donorMin.Value;
                    if (!donorOk)
                    {
                        donorOk = TryRestoreDonorMinimum(
                            solution, constraints, donor, nightShift, depth: 0, visitedUsers: new HashSet<int> { receiver.UserId }, allowCriticalDonor: true);
                    }

                    var restRec = !AdjacentShiftRestRules.HasForbiddenAdjacentPair(solution.GetUserAllAssignments(receiver.UserId), constraints.HardRules);
                    var runRec = !constraints.HardRules.EnforceMaxConsecutiveShifts
                        || MaxConsecutiveWorkdayRules.GetMaxConsecutiveWorkRun(solution, receiver.UserId) <= receiver.MaxConsecutiveShifts;
                    var spRec = !ViolatesNightSpacing(solution, constraints, receiver, date, receiverMinGap);

                    if (donorOk && restRec && runRec && spRec && IsSlotManagerMixSatisfied(solution, constraints, nightShift, date))
                    {
                        return true;
                    }
                }

                RestoreSolutionFrom(solution, backup);
            }

            // اگر گیرنده مستقیماً نتوانست شب مازاد را بگیرد،
            // از چرخش ۲ مرحله‌ای استفاده کن: واسط intermediate شب date را از donor می‌گیرد، و شب دیگر خودش (date2) را به receiver می‌دهد.
            foreach (var intermediate in constraints.UserConstraints.Where(u => u.IsActive && u.UserId != donor.UserId && u.UserId != receiver.UserId))
            {
                var intMinGap = ResolveNightSpacingGap(constraints, intermediate);
                if (ViolatesNightSpacing(solution, constraints, intermediate, date, intMinGap))
                {
                    continue;
                }

                if (!IsPersonallyFeasibleNightDate(solution, constraints, intermediate, nightShift, date, holidayOnly: false)
                    || !CanAcceptNightAfterClearing(solution, constraints, intermediate, nightShift, date))
                {
                    continue;
                }

                var intermediateNights = GetNights(solution, intermediate.UserId).Select(a => a.Date.Date).ToList();
                foreach (var date2 in intermediateNights)
                {
                    if (date2 == date) continue;

                    var hasIntAsg = solution.TryGetAssignment(intermediate.UserId, nightShift.ShiftId, date2, out var intAsg);
                    if (hasIntAsg && IsProtected(constraints, intermediate.UserId, intAsg))
                    {
                        continue;
                    }

                    if (ViolatesNightSpacing(solution, constraints, receiver, date2, receiverMinGap))
                    {
                        continue;
                    }

                    if (!IsPersonallyFeasibleNightDate(solution, constraints, receiver, nightShift, date2, holidayOnly: false)
                        || !CanAcceptNightAfterClearing(solution, constraints, receiver, nightShift, date2))
                    {
                        continue;
                    }

                    var backup2 = solution.Clone();
                    solution.UnlockSkeletonAssignment(donor.UserId, night.ShiftId, night.Date);
                    solution.RemoveAssignment(donor.UserId, night.ShiftId, night.Date, force: true);
                    AddNightSafely(solution, constraints, intermediate, nightShift, date);

                    solution.UnlockSkeletonAssignment(intermediate.UserId, night.ShiftId, date2);
                    solution.RemoveAssignment(intermediate.UserId, night.ShiftId, date2, force: true);
                    AddNightSafely(solution, constraints, receiver, nightShift, date2);

                    var countOk = CountNights(solution, receiver.UserId) > receiverBefore
                        && CountNights(solution, intermediate.UserId) >= (intermediate.ExactNightShiftCount ?? 0)
                        && CountNights(solution, donor.UserId) >= (donor.ExactNightShiftCount ?? 0);

                    var mix1 = IsSlotManagerMixSatisfied(solution, constraints, nightShift, date);
                    var mix2 = IsSlotManagerMixSatisfied(solution, constraints, nightShift, date2);

                    var spInt = !ViolatesNightSpacing(solution, constraints, intermediate, date, intMinGap);
                    var spRec = !ViolatesNightSpacing(solution, constraints, receiver, date2, receiverMinGap);

                    var restInt = !AdjacentShiftRestRules.HasForbiddenAdjacentPair(solution.GetUserAllAssignments(intermediate.UserId), constraints.HardRules);
                    var restRec = !AdjacentShiftRestRules.HasForbiddenAdjacentPair(solution.GetUserAllAssignments(receiver.UserId), constraints.HardRules);

                    var runInt = !constraints.HardRules.EnforceMaxConsecutiveShifts
                        || MaxConsecutiveWorkdayRules.GetMaxConsecutiveWorkRun(solution, intermediate.UserId) <= intermediate.MaxConsecutiveShifts;
                    var runRec = !constraints.HardRules.EnforceMaxConsecutiveShifts
                        || MaxConsecutiveWorkdayRules.GetMaxConsecutiveWorkRun(solution, receiver.UserId) <= receiver.MaxConsecutiveShifts;

                    if (countOk && mix1 && mix2 && spInt && spRec && restInt && restRec && runInt && runRec)
                    {
                        return true;
                    }

                    RestoreSolutionFrom(solution, backup2);
                }
            }
        }

        return false;
    }

    private static IEnumerable<(UserConstraint Donor, SaShiftAssignment Night)> EnumerateSurplusNights(
        ShiftSolution solution,
        ShiftConstraints constraints,
        int excludeUserId)
    {
        foreach (var donor in constraints.UserConstraints.Where(u => u.UserId != excludeUserId))
        {
            var nights = GetNights(solution, donor.UserId);
            var min = donor.ExactNightShiftCount ?? 0;
            var surplus = donor.HasExactNightQuota ? nights.Count - min : nights.Count;
            if (surplus <= 0)
            {
                continue;
            }

            var holidayNights = donor.ExactHolidayWeekendNightShiftCount.HasValue
                ? nights.Count(a => constraints.IsHolidayWeekendNight(a.Date))
                : 0;

            foreach (var night in nights
                         .Where(a => !IsProtected(constraints, donor.UserId, a))
                         .OrderByDescending(a => constraints.IsHolidayWeekendNight(a.Date) ? 0 : 1)
                         .ThenByDescending(a => a.Date))
            {
                if (donor.ExactHolidayWeekendNightShiftCount.HasValue
                    && constraints.IsHolidayWeekendNight(night.Date)
                    && holidayNights <= donor.ExactHolidayWeekendNightShiftCount.Value)
                {
                    continue;
                }

                yield return (donor, night);
            }
        }
    }

    private static bool TryClaimNightForDeficitUser(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        ShiftRequirement nightShift,
        DateTime date,
        int depth,
        HashSet<int> visitedUsers = null,
        bool allowCriticalDonor = false)
    {
        if (depth > 2 || !HasNightQuotaDeficit(solution, user))
        {
            return false;
        }

        visitedUsers ??= new HashSet<int>();
        if (visitedUsers.Contains(user.UserId))
        {
            return false;
        }
        visitedUsers.Add(user.UserId);

        var backup = solution.Clone();
        var receiverBefore = CountNights(backup, user.UserId);
        try
        {
            if (!IsPersonallyFeasibleNightDate(solution, constraints, user, nightShift, date, holidayOnly: false))
            {
                return false;
            }

            ClearConflictingForNight(solution, constraints, user, date);

            if (!CanAcceptNightAfterClearing(solution, constraints, user, nightShift, date))
            {
                RestoreSolutionFrom(solution, backup);
                return false;
            }

            var minGap = ResolveNightSpacingGap(constraints, user);
            if (ViolatesNightSpacing(solution, constraints, user, date, minGap))
            {
                var blocking = GetNights(solution, user.UserId)
                    .Where(a => Math.Abs((a.Date.Date - date.Date).Days) <= minGap)
                    .OrderBy(a => Math.Abs((a.Date.Date - date.Date).Days))
                    .FirstOrDefault();
                if (blocking == null)
                {
                    RestoreSolutionFrom(solution, backup);
                    return false;
                }

                var moved = TryMoveNightToAnyFeasibleDate(
                    solution, constraints, user, nightShift, blocking.Date.Date, excludeDate: date, minGap);
                if (!moved || ViolatesNightSpacing(solution, constraints, user, date, minGap))
                {
                    RestoreSolutionFrom(solution, backup);
                    return false;
                }
            }

            if (ShiftManagerRules.IsLevel1(user))
            {
                var existingL1 = solution.GetShiftAssignments(nightShift.ShiftId, date)
                    .Count(a => !a.IsOnCall && ShiftManagerRules.IsLevel1(constraints.UserConstraints.FirstOrDefault(u => u.UserId == a.UserId)));
                if (existingL1 >= 1)
                {
                    var anyDateWithoutL1 = AllCandidateDates(constraints, holidayOnly: false)
                        .Any(d => d != date && solution.GetShiftAssignments(nightShift.ShiftId, d)
                            .All(a => a.IsOnCall || !ShiftManagerRules.IsLevel1(constraints.UserConstraints.FirstOrDefault(u => u.UserId == a.UserId))));
                    if (anyDateWithoutL1)
                    {
                        RestoreSolutionFrom(solution, backup);
                        return false;
                    }
                }
            }

            if (HasSpecialtyCapacity(solution, constraints, nightShift, date, user.SpecialtyId))
            {
                AddNightSafely(solution, constraints, user, nightShift, date);
                if (CountNights(solution, user.UserId) > receiverBefore)
                {
                    return true;
                }
                RestoreSolutionFrom(solution, backup);
                return false;
            }

            var donorAssignment = FindMandatoryDonor(
                solution, constraints, nightShift, date, user.UserId, visitedUsers, allowCriticalDonor);
            if (donorAssignment == null)
            {
                RestoreSolutionFrom(solution, backup);
                return false;
            }

            var donor = constraints.UserConstraints.FirstOrDefault(u => u.UserId == donorAssignment.UserId);
            if (donor == null)
            {
                RestoreSolutionFrom(solution, backup);
                return false;
            }

            var donorMin = donor.ExactNightShiftCount;

            solution.UnlockSkeletonAssignment(donorAssignment.UserId, donorAssignment.ShiftId, donorAssignment.Date);
            solution.RemoveAssignment(donorAssignment.UserId, donorAssignment.ShiftId, donorAssignment.Date, force: true);
            AddNightSafely(solution, constraints, user, nightShift, date);

            var donorOk = !donorMin.HasValue || CountNights(solution, donor.UserId) >= donorMin.Value;
            if (!donorOk && depth < 2)
            {
                donorOk = TryRestoreDonorMinimum(
                    solution, constraints, donor, nightShift, depth + 1, visitedUsers, allowCriticalDonor);
            }

            if (donorOk && CountNights(solution, user.UserId) > receiverBefore
                && IsSlotManagerMixSatisfied(solution, constraints, nightShift, date))
            {
                return true;
            }

            RestoreSolutionFrom(solution, backup);
            return false;
        }
        finally
        {
            visitedUsers.Remove(user.UserId);
        }
    }

    private static bool TryRestoreDonorMinimum(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint donor,
        ShiftRequirement nightShift,
        int depth,
        HashSet<int> visitedUsers,
        bool allowCriticalDonor)
    {
        if (!donor.ExactNightShiftCount.HasValue)
        {
            return true;
        }

        if (CountNights(solution, donor.UserId) >= donor.ExactNightShiftCount.Value)
        {
            return true;
        }

        if (depth >= 2)
        {
            return false;
        }

        var minGap = ResolveNightSpacingGap(constraints, donor);
        var existingDates = GetNights(solution, donor.UserId).Select(a => a.Date.Date).ToList();

        // ۱) اولویت نخست: هر تاریخی که ظرفیت خالی دارد
        var openCapacityDates = AllCandidateDates(constraints, holidayOnly: false)
            .Where(d => HasSpecialtyCapacity(solution, constraints, nightShift, d, donor.SpecialtyId))
            .OrderBy(d => existingDates.Any() ? existingDates.Min(e => Math.Abs((e - d).Days)) : 0)
            .ToList();

        foreach (var donorDate in openCapacityDates)
        {
            if (TryClaimNightForDeficitUser(
                    solution, constraints, donor, nightShift, donorDate, depth, visitedUsers, allowCriticalDonor)
                && CountNights(solution, donor.UserId) >= donor.ExactNightShiftCount.Value)
            {
                return true;
            }
        }

        // ۲) اولویت دوم: تاریخ‌های دارای پخش مناسب و بقیه کاندیداها
        var candidateDates = PickSpreadDates(
                AllCandidateDates(constraints, holidayOnly: false).ToList(),
                existingDates,
                needed: Math.Max(8, (donor.ExactNightShiftCount ?? 8) * 2),
                minGap)
            .Concat(AllCandidateDates(constraints, holidayOnly: false))
            .Distinct()
            .Except(openCapacityDates)
            .ToList();

        foreach (var donorDate in candidateDates)
        {
            if (TryClaimNightForDeficitUser(
                    solution, constraints, donor, nightShift, donorDate, depth, visitedUsers, allowCriticalDonor)
                && CountNights(solution, donor.UserId) >= donor.ExactNightShiftCount.Value)
            {
                return true;
            }
        }

        return false;
    }

    public static bool TrySatisfyDeficitViaOpenSlotBridge(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint receiver,
        ShiftRequirement nightShift)
    {
        var receiverBefore = CountNights(solution, receiver.UserId);
        var openDates = AllCandidateDates(constraints, holidayOnly: false)
            .Where(d => HasSpecialtyCapacity(solution, constraints, nightShift, d, receiver.SpecialtyId))
            .ToList();

        if (openDates.Count == 0)
        {
            return false;
        }

        var receiverMinGap = ResolveNightSpacingGap(constraints, receiver);

        foreach (var vacantDate in openDates)
        {
            // ۱) اگر گیرنده مستقیماً می‌تواند صندلی خالی را بگیرد
            if (IsPersonallyFeasibleNightDate(solution, constraints, receiver, nightShift, vacantDate, holidayOnly: false)
                && CanAcceptNightAfterClearing(solution, constraints, receiver, nightShift, vacantDate)
                && !ViolatesNightSpacing(solution, constraints, receiver, vacantDate, receiverMinGap))
            {
                var backup = solution.Clone();
                AddNightSafely(solution, constraints, receiver, nightShift, vacantDate);
                if (CountNights(solution, receiver.UserId) > receiverBefore
                    && IsSlotManagerMixSatisfied(solution, constraints, nightShift, vacantDate))
                {
                    return true;
                }
                RestoreSolutionFrom(solution, backup);
            }

            // ۲) گیرنده نمی‌تواند مستقیماً صندلی خالی را بگیرد؛ جستجوی کاربر واسط bridge
            foreach (var bridge in constraints.UserConstraints.Where(u => u.IsActive && u.UserId != receiver.UserId))
            {
                if (!ShiftEligibilityResolver.MayEverTakeLabel(bridge, ShiftLabel.Night))
                {
                    continue;
                }

                if (bridge.UnavailableDates.Any(d => d.Date == vacantDate.Date)
                    || bridge.UnavailableShiftSlots.Any(s => s.Date.Date == vacantDate.Date && s.ShiftLabel == ShiftLabel.Night))
                {
                    continue;
                }

                var bridgeMinGap = ResolveNightSpacingGap(constraints, bridge);
                var bridgeNights = GetNights(solution, bridge.UserId).Select(a => a.Date.Date).ToList();

                foreach (var bridgeDate in bridgeNights)
                {
                    if (bridgeDate == vacantDate) continue;

                    // اگر شب مبدأ bridge درخواست تأییدشده است یا برای مسئول بحرانی است، جابجا نشود
                    var hasBridgeAsg = solution.TryGetAssignment(bridge.UserId, nightShift.ShiftId, bridgeDate, out var bridgeAssignment);
                    var isProt = hasBridgeAsg && IsProtected(constraints, bridge.UserId, bridgeAssignment);
                    var isCrit = hasBridgeAsg && ShiftManagerRules.IsCriticalForManagerMix(constraints, solution, bridgeAssignment) && !ShiftManagerRules.IsManager(receiver);

                    if (isProt || isCrit)
                    {
                        continue;
                    }

                    if (receiver.UnavailableDates.Any(d => d.Date == bridgeDate.Date)
                        || receiver.UnavailableShiftSlots.Any(s => s.Date.Date == bridgeDate.Date && s.ShiftLabel == ShiftLabel.Night))
                    {
                        continue;
                    }

                    var backup = solution.Clone();

                    // واسط صندلی خالی را می‌گیرد و شب قبلی‌اش را آزاد می‌کند
                    solution.UnlockSkeletonAssignment(bridge.UserId, nightShift.ShiftId, bridgeDate);
                    solution.RemoveAssignment(bridge.UserId, nightShift.ShiftId, bridgeDate, force: true);
                    ClearConflictingForNight(solution, constraints, bridge, vacantDate);
                    AddNightSafely(solution, constraints, bridge, nightShift, vacantDate);

                    // گیرنده شب آزادشدهٔ واسط را می‌گیرد
                    ClearConflictingForNight(solution, constraints, receiver, bridgeDate);
                    AddNightSafely(solution, constraints, receiver, nightShift, bridgeDate);

                    var countOk = CountNights(solution, receiver.UserId) > receiverBefore
                        && CountNights(solution, bridge.UserId) >= (bridge.ExactNightShiftCount ?? 0);
                    var mixVacant = IsSlotManagerMixSatisfied(solution, constraints, nightShift, vacantDate);
                    var mixBridge = IsSlotManagerMixSatisfied(solution, constraints, nightShift, bridgeDate);

                    var spacingBridge = !ViolatesNightSpacing(solution, constraints, bridge, vacantDate, bridgeMinGap);
                    var spacingReceiver = !ViolatesNightSpacing(solution, constraints, receiver, bridgeDate, receiverMinGap);

                    var restBridge = !AdjacentShiftRestRules.HasForbiddenAdjacentPair(solution.GetUserAllAssignments(bridge.UserId), constraints.HardRules);
                    var restReceiver = !AdjacentShiftRestRules.HasForbiddenAdjacentPair(solution.GetUserAllAssignments(receiver.UserId), constraints.HardRules);

                    var runBridge = !constraints.HardRules.EnforceMaxConsecutiveShifts
                        || MaxConsecutiveWorkdayRules.GetMaxConsecutiveWorkRun(solution, bridge.UserId) <= bridge.MaxConsecutiveShifts;
                    var runReceiver = !constraints.HardRules.EnforceMaxConsecutiveShifts
                        || MaxConsecutiveWorkdayRules.GetMaxConsecutiveWorkRun(solution, receiver.UserId) <= receiver.MaxConsecutiveShifts;

                    if (countOk && mixVacant && mixBridge && spacingBridge && spacingReceiver && restBridge && restReceiver && runBridge && runReceiver)
                    {
                        return true;
                    }

                    RestoreSolutionFrom(solution, backup);
                }
            }
        }

        return false;
    }

    private static SaShiftAssignment? FindMandatoryDonor(
        ShiftSolution solution,
        ShiftConstraints constraints,
        ShiftRequirement nightShift,
        DateTime date,
        int receiverUserId,
        HashSet<int> visitedUsers = null,
        bool allowCriticalDonor = false)
    {
        return solution.GetShiftAssignments(nightShift.ShiftId, date)
            .Where(a => !a.IsOnCall && a.UserId != receiverUserId && (visitedUsers == null || !visitedUsers.Contains(a.UserId)))
            .Select(a =>
            {
                var donor = constraints.UserConstraints.FirstOrDefault(u => u.UserId == a.UserId);
                return (Assignment: a, Donor: donor);
            })
            .Where(x => x.Donor != null)
            .Where(x => !IsProtected(constraints, x.Donor!.UserId, x.Assignment))
            .Where(x =>
            {
                if (ShiftManagerRules.IsCriticalForManagerMix(constraints, solution, x.Assignment))
                {
                    var receiverUser = constraints.UserConstraints.FirstOrDefault(u => u.UserId == receiverUserId);
                    if (receiverUser != null)
                    {
                        var (reqTotal, reqL1) = ShiftManagerRules.GetRequirement(nightShift);
                        var assigneesWithoutDonor = solution.GetShiftAssignments(nightShift.ShiftId, date)
                            .Where(a => !a.IsOnCall && a.UserId != x.Donor!.UserId)
                            .Select(a => constraints.UserConstraints.FirstOrDefault(u => u.UserId == a.UserId))
                            .Where(u => u != null)
                            .Cast<UserConstraint>()
                            .ToList();
                        assigneesWithoutDonor.Add(receiverUser);
                        if (ShiftManagerRules.IsSatisfied(assigneesWithoutDonor, reqTotal, reqL1))
                        {
                            return true;
                        }
                    }
                    if (ShiftManagerRules.IsLevel1(x.Donor!) && (receiverUser == null || !ShiftManagerRules.IsLevel1(receiverUser)))
                    {
                        return false;
                    }
                    return allowCriticalDonor;
                }
                return true;
            })
            .OrderBy(x => CanDonateNight(solution, constraints, x.Donor!, x.Assignment) ? 0 : 1)
            .ThenBy(x => x.Donor!.HasExactNightQuota ? 1 : 0)
            .ThenBy(x => DonorPriority(solution, constraints, x.Donor!, x.Assignment, holidayClaim: false))
            .Select(x => x.Assignment)
            .FirstOrDefault();
    }

    private static void RestoreSolutionFrom(ShiftSolution target, ShiftSolution backup)
    {
        target.Assignments.Clear();
        foreach (var assignment in backup.Assignments.Values)
        {
            target.AddAssignment(
                assignment.UserId,
                assignment.ShiftId,
                assignment.Date,
                assignment.ShiftLabel,
                assignment.IsOnCall,
                isSkeleton: assignment.IsSkeleton);
        }

        target.Score = backup.Score;
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
                .Where(a => !IsProtected(constraints, user.UserId, a)
                            && !ShiftManagerRules.IsCriticalForManagerMix(constraints, solution, a))
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

            solution.UnlockSkeletonAssignment(removable.UserId, removable.ShiftId, removable.Date);
            var removed = solution.RemoveAssignment(removable.UserId, removable.ShiftId, removable.Date, force: true);
            if (!removed)
            {
                break;
            }
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
        var allDates = picks.Concat(candidates.Except(picks)).ToList();
        var added = 0;
        foreach (var date in allDates)
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
            solution.UnlockSkeletonAssignment(donorAssignment.UserId, donorAssignment.ShiftId, donorAssignment.Date);
            solution.RemoveAssignment(donorAssignment.UserId, donorAssignment.ShiftId, donorAssignment.Date, force: true);
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
                if (bridge == null
                    || IsProtected(constraints, bridge.UserId, occupant)
                    || ShiftManagerRules.IsCriticalForManagerMix(constraints, solution, occupant))
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
                .Where(a => !IsSticky(constraints, solution, user.UserId, a))
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
        // اگر شب مبدأ اسکلت/sticky است نباید جابجا شود
        if (solution.TryGetAssignment(user.UserId, nightShift.ShiftId, fromDate, out var fromAssign)
            && IsSticky(constraints, solution, user.UserId, fromAssign))
        {
            return false;
        }

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
                var wasSkeleton = solution.TryGetAssignment(user.UserId, nightShift.ShiftId, fromDate, out var fromAss) && fromAss.IsSkeleton;
                ClearConflictingForNight(solution, constraints, user, target);
                solution.RemoveAssignment(user.UserId, nightShift.ShiftId, fromDate);
                solution.AddAssignment(user.UserId, nightShift.ShiftId, target, ShiftLabel.Night, false, isSkeleton: wasSkeleton);
                return true;
            }

            var donorAssignment = FindBestDonorAssignment(solution, constraints, nightShift, target, user.UserId, holidayClaim: false);
            if (donorAssignment == null)
            {
                continue;
            }

            var wasSkel = solution.TryGetAssignment(user.UserId, nightShift.ShiftId, fromDate, out var fAss) && fAss.IsSkeleton;
            ClearConflictingForNight(solution, constraints, user, target);
            solution.RemoveAssignment(user.UserId, nightShift.ShiftId, fromDate);
            solution.RemoveAssignment(donorAssignment.UserId, donorAssignment.ShiftId, donorAssignment.Date);
            solution.AddAssignment(user.UserId, nightShift.ShiftId, target, ShiftLabel.Night, false, isSkeleton: wasSkel);
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
            .Where(x => !ShiftManagerRules.IsCriticalForManagerMix(constraints, solution, x.Assignment))
            .OrderBy(x => ShiftManagerRules.IsLevel1(x.Donor!) ? 1 : 0)
            .ThenBy(x => DonorPriority(solution, constraints, x.Donor!, x.Assignment, holidayClaim))
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

        if (ShiftManagerRules.IsCriticalForManagerMix(constraints, solution, assignment))
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
        var minRequired = user.ExactNightShiftCount ?? (user.MinimumShiftsRequired.TryGetValue(ShiftLabel.Night, out var minN) ? minN : (int?)null);
        if (minRequired.HasValue && remainingTotal < minRequired.Value)
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
                .Where(a => !IsProtected(constraints, user.UserId, a) && !solution.IsLockedSkeleton(a.UserId, a.ShiftId, a.Date))
                .OrderByDescending(a => a.Date)
                .FirstOrDefault();
            if (removableMorning != null)
            {
                var removed = solution.RemoveAssignment(removableMorning.UserId, removableMorning.ShiftId, removableMorning.Date);
                if (removed)
                {
                    continue;
                }
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
            if (IsSticky(constraints, solution, user.UserId, assignment))
            {
                continue;
            }

            if (constraints.HardRules.IsForbiddenOnDayAfterNight(assignment.ShiftLabel))
            {
                solution.RemoveAssignment(assignment.UserId, assignment.ShiftId, assignment.Date);
            }
        }
    }

    private static void AddNightSafely(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        ShiftRequirement nightShift,
        DateTime date)
    {
        ClearConflictingForNight(solution, constraints, user, date);
        if (solution.HasAssignment(user.UserId, nightShift.ShiftId, date))
        {
            return;
        }

        solution.AddAssignment(user.UserId, nightShift.ShiftId, date, ShiftLabel.Night, isOnCall: false);
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
            .Where(a => !IsSticky(constraints, solution, user.UserId, a))
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
            .Where(a => !IsSticky(constraints, solution, user.UserId, a))
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

                if (IsSticky(constraints, solution, user.UserId, a))
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
            .Any(a => IsSticky(constraints, solution, user.UserId, a));
        if (protectedEvening)
        {
            return false;
        }

        var nextDay = date.Date.AddDays(1);
        var protectedBlockingNextDay = solution.GetUserAssignments(user.UserId, nextDay)
            .Where(a => !a.IsOnCall)
            .Where(a => IsSticky(constraints, solution, user.UserId, a))
            .Any(a => constraints.HardRules.IsForbiddenOnDayAfterNight(a.ShiftLabel));
        if (protectedBlockingNextDay)
        {
            return false;
        }

        var prevDay = date.Date.AddDays(-1);
        var hasPrevNight = solution.GetUserAssignments(user.UserId, prevDay)
            .Any(a => !a.IsOnCall && a.ShiftLabel == ShiftLabel.Night);
        if (hasPrevNight && !constraints.HardRules.AllowNightShiftAfterNightShift)
        {
            return false;
        }

        var hasNextNight = solution.GetUserAssignments(user.UserId, nextDay)
            .Any(a => !a.IsOnCall && a.ShiftLabel == ShiftLabel.Night);
        if (hasNextNight && !constraints.HardRules.AllowNightShiftAfterNightShift)
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
               && (HasNightQuotaDeficit(solution, user)
                   || !MaxConsecutiveWorkdayRules.WouldExceedMaxConsecutiveWorkdays(
                       solution, constraints, user, date));
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
                .Where(a => !IsSticky(constraints, solution, user.UserId, a))
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

                    var fromDayReq = nightShift.SpecialtyRequirements.Sum(s => s.ForDay(constraints.IsHoliday(night.Date)).RequiredTotalCount);
                    var fromCurrentAssignees = solution.GetShiftAssignments(nightShift.ShiftId, night.Date).Count(a => !a.IsOnCall);
                    if (fromCurrentAssignees > fromDayReq
                        && IsFeasibleNightDate(solution, constraints, user, nightShift, target, holidayOnly: false, ignoreUserNightOnDate: false)
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
                var backup = solution.Clone();
                var userWasSkeleton = solution.TryGetAssignment(user.UserId, nightShift.ShiftId, fromDate, out var userFromAss) && userFromAss.IsSkeleton;
                var swapWasSkeleton = solution.TryGetAssignment(swapUserId.Value, nightShift.ShiftId, toDate, out var swapToAss) && swapToAss.IsSkeleton;
                solution.RemoveAssignment(user.UserId, nightShift.ShiftId, fromDate);
                solution.RemoveAssignment(swapUserId.Value, nightShift.ShiftId, toDate);
                solution.AddAssignment(user.UserId, nightShift.ShiftId, toDate, ShiftLabel.Night, false, isSkeleton: userWasSkeleton);
                solution.AddAssignment(swapUserId.Value, nightShift.ShiftId, fromDate, ShiftLabel.Night, false, isSkeleton: swapWasSkeleton);

                if (!IsSlotManagerMixSatisfied(solution, constraints, nightShift, fromDate) ||
                    !IsSlotManagerMixSatisfied(solution, constraints, nightShift, toDate))
                {
                    RestoreSolutionFrom(solution, backup);
                }
            }
            else
            {
                var backup = solution.Clone();
                var userWasSkeleton = solution.TryGetAssignment(user.UserId, nightShift.ShiftId, fromDate, out var userFromAss2) && userFromAss2.IsSkeleton;
                solution.RemoveAssignment(user.UserId, nightShift.ShiftId, fromDate);
                solution.AddAssignment(user.UserId, nightShift.ShiftId, toDate, ShiftLabel.Night, false, isSkeleton: userWasSkeleton);

                if (!IsSlotManagerMixSatisfied(solution, constraints, nightShift, fromDate) ||
                    !IsSlotManagerMixSatisfied(solution, constraints, nightShift, toDate))
                {
                    RestoreSolutionFrom(solution, backup);
                }
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
        if (AdjacentShiftRestRules.WouldConflict(
                forAdjacency,
                date,
                ShiftLabel.Night,
                constraints,
                ignoreShiftId: null,
                ignoreSettingsControlledAfterNight: false))
        {
            return false;
        }

        if (ShiftManagerRules.IsLevel1(user))
        {
            var existingL1 = solution.GetShiftAssignments(nightShift.ShiftId, date)
                .Count(a => !a.IsOnCall && ShiftManagerRules.IsLevel1(constraints.UserConstraints.FirstOrDefault(u => u.UserId == a.UserId)));
            if (existingL1 >= 1)
            {
                var anyDateWithoutL1 = AllCandidateDates(constraints, holidayOnly: false)
                    .Any(d => d != date && solution.GetShiftAssignments(nightShift.ShiftId, d)
                        .All(a => a.IsOnCall || !ShiftManagerRules.IsLevel1(constraints.UserConstraints.FirstOrDefault(u => u.UserId == a.UserId))));
                if (anyDateWithoutL1)
                {
                    return false;
                }
            }
        }
        else if (ShiftManagerRules.IsManager(user))
        {
            var existingManagers = solution.GetShiftAssignments(nightShift.ShiftId, date)
                .Count(a => !a.IsOnCall && ShiftManagerRules.IsManager(constraints.UserConstraints.FirstOrDefault(u => u.UserId == a.UserId)));
            if (existingManagers >= 2)
            {
                var anyDateWithout2Managers = AllCandidateDates(constraints, holidayOnly: false)
                    .Any(d => d != date && solution.GetShiftAssignments(nightShift.ShiftId, d)
                        .Count(a => !a.IsOnCall && ShiftManagerRules.IsManager(constraints.UserConstraints.FirstOrDefault(u => u.UserId == a.UserId))) < 2);
                if (anyDateWithout2Managers)
                {
                    return false;
                }
            }
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
               && (HasNightQuotaDeficit(solution, user)
                   || !MaxConsecutiveWorkdayRules.WouldExceedMaxConsecutiveWorkdays(
                       solution, constraints, user, date));
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
            if (nightDate == candidateDate.Date)
            {
                continue;
            }

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

    private static bool IsSticky(
        ShiftConstraints constraints,
        ShiftSolution solution,
        int userId,
        SaShiftAssignment assignment) =>
        IsProtected(constraints, userId, assignment)
        || ShiftManagerRules.IsCriticalForManagerMix(constraints, solution, assignment);

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

    private static bool IsSlotManagerMixSatisfied(
        ShiftSolution solution,
        ShiftConstraints constraints,
        ShiftRequirement shiftReq,
        DateTime date)
    {
        var (requiredTotal, minLevel1) = ShiftManagerRules.GetRequirement(shiftReq);
        if (requiredTotal <= 0)
        {
            return true;
        }

        var assignees = solution.GetShiftAssignments(shiftReq.ShiftId, date)
            .Where(a => !a.IsOnCall)
            .Select(a => constraints.UserConstraints.FirstOrDefault(u => u.UserId == a.UserId))
            .Where(u => u != null)
            .Cast<UserConstraint>()
            .ToList();

        return ShiftManagerRules.IsSatisfied(assignees, requiredTotal, minLevel1);
    }
}
