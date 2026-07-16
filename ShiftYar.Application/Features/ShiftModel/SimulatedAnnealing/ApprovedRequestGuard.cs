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
                            $"حضور اجباری شیفت‌مشخص اعمال نشد: کاربر {user.UserId} ({user.UserName}) باید در {required.ShiftLabel} تاریخ {required.Date:yyyy-MM-dd} باشد.");
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
            foreach (var user in constraints.UserConstraints)
            {
                foreach (var required in user.RequiredShiftSlots)
                {
                    if (required.Date.Date < constraints.StartDate.Date ||
                        required.Date.Date > constraints.EndDate.Date)
                    {
                        continue;
                    }

                    if (!IsUserAvailable(user, required.Date, required.ShiftLabel, solution))
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

                    RemoveOtherDailyAssignments(solution, user.UserId, required.Date, shiftReq.ShiftId);
                    MakeRoomForIncoming(solution, constraints, shiftReq, required.Date, user);

                    solution.AddAssignment(
                        user.UserId,
                        shiftReq.ShiftId,
                        required.Date.Date,
                        required.ShiftLabel,
                        isOnCall: false);
                }
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

                    var candidates = constraints.ShiftRequirements
                        .Where(s => IsUserAvailable(user, presenceDate, s.ShiftLabel, solution))
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
                        .ThenBy(s => s.ShiftId)
                        .ToList();

                    var target = candidates.FirstOrDefault();
                    if (target == null)
                    {
                        continue;
                    }

                    if (onCallOnly == null || onCallOnly.ShiftId != target.ShiftId)
                    {
                        RemoveOtherDailyAssignments(solution, user.UserId, presenceDate, target.ShiftId);
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

        private static void MakeRoomForIncoming(
            ShiftSolution solution,
            ShiftConstraints constraints,
            ShiftRequirement shiftReq,
            DateTime date,
            UserConstraint incoming)
        {
            var specialtyReq = shiftReq.SpecialtyRequirements
                .FirstOrDefault(r => r.SpecialtyId == incoming.SpecialtyId);
            if (specialtyReq == null || specialtyReq.RequiredTotalCount <= 0)
            {
                return;
            }

            var regulars = solution.GetShiftAssignments(shiftReq.ShiftId, date)
                .Where(a => !a.IsOnCall &&
                            a.UserId != incoming.UserId &&
                            GetSpecialty(constraints, a.UserId) == incoming.SpecialtyId)
                .ToList();

            if (regulars.Count < specialtyReq.RequiredTotalCount)
            {
                return;
            }

            // هر کسی به‌جز دارندگان «حضور اجباری همان شیفت» قابل جایگزینی است
            var removable = regulars
                .OrderBy(a =>
                {
                    var u = constraints.UserConstraints.FirstOrDefault(x => x.UserId == a.UserId);
                    if (u != null &&
                        u.RequiredShiftSlots.Any(s => s.Date.Date == date.Date && s.ShiftLabel == shiftReq.ShiftLabel))
                    {
                        return 2;
                    }

                    if (u != null && u.RequiredPresenceDates.Any(d => d.Date == date.Date))
                    {
                        return 1;
                    }

                    return 0;
                })
                .FirstOrDefault(a =>
                {
                    var u = constraints.UserConstraints.FirstOrDefault(x => x.UserId == a.UserId);
                    return u == null ||
                           !u.RequiredShiftSlots.Any(s => s.Date.Date == date.Date && s.ShiftLabel == shiftReq.ShiftLabel);
                });

            if (removable != null)
            {
                solution.RemoveAssignment(removable.UserId, removable.ShiftId, removable.Date);
            }
        }

        private static void RemoveOtherDailyAssignments(ShiftSolution solution, int userId, DateTime date, int keepShiftId)
        {
            foreach (var assignment in solution.GetUserAssignments(userId, date).ToList())
            {
                if (assignment.ShiftId != keepShiftId)
                {
                    solution.RemoveAssignment(userId, assignment.ShiftId, date);
                }
            }
        }

        private static bool IsOffConflictFree(UserConstraint user, DateTime date, ShiftLabel shiftLabel)
        {
            if (user.UnavailableDates.Any(d => d.Date == date.Date))
            {
                return false;
            }

            return !user.UnavailableShiftSlots.Any(s =>
                s.Date.Date == date.Date && s.ShiftLabel == shiftLabel);
        }

        private static bool IsUserAvailable(UserConstraint user, DateTime date, ShiftLabel shiftLabel, ShiftSolution? solution = null)
        {
            if (!IsOffConflictFree(user, date, shiftLabel))
            {
                return false;
            }

            if (!Common.Utilities.ShiftEligibilityResolver.IsLabelAllowed(user.AllowedShiftLabels, shiftLabel))
            {
                return false;
            }

            if (solution != null &&
                Common.Utilities.AdjacentShiftRestRules.WouldConflict(
                    solution.GetUserAllAssignments(user.UserId), date, shiftLabel))
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
