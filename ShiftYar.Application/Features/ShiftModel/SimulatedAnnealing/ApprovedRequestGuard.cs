using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing
{
    /// <summary>
    /// تضمین قطعی رعایت درخواست‌های تأییدشده روی یک راه‌حل، مستقل از حلقه SA.
    /// این لایه آخرین حرف را می‌زند و خروجی نباید بدون رضایت قیود برگردد.
    /// </summary>
    public static class ApprovedRequestGuard
    {
        public static bool HasAnyApprovedRequestConstraints(ShiftConstraints constraints)
        {
            return constraints.UserConstraints.Any(u =>
                u.UnavailableDates.Count > 0 ||
                u.UnavailableShiftSlots.Count > 0 ||
                u.RequiredShiftSlots.Count > 0 ||
                u.RequiredPresenceDates.Count > 0);
        }

        /// <summary>
        /// اعمال قطعی Off/On روی راه‌حل. ترتیب: حذف Off → اجبار On → حذف مجدد Off.
        /// </summary>
        public static void ForceApply(ShiftSolution solution, ShiftConstraints constraints)
        {
            StripUnavailableAssignments(solution, constraints);
            ForceRequiredShiftSlots(solution, constraints);
            ForceRequiredPresenceDates(solution, constraints);
            StripUnavailableAssignments(solution, constraints);
        }

        public static List<string> GetUnmetViolations(ShiftSolution solution, ShiftConstraints constraints)
        {
            var violations = new List<string>();

            foreach (var user in constraints.UserConstraints)
            {
                foreach (var date in user.UnavailableDates)
                {
                    if (date.Date < constraints.StartDate.Date || date.Date > constraints.EndDate.Date)
                    {
                        continue;
                    }

                    if (solution.GetUserAssignments(user.UserId, date).Any())
                    {
                        violations.Add(
                            $"عدم‌حضور کل‌روز نقض شد: کاربر {user.UserId} ({user.UserName}) هنوز در {date:yyyy-MM-dd} شیفت دارد.");
                    }
                }

                foreach (var slot in user.UnavailableShiftSlots)
                {
                    if (slot.Date.Date < constraints.StartDate.Date || slot.Date.Date > constraints.EndDate.Date)
                    {
                        continue;
                    }

                    // ON صریح همان روز/شیفت بر OFF مشتق اولویت دارد
                    if (IsApprovedRequiredSlot(user, slot.Date, slot.ShiftLabel, slot.ShiftId))
                    {
                        continue;
                    }

                    if (solution.GetUserAllAssignments(user.UserId)
                        .Any(a => a.Date.Date == slot.Date.Date && a.ShiftLabel == slot.ShiftLabel))
                    {
                        violations.Add(
                            $"عدم‌حضور شیفت‌مشخص نقض شد: کاربر {user.UserId} ({user.UserName}) هنوز در {slot.ShiftLabel} تاریخ {slot.Date:yyyy-MM-dd} است.");
                    }
                }

                foreach (var required in user.RequiredShiftSlots)
                {
                    if (required.Date.Date < constraints.StartDate.Date || required.Date.Date > constraints.EndDate.Date)
                    {
                        continue;
                    }

                    if (!IsOffConflictFree(user, required.Date, required.ShiftLabel))
                    {
                        violations.Add(
                            $"تداخل درخواست: کاربر {user.UserId} هم حضور در {required.ShiftLabel} و هم عدم‌حضور برای {required.Date:yyyy-MM-dd} دارد.");
                        continue;
                    }

                    var shiftReq = ResolveShift(constraints, required.ShiftLabel, user.SpecialtyId, required.ShiftId);
                    var ok = shiftReq != null &&
                             solution.GetShiftAssignments(shiftReq.ShiftId, required.Date)
                                 .Any(a => a.UserId == user.UserId && !a.IsOnCall);

                    if (!ok)
                    {
                        violations.Add(
                            DescribeUnmetRequiredSlot(solution, constraints, user, required, shiftReq));
                    }
                }

                foreach (var presenceDate in user.RequiredPresenceDates)
                {
                    if (presenceDate.Date < constraints.StartDate.Date || presenceDate.Date > constraints.EndDate.Date)
                    {
                        continue;
                    }

                    if (user.UnavailableDates.Any(d => d.Date == presenceDate.Date))
                    {
                        violations.Add(
                            $"تداخل درخواست: کاربر {user.UserId} هم حضور کل‌روز و هم مرخصی برای {presenceDate:yyyy-MM-dd} دارد.");
                        continue;
                    }

                    if (!solution.GetUserAssignments(user.UserId, presenceDate).Any(a => !a.IsOnCall))
                    {
                        violations.Add(
                            $"حضور اجباری کل‌روز اعمال نشد: کاربر {user.UserId} ({user.UserName}) باید در {presenceDate:yyyy-MM-dd} نیروی حاضر باشد.");
                    }
                }
            }

            return violations;
        }

        /// <summary>
        /// چند درخواست ON تأییدشده برای یک صندلی/روز — از قبل غیرممکن است.
        /// </summary>
        public static List<string> GetConflictingRequiredShiftSlotViolations(ShiftConstraints constraints)
        {
            var violations = new List<string>();
            if (constraints.ShiftRequirements.Count == 0)
            {
                return violations;
            }

            var dates = Enumerable.Range(0, (constraints.EndDate.Date - constraints.StartDate.Date).Days + 1)
                .Select(i => constraints.StartDate.Date.AddDays(i));

            foreach (var date in dates)
            {
                foreach (var shiftReq in constraints.ShiftRequirements)
                {
                    foreach (var specialtyReq in shiftReq.SpecialtyRequirements)
                    {
                        var day = specialtyReq.ForDay(constraints.IsHoliday(date));
                        if (day.RequiredTotalCount <= 0)
                        {
                            continue;
                        }

                        var claimants = constraints.UserConstraints
                            .Where(u => u.IsActive && u.SpecialtyId == specialtyReq.SpecialtyId)
                            .Where(u => u.RequiredShiftSlots.Any(s =>
                                s.Date.Date == date.Date &&
                                s.ShiftLabel == shiftReq.ShiftLabel &&
                                (!s.ShiftId.HasValue || s.ShiftId.Value == shiftReq.ShiftId)))
                            .ToList();

                        if (claimants.Count <= day.RequiredTotalCount)
                        {
                            continue;
                        }

                        var names = string.Join("، ", claimants.Select(u =>
                            string.IsNullOrWhiteSpace(u.UserName) ? $"User {u.UserId}" : u.UserName));
                        violations.Add(
                            $"تداخل درخواست تأییدشده: {claimants.Count} نفر ({names}) برای {shiftReq.ShiftLabel} " +
                            $"در {date:yyyy-MM-dd} درخواست ON دارند، در حالی که ظرفیت این شیفت {day.RequiredTotalCount} نفر است.");
                    }
                }
            }

            return violations;
        }

        public static bool IsApprovedRequiredSlot(
            UserConstraint user,
            DateTime date,
            ShiftLabel label,
            int? shiftId = null) =>
            user.RequiredShiftSlots.Any(s =>
                s.Date.Date == date.Date &&
                s.ShiftLabel == label &&
                (!shiftId.HasValue || !s.ShiftId.HasValue || s.ShiftId.Value == shiftId.Value));

        private static void StripUnavailableAssignments(ShiftSolution solution, ShiftConstraints constraints)
        {
            foreach (var assignment in solution.Assignments.Values.ToList())
            {
                var user = constraints.UserConstraints.FirstOrDefault(u => u.UserId == assignment.UserId);
                if (user == null)
                {
                    continue;
                }

                if (!IsUserAvailable(user, assignment.Date, assignment.ShiftLabel))
                {
                    solution.RemoveAssignment(assignment.UserId, assignment.ShiftId, assignment.Date);
                }
            }
        }

        private static void ForceRequiredShiftSlots(ShiftSolution solution, ShiftConstraints constraints)
        {
            var requiredEntries = constraints.UserConstraints
                .SelectMany(u => u.RequiredShiftSlots.Select(r => (User: u, Required: r)))
                .Where(x => x.Required.Date.Date >= constraints.StartDate.Date &&
                            x.Required.Date.Date <= constraints.EndDate.Date)
                .OrderBy(x => x.Required.Date)
                .ThenBy(x => x.Required.ShiftLabel)
                .ThenBy(x => x.User.UserId)
                .ToList();

            foreach (var (user, required) in requiredEntries)
            {
                if (!IsOffConflictFree(user, required.Date, required.ShiftLabel) ||
                    !Common.Utilities.ShiftEligibilityResolver.MayEverTakeLabel(user, required.ShiftLabel))
                {
                    continue;
                }

                var shiftReq = ResolveShift(constraints, required.ShiftLabel, user.SpecialtyId, required.ShiftId);
                if (shiftReq == null)
                {
                    continue;
                }

                var existing = solution.GetShiftAssignments(shiftReq.ShiftId, required.Date)
                    .FirstOrDefault(a => a.UserId == user.UserId);

                if (existing != null && !existing.IsOnCall)
                {
                    continue;
                }

                // چند پاس پاک‌سازی تا تداخل‌های غیرمحافظت‌شده جلوی حضور اجباری را نگیرند
                for (var pass = 0; pass < 6; pass++)
                {
                    PrepareForRequiredSlot(solution, constraints, user, required.Date, required.ShiftLabel);
                    ClearUnprotectedAdjacentConflicts(solution, constraints, user, required.Date, required.ShiftLabel);
                    ClearUnprotectedSameDayConflicts(solution, constraints, user, required.Date, required.ShiftLabel);

                    if (IsUserAvailable(user, required.Date, required.ShiftLabel, solution, constraints))
                    {
                        break;
                    }
                }

                if (!IsUserAvailable(user, required.Date, required.ShiftLabel, solution, constraints))
                {
                    continue;
                }

                RemoveOtherDailyAssignments(solution, constraints, user, required.Date, shiftReq.ShiftId);
                MakeRoomForIncoming(solution, constraints, shiftReq, required.Date, user);

                PrepareForRequiredSlot(solution, constraints, user, required.Date, required.ShiftLabel);
                ClearUnprotectedAdjacentConflicts(solution, constraints, user, required.Date, required.ShiftLabel);
                ClearUnprotectedSameDayConflicts(solution, constraints, user, required.Date, required.ShiftLabel);

                if (!IsUserAvailable(user, required.Date, required.ShiftLabel, solution, constraints))
                {
                    continue;
                }

                solution.AddAssignment(
                    user.UserId,
                    shiftReq.ShiftId,
                    required.Date.Date,
                    required.ShiftLabel,
                    isOnCall: false);
            }
        }

        private static void ForceRequiredPresenceDates(ShiftSolution solution, ShiftConstraints constraints)
        {
            foreach (var user in constraints.UserConstraints)
            {
                foreach (var presenceDate in user.RequiredPresenceDates)
                {
                    if (presenceDate.Date < constraints.StartDate.Date ||
                        presenceDate.Date > constraints.EndDate.Date)
                    {
                        continue;
                    }

                    if (user.UnavailableDates.Any(d => d.Date == presenceDate.Date))
                    {
                        continue;
                    }

                    if (solution.GetUserAssignments(user.UserId, presenceDate).Any(a => !a.IsOnCall))
                    {
                        continue;
                    }

                    var onCallOnly = solution.GetUserAssignments(user.UserId, presenceDate)
                        .FirstOrDefault(a => a.IsOnCall);

                    var existingLabels = solution.GetUserAssignments(user.UserId, presenceDate)
                        .Where(a => !a.IsOnCall)
                        .Select(a => a.ShiftLabel)
                        .ToList();
                    var maxPerDay = constraints.HardRules.EnforceMaxShiftsPerDay
                        ? Math.Max(1, constraints.GlobalConstraints.MaxShiftsPerDay)
                        : 2;

                    var candidates = constraints.ShiftRequirements
                        .Where(s => IsOffConflictFree(user, presenceDate, s.ShiftLabel))
                        .Where(s => Common.Utilities.ShiftEligibilityResolver.IsAssignmentAllowed(
                            user, existingLabels, s.ShiftLabel, maxPerDay,
                            constraints.HardRules.ForbidDuplicateDailyAssignments))
                        .OrderByDescending(s => onCallOnly != null && s.ShiftId == onCallOnly.ShiftId ? 1_000_000 : 0)
                        .ThenByDescending(s =>
                        {
                            var req = s.SpecialtyRequirements.FirstOrDefault(r => r.SpecialtyId == user.SpecialtyId);
                            if (req == null)
                            {
                                return -1;
                            }

                            var current = solution.GetShiftAssignments(s.ShiftId, presenceDate)
                                .Count(a => !a.IsOnCall && GetSpecialty(constraints, a.UserId) == user.SpecialtyId);
                            return req.RequiredTotalCount - current;
                        })
                        .ThenBy(s =>
                            Common.Utilities.AdjacentShiftRestRules.WouldConflict(
                                solution.GetUserAllAssignments(user.UserId), presenceDate, s.ShiftLabel, constraints)
                                ? 1
                                : 0)
                        .ThenBy(s => s.ShiftId)
                        .ToList();

                    ShiftRequirement? target = null;
                    foreach (var candidate in candidates)
                    {
                        ClearUnprotectedAdjacentConflicts(solution, constraints, user, presenceDate, candidate.ShiftLabel);
                        ClearUnprotectedSameDayConflicts(solution, constraints, user, presenceDate, candidate.ShiftLabel);
                        if (IsUserAvailable(user, presenceDate, candidate.ShiftLabel, solution, constraints))
                        {
                            target = candidate;
                            break;
                        }
                    }

                    if (target == null)
                    {
                        continue;
                    }

                    if (onCallOnly == null || onCallOnly.ShiftId != target.ShiftId)
                    {
                        RemoveOtherDailyAssignments(solution, constraints, user, presenceDate, target.ShiftId);
                    }

                    MakeRoomForIncoming(solution, constraints, target, presenceDate, user);
                    solution.AddAssignment(
                        user.UserId,
                        target.ShiftId,
                        presenceDate.Date,
                        target.ShiftLabel,
                        isOnCall: false);
                }
            }
        }

        /// <summary>
        /// برای حضور اجباری، انتساب‌های غیرمحافظت‌شده‌ای که توالی ممنوع می‌سازند حذف می‌شوند.
        /// </summary>
        private static void PrepareForRequiredSlot(
            ShiftSolution solution,
            ShiftConstraints constraints,
            UserConstraint user,
            DateTime date,
            ShiftLabel label)
        {
            if (label == ShiftLabel.Night)
            {
                var next = date.Date.AddDays(1);
                if (!constraints.HardRules.AllowEveningAfterNightShift)
                {
                    foreach (var assignment in solution.GetUserAssignments(user.UserId, next).ToList())
                    {
                        if (!IsHardProtectedAssignment(user, assignment))
                        {
                            solution.RemoveAssignment(assignment.UserId, assignment.ShiftId, assignment.Date);
                        }
                    }
                }
                else
                {
                    foreach (var assignment in solution.GetUserAssignments(user.UserId, next)
                                 .Where(a => a.ShiftLabel == ShiftLabel.Morning)
                                 .ToList())
                    {
                        if (!IsHardProtectedAssignment(user, assignment))
                        {
                            solution.RemoveAssignment(assignment.UserId, assignment.ShiftId, assignment.Date);
                        }
                    }
                }

                if (!constraints.HardRules.AllowEveningAfterNightShift)
                {
                    foreach (var assignment in GetUserNightAssignments(solution, user.UserId)
                                 .Where(a => a.Date.Date == date.Date.AddDays(-1))
                                 .ToList())
                    {
                        if (!IsHardProtectedAssignment(user, assignment))
                        {
                            solution.RemoveAssignment(assignment.UserId, assignment.ShiftId, assignment.Date);
                        }
                    }
                }
            }

            foreach (var assignment in solution.GetUserAssignments(user.UserId, date)
                         .Where(a => a.ShiftLabel == ShiftLabel.Evening && a.ShiftLabel != label)
                         .ToList())
            {
                if (label == ShiftLabel.Night && !IsHardProtectedAssignment(user, assignment))
                {
                    solution.RemoveAssignment(assignment.UserId, assignment.ShiftId, assignment.Date);
                }
            }
        }

        private static IEnumerable<SaShiftAssignment> GetUserNightAssignments(ShiftSolution solution, int userId) =>
            solution.GetUserAllAssignments(userId)
                .Where(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall);

        private static string DescribeUnmetRequiredSlot(
            ShiftSolution solution,
            ShiftConstraints constraints,
            UserConstraint user,
            ShiftSlotConstraint required,
            ShiftRequirement? shiftReq)
        {
            var baseMsg =
                $"حضور اجباری شیفت‌مشخص اعمال نشد: کاربر {user.UserId} ({user.UserName}) باید در {required.ShiftLabel} تاریخ {required.Date:yyyy-MM-dd} باشد.";

            if (!Common.Utilities.ShiftEligibilityResolver.MayEverTakeLabel(user, required.ShiftLabel))
            {
                return baseMsg + " علت: کاربر مجوز این نوع شیفت را ندارد.";
            }

            if (shiftReq == null)
            {
                return baseMsg + " علت: شیفت در تنظیمات دپارتمان یافت نشد.";
            }

            if (user.UnavailableDates.Any(d => d.Date == required.Date.Date))
            {
                return baseMsg + " علت: درخواست عدم‌حضور کل‌روز تأییدشده برای همین تاریخ وجود دارد.";
            }

            if (user.UnavailableShiftSlots.Any(s =>
                    s.Date.Date == required.Date.Date && s.ShiftLabel == required.ShiftLabel) &&
                !IsApprovedRequiredSlot(user, required.Date, required.ShiftLabel, required.ShiftId))
            {
                return baseMsg + " علت: درخواست عدم‌حضور تأییدشده برای همین شیفت وجود دارد.";
            }

            if (Common.Utilities.ApprovedOffNightBeforeRules.IsNightBlockedByApprovedOff(user, required.Date) &&
                !IsApprovedRequiredSlot(user, required.Date, required.ShiftLabel, required.ShiftId))
            {
                return baseMsg +
                       " علت: درخواست OFF صبح/کل‌روز روز بعد، شب این تاریخ را مسدود کرده است.";
            }

            var specialtyReq = shiftReq.SpecialtyRequirements
                .FirstOrDefault(r => r.SpecialtyId == user.SpecialtyId);
            var day = specialtyReq?.ForDay(constraints.IsHoliday(required.Date));
            if (specialtyReq == null || day == null || day.Value.RequiredTotalCount <= 0)
            {
                return baseMsg + " علت: ظرفیت این شیفت در این تاریخ صفر است.";
            }

            var occupants = solution.GetShiftAssignments(shiftReq.ShiftId, required.Date)
                .Where(a => !a.IsOnCall && a.UserId != user.UserId)
                .ToList();
            var blockedByApprovedPeers = occupants.Count(a =>
            {
                var peer = constraints.UserConstraints.FirstOrDefault(u => u.UserId == a.UserId);
                return peer != null &&
                       IsApprovedRequiredSlot(peer, required.Date, required.ShiftLabel, shiftReq.ShiftId);
            });
            if (blockedByApprovedPeers >= day.Value.RequiredTotalCount)
            {
                return baseMsg + " علت: صندلی با درخواست ON تأییدشدهٔ کاربر دیگر پر شده است.";
            }

            if (!IsUserAvailable(user, required.Date, required.ShiftLabel, solution, constraints))
            {
                var next = required.Date.Date.AddDays(1);
                var protectedNextDay = solution.GetUserAssignments(user.UserId, next)
                    .Any(a => !a.IsOnCall && IsHardProtectedAssignment(user, a));
                if (required.ShiftLabel == ShiftLabel.Night && protectedNextDay)
                {
                    return baseMsg +
                           $" علت: درخواست تأییدشدهٔ دیگر در {next:yyyy-MM-dd} با استراحت بعد از شب (بیش از ۱۲ ساعت کار متوالی) تداخل دارد.";
                }

                return baseMsg +
                       " علت: قوانین توالی شیفت (شب→صبح روز بعد) یا سقف شیفت روزانه مانع تخصیص است.";
            }

            return baseMsg;
        }

        /// <summary>
        /// برای حضور اجباری، انتساب‌های غیرمحافظت‌شده‌ای که توالی ممنوع می‌سازند حذف می‌شوند.
        /// </summary>
        private static void ClearUnprotectedAdjacentConflicts(
            ShiftSolution solution,
            ShiftConstraints constraints,
            UserConstraint user,
            DateTime date,
            ShiftLabel label)
        {
            foreach (var assignment in solution.GetUserAllAssignments(user.UserId).ToList())
            {
                if (assignment.Date.Date == date.Date && assignment.ShiftLabel == label)
                {
                    continue;
                }

                var earlierLabel = assignment.ShiftLabel;
                var earlierDate = assignment.Date.Date;
                var laterLabel = label;
                var laterDate = date.Date;
                if (date.Date < assignment.Date.Date ||
                    (date.Date == assignment.Date.Date &&
                     Common.Utilities.AdjacentShiftRestRules.LabelOrder(label) <
                     Common.Utilities.AdjacentShiftRestRules.LabelOrder(assignment.ShiftLabel)))
                {
                    earlierLabel = label;
                    earlierDate = date.Date;
                    laterLabel = assignment.ShiftLabel;
                    laterDate = assignment.Date.Date;
                }

                if (!Common.Utilities.AdjacentShiftRestRules.IsForbiddenBackToBack(
                        earlierLabel, earlierDate, laterLabel, laterDate,
                        constraints.HardRules.AllowEveningAfterNightShift))
                {
                    continue;
                }

                if (IsHardProtectedAssignment(user, assignment))
                {
                    continue;
                }

                solution.RemoveAssignment(assignment.UserId, assignment.ShiftId, assignment.Date);
            }
        }

        /// <summary>
        /// ترکیب غیرمجاز همان‌روز را برای جای‌گذاری اجباری پاک می‌کند.
        /// اگر MaxShiftsPerDay=1 باشد هر شیفت دیگر همان روز حذف می‌شود.
        /// </summary>
        private static void ClearUnprotectedSameDayConflicts(
            ShiftSolution solution,
            ShiftConstraints constraints,
            UserConstraint user,
            DateTime date,
            ShiftLabel label)
        {
            var maxPerDay = constraints.HardRules.EnforceMaxShiftsPerDay
                ? Math.Max(1, constraints.GlobalConstraints.MaxShiftsPerDay)
                : 2;

            foreach (var assignment in solution.GetUserAssignments(user.UserId, date).ToList())
            {
                if (assignment.ShiftLabel == label)
                {
                    continue;
                }

                var conflicts = maxPerDay <= 1
                    || (label == ShiftLabel.Night && assignment.ShiftLabel == ShiftLabel.Evening)
                    || (label == ShiftLabel.Evening && assignment.ShiftLabel == ShiftLabel.Night);

                if (!conflicts)
                {
                    continue;
                }

                if (IsHardProtectedAssignment(user, assignment))
                {
                    continue;
                }

                solution.RemoveAssignment(assignment.UserId, assignment.ShiftId, assignment.Date);
            }
        }

        private static bool IsHardProtectedAssignment(UserConstraint user, SaShiftAssignment assignment)
        {
            if (user.RequiredShiftSlots.Any(s =>
                    s.Date.Date == assignment.Date.Date && s.ShiftLabel == assignment.ShiftLabel))
            {
                return true;
            }

            // حضور کل‌روز روی روز دیگر را حفظ کن تا لیبل دیگری برای امروز انتخاب شود
            return !assignment.IsOnCall &&
                   user.RequiredPresenceDates.Any(d => d.Date == assignment.Date.Date);
        }

        private static void MakeRoomForIncoming(
            ShiftSolution solution,
            ShiftConstraints constraints,
            ShiftRequirement shiftReq,
            DateTime date,
            UserConstraint incoming)
        {
            var specialtyReq = shiftReq.SpecialtyRequirements
                .FirstOrDefault(r => r.SpecialtyId == incoming.SpecialtyId);
            var day = specialtyReq?.ForDay(constraints.IsHoliday(date));
            if (specialtyReq == null || day == null || day.Value.RequiredTotalCount <= 0)
            {
                return;
            }

            var dayCounts = day.Value;
            var regulars = solution.GetShiftAssignments(shiftReq.ShiftId, date)
                .Where(a => !a.IsOnCall &&
                            a.UserId != incoming.UserId &&
                            GetSpecialty(constraints, a.UserId) == incoming.SpecialtyId)
                .ToList();

            if (regulars.Count < dayCounts.RequiredTotalCount)
            {
                return;
            }

            var targetBeforeAdd = Math.Max(0, dayCounts.RequiredTotalCount - 1);
            var incomingHasRequired = IsApprovedRequiredSlot(incoming, date, shiftReq.ShiftLabel, shiftReq.ShiftId);

            while (regulars.Count > targetBeforeAdd)
            {
                var removable = regulars
                    .Where(a => a.UserId != incoming.UserId)
                    .Where(a =>
                    {
                        var u = constraints.UserConstraints.FirstOrDefault(x => x.UserId == a.UserId);
                        if (u == null)
                        {
                            return true;
                        }

                        // هرگز ON تأییدشدهٔ دیگر را برای همین صندلی حذف نکن
                        if (IsApprovedRequiredSlot(u, date, shiftReq.ShiftLabel, shiftReq.ShiftId))
                        {
                            return false;
                        }

                        return true;
                    })
                    .OrderBy(a =>
                    {
                        var u = constraints.UserConstraints.FirstOrDefault(x => x.UserId == a.UserId);
                        if (u != null && u.RequiredPresenceDates.Any(d => d.Date == date.Date))
                        {
                            return 1;
                        }

                        return 0;
                    })
                    .FirstOrDefault();

                if (removable == null)
                {
                    break;
                }

                solution.RemoveAssignment(removable.UserId, removable.ShiftId, removable.Date);
                regulars = solution.GetShiftAssignments(shiftReq.ShiftId, date)
                    .Where(a => !a.IsOnCall &&
                                a.UserId != incoming.UserId &&
                                GetSpecialty(constraints, a.UserId) == incoming.SpecialtyId)
                    .ToList();
            }

            // اگر ورودی ON تأییدشده دارد و هنوز صندلی پر است، فقط غیر ON را یک بار دیگر پاک کن
            if (incomingHasRequired && regulars.Count >= dayCounts.RequiredTotalCount)
            {
                foreach (var occupant in regulars.ToList())
                {
                    var u = constraints.UserConstraints.FirstOrDefault(x => x.UserId == occupant.UserId);
                    if (u != null && IsApprovedRequiredSlot(u, date, shiftReq.ShiftLabel, shiftReq.ShiftId))
                    {
                        continue;
                    }

                    solution.RemoveAssignment(occupant.UserId, occupant.ShiftId, occupant.Date);
                }
            }
        }

        private static void RemoveOtherDailyAssignments(
            ShiftSolution solution,
            ShiftConstraints constraints,
            UserConstraint user,
            DateTime date,
            int keepShiftId)
        {
            foreach (var assignment in solution.GetUserAssignments(user.UserId, date).ToList())
            {
                if (assignment.ShiftId == keepShiftId)
                {
                    continue;
                }

                if (IsHardProtectedAssignment(user, assignment))
                {
                    continue;
                }

                solution.RemoveAssignment(user.UserId, assignment.ShiftId, date);
            }
        }

        private static bool IsOffConflictFree(UserConstraint user, DateTime date, ShiftLabel shiftLabel)
        {
            // ON صریح همان روز/شیفت بر OFF مشتق یا متعارض اولویت دارد
            if (IsApprovedRequiredSlot(user, date, shiftLabel))
            {
                return true;
            }

            if (user.UnavailableDates.Any(d => d.Date == date.Date))
            {
                return false;
            }

            return !user.UnavailableShiftSlots.Any(s =>
                s.Date.Date == date.Date && s.ShiftLabel == shiftLabel);
        }

        private static bool IsUserAvailable(
            UserConstraint user,
            DateTime date,
            ShiftLabel shiftLabel,
            ShiftSolution? solution = null,
            ShiftConstraints? constraints = null)
        {
            if (!IsOffConflictFree(user, date, shiftLabel))
            {
                return false;
            }

            if (solution != null && constraints != null)
            {
                var maxPerDay = constraints.HardRules.EnforceMaxShiftsPerDay
                    ? Math.Max(1, constraints.GlobalConstraints.MaxShiftsPerDay)
                    : 2;
                var existing = solution.GetUserAssignments(user.UserId, date)
                    .Where(a => a.ShiftLabel != shiftLabel)
                    .Select(a => a.ShiftLabel);
                if (!Common.Utilities.ShiftEligibilityResolver.IsAssignmentAllowed(
                        user, existing, shiftLabel, maxPerDay, constraints.HardRules.ForbidDuplicateDailyAssignments))
                {
                    return false;
                }
            }
            else if (!Common.Utilities.ShiftEligibilityResolver.MayEverTakeLabel(user, shiftLabel))
            {
                return false;
            }

            if (solution != null &&
                Common.Utilities.AdjacentShiftRestRules.WouldConflict(
                    solution.GetUserAllAssignments(user.UserId),
                    date,
                    shiftLabel,
                    allowEveningAfterNightShift: constraints?.HardRules.AllowEveningAfterNightShift ?? true))
            {
                return false;
            }

            return true;
        }

        private static ShiftRequirement? ResolveShift(ShiftConstraints constraints, ShiftLabel label, int specialtyId, int? shiftId = null)
        {
            if (shiftId.HasValue)
            {
                var byId = constraints.ShiftRequirements.FirstOrDefault(s => s.ShiftId == shiftId.Value);
                if (byId != null)
                {
                    return byId;
                }
            }

            var matches = constraints.ShiftRequirements.Where(s => s.ShiftLabel == label).ToList();
            if (matches.Count == 0)
            {
                return null;
            }

            if (matches.Count == 1)
            {
                return matches[0];
            }

            return matches
                .OrderByDescending(s =>
                    s.SpecialtyRequirements.FirstOrDefault(r => r.SpecialtyId == specialtyId)?.RequiredTotalCount ?? 0)
                .ThenBy(s => s.ShiftId)
                .First();
        }

        private static int GetSpecialty(ShiftConstraints constraints, int userId)
        {
            return constraints.UserConstraints.FirstOrDefault(u => u.UserId == userId)?.SpecialtyId ?? 0;
        }
    }
}
