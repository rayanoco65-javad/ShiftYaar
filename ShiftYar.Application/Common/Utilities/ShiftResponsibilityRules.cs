using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;
using System;
using System.Collections.Generic;
using System.Linq;
using static ShiftYar.Domain.Enums.DepartmentModel.DepartmentEnums;
using static ShiftYar.Domain.Enums.UserModel.UserEnums;
using ShiftYar.Domain.Enums.ShiftModel;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.Common.Utilities
{
    /// <summary>
    /// قواعد و منطق چیدمان و اعتبارسنجی مسئولیت‌ها و نقش‌های تخصصی پرسنل (اتاق عمل و ...)
    /// </summary>
    public static class ShiftResponsibilityRules
    {
        public static bool IsApplicable(ShiftConstraints constraints)
        {
            if (constraints == null) return false;
            if (constraints.StaffingMode is DepartmentStaffingMode.ResponsibilityBased or DepartmentStaffingMode.Hybrid)
            {
                return true;
            }

            return constraints.ShiftRequirements.Any(s => s.ResponsibilityRequirements != null && s.ResponsibilityRequirements.Count > 0);
        }

        /// <summary>
        /// تخصیص بهینه و هوشمند مسئولیت‌ها به انتساب‌های یک اسلات شیفت مشخص
        /// با اولویت نقش‌های محدودتر/کمیاب‌تر و رعایت دقیق جنسیت و سهمیه کل.
        /// </summary>
        private class DemandSlot
        {
            public int RespId { get; set; }
            public bool IsDefault { get; set; }
            public UserGender? RequiredGender { get; set; }
            public int Priority { get; set; }
            public int OriginalOrder { get; set; }
        }

        /// <summary>
        /// تخصیص بهینه و هوشمند مسئولیت‌ها به انتساب‌های یک اسلات شیفت مشخص
        /// با استفاده از الگوریتم تطابق بیشینه دو‌بخشی (Bipartite Matching / Augmenting Path)
        /// با اولویت نقش‌های غیرپیش‌فرض/تخصصی‌تر و اولویت نفرات با انعطاف‌پذیری کمتر (MRV).
        /// </summary>
        public static void AssignResponsibilitiesToSlot(
            List<SaShiftAssignment> slotAssignments,
            ShiftRequirement shiftReq,
            DateTime date,
            bool isHoliday,
            IReadOnlyList<UserConstraint> allUsers,
            int? defaultDepartmentResponsibilityId = null)
        {
            if (slotAssignments == null || slotAssignments.Count == 0 || shiftReq == null)
            {
                return;
            }

            var defaultRespId = (shiftReq.ResponsibilityRequirements ?? new List<ResponsibilityRequirement>())
                .FirstOrDefault(r => r.IsDefault)?.DepartmentResponsibilityId ?? defaultDepartmentResponsibilityId;

            var usersMap = allUsers.GroupBy(u => u.UserId).ToDictionary(g => g.Key, g => g.First());

            // ۱. ساخت اسلات‌های مورد نیاز نقش‌ها
            var demands = new List<DemandSlot>();
            var order = 0;
            foreach (var req in (shiftReq.ResponsibilityRequirements ?? new List<ResponsibilityRequirement>()))
            {
                var dayCounts = req.ForDay(isHoliday);
                var requiredMale = dayCounts.RequiredMaleCount;
                var requiredFemale = dayCounts.RequiredFemaleCount;
                var requiredTotal = dayCounts.RequiredTotalCount;

                for (var i = 0; i < requiredMale; i++)
                {
                    demands.Add(new DemandSlot
                    {
                        RespId = req.DepartmentResponsibilityId,
                        IsDefault = req.IsDefault,
                        RequiredGender = UserGender.Male,
                        Priority = req.Priority,
                        OriginalOrder = order++
                    });
                }
                for (var i = 0; i < requiredFemale; i++)
                {
                    demands.Add(new DemandSlot
                    {
                        RespId = req.DepartmentResponsibilityId,
                        IsDefault = req.IsDefault,
                        RequiredGender = UserGender.Female,
                        Priority = req.Priority,
                        OriginalOrder = order++
                    });
                }
                var remaining = requiredTotal - requiredMale - requiredFemale;
                for (var i = 0; i < remaining; i++)
                {
                    demands.Add(new DemandSlot
                    {
                        RespId = req.DepartmentResponsibilityId,
                        IsDefault = req.IsDefault,
                        RequiredGender = null,
                        Priority = req.Priority,
                        OriginalOrder = order++
                    });
                }
            }

            // ترتیب اسلات‌های تقاضا: ابتدا نقش‌های غیراستاندارد/تخصصی‌تر، سپس اولویت بالاتر
            var orderedDemands = demands
                .OrderBy(d => d.IsDefault ? 1 : 0)
                .ThenByDescending(d => d.Priority)
                .ThenBy(d => d.OriginalOrder)
                .ToList();

            var assignees = slotAssignments.Where(a => !a.IsOnCall).ToList();

            bool CanAssign(DemandSlot d, SaShiftAssignment a)
            {
                if (!usersMap.TryGetValue(a.UserId, out var u)) return false;
                if (d.RequiredGender.HasValue && u.Gender != d.RequiredGender.Value) return false;
                return u.CanPerformResponsibility(d.RespId, d.IsDefault);
            }

            // ۲. تطابق دوبخشی بیشینه با مسیر افزاینده (Augmenting Paths)
            var matchDemandToAssignee = new Dictionary<int, int>();
            var matchAssigneeToDemand = new Dictionary<int, int>();

            bool TryDfs(int demandIdx, HashSet<int> visitedAssignees)
            {
                var demand = orderedDemands[demandIdx];
                var eligibleAssignees = Enumerable.Range(0, assignees.Count)
                    .Where(aIdx => CanAssign(demand, assignees[aIdx]))
                    .OrderBy(aIdx => usersMap.TryGetValue(assignees[aIdx].UserId, out var u) ? u.ResponsibilityIds.Count : 99);

                foreach (var aIdx in eligibleAssignees)
                {
                    if (visitedAssignees.Contains(aIdx)) continue;
                    visitedAssignees.Add(aIdx);

                    if (!matchAssigneeToDemand.TryGetValue(aIdx, out var prevDemandIdx) || TryDfs(prevDemandIdx, visitedAssignees))
                    {
                        matchDemandToAssignee[demandIdx] = aIdx;
                        matchAssigneeToDemand[aIdx] = demandIdx;
                        return true;
                    }
                }

                return false;
            }

            for (var dIdx = 0; dIdx < orderedDemands.Count; dIdx++)
            {
                var visited = new HashSet<int>();
                TryDfs(dIdx, visited);
            }

            // ۳. اعمال نتایج روی انتساب‌های اسلات
            for (var aIdx = 0; aIdx < assignees.Count; aIdx++)
            {
                if (matchAssigneeToDemand.TryGetValue(aIdx, out var matchedDemandIdx))
                {
                    assignees[aIdx].ResponsibilityId = orderedDemands[matchedDemandIdx].RespId;
                }
                else if (defaultRespId.HasValue)
                {
                    assignees[aIdx].ResponsibilityId = defaultRespId.Value;
                }
            }
        }

        /// <summary>
        /// اجرای انتساب مسئولیت‌ها برای تمام اسلات‌های کل راه‌حل
        /// </summary>
        public static void AssignResponsibilitiesToAllSlots(ShiftSolution solution, ShiftConstraints constraints)
        {
            if (solution == null || constraints == null) return;

            for (var date = constraints.StartDate.Date; date <= constraints.EndDate.Date; date = date.AddDays(1))
            {
                var isHoliday = constraints.IsHoliday(date);
                foreach (var shiftReq in constraints.ShiftRequirements)
                {
                    var slotAssignees = solution.GetShiftAssignments(shiftReq.ShiftId, date)
                        .Where(a => !a.IsOnCall)
                        .ToList();

                    AssignResponsibilitiesToSlot(
                        slotAssignees,
                        shiftReq,
                        date,
                        isHoliday,
                        constraints.UserConstraints,
                        constraints.DefaultDepartmentResponsibilityId);
                }
            }
        }

        /// <summary>
        /// محاسبه میزان جریمه کسری یا عدم تطابق مسئولیت‌های مورد نیاز در طول بازه زمان‌بندی
        /// </summary>
        public static double CalculateDeficitPenalty(ShiftSolution solution, ShiftConstraints constraints)
        {
            if (solution == null || constraints == null || !IsApplicable(constraints))
                return 0;

            double penalty = 0;
            var usersMap = constraints.UserConstraints.GroupBy(u => u.UserId).ToDictionary(g => g.Key, g => g.First());

            for (var date = constraints.StartDate.Date; date <= constraints.EndDate.Date; date = date.AddDays(1))
            {
                var isHoliday = constraints.IsHoliday(date);
                foreach (var shiftReq in constraints.ShiftRequirements)
                {
                    if (shiftReq.ResponsibilityRequirements == null || shiftReq.ResponsibilityRequirements.Count == 0)
                        continue;

                    var slotAssignments = solution.GetShiftAssignments(shiftReq.ShiftId, date)
                        .Where(a => !a.IsOnCall)
                        .ToList();

                    if (slotAssignments.Count > 0)
                    {
                        // انتساب موقت بر روی این اسلات جهت ارزیابی میزان ارضای نیازها
                        AssignResponsibilitiesToSlot(
                            slotAssignments,
                            shiftReq,
                            date,
                            isHoliday,
                            constraints.UserConstraints,
                            constraints.DefaultDepartmentResponsibilityId);
                    }

                    foreach (var req in shiftReq.ResponsibilityRequirements)
                    {
                        var dayCounts = req.ForDay(isHoliday);
                        if (dayCounts.RequiredTotalCount <= 0) continue;

                        var assigneesWithResp = slotAssignments
                            .Where(a => a.ResponsibilityId == req.DepartmentResponsibilityId)
                            .ToList();

                        var males = assigneesWithResp.Count(a => usersMap.TryGetValue(a.UserId, out var u) && u.Gender == UserGender.Male);
                        var females = assigneesWithResp.Count(a => usersMap.TryGetValue(a.UserId, out var u) && u.Gender == UserGender.Female);
                        var total = assigneesWithResp.Count;

                        if (dayCounts.RequiredMaleCount > 0 && males < dayCounts.RequiredMaleCount)
                        {
                            penalty += (dayCounts.RequiredMaleCount - males) * 4000.0;
                        }
                        if (dayCounts.RequiredFemaleCount > 0 && females < dayCounts.RequiredFemaleCount)
                        {
                            penalty += (dayCounts.RequiredFemaleCount - females) * 4000.0;
                        }
                        if (total < dayCounts.RequiredTotalCount)
                        {
                            penalty += (dayCounts.RequiredTotalCount - total) * 5000.0;
                        }
                    }
                }
            }

            return penalty;
        }

        /// <summary>
        /// استخراج خطاهای عدم پوشش مسئولیت‌های مورد نیاز در صورت وجود
        /// قبل از بررسی، انتساب دقیق نقش‌ها بر روی تمام اسلات‌ها انجام می‌شود تا از شمارش دقیق اطمینان حاصل گردد.
        /// </summary>
        public static List<string> GetViolations(ShiftSolution solution, ShiftConstraints constraints)
        {
            var violations = new List<string>();
            if (!IsApplicable(constraints)) return violations;

            AssignResponsibilitiesToAllSlots(solution, constraints);

            var usersMap = constraints.UserConstraints.GroupBy(u => u.UserId).ToDictionary(g => g.Key, g => g.First());

            for (var date = constraints.StartDate.Date; date <= constraints.EndDate.Date; date = date.AddDays(1))
            {
                var isHoliday = constraints.IsHoliday(date);
                var persianDate = DateConverter.ConvertToPersianDate(date);
                foreach (var shiftReq in constraints.ShiftRequirements)
                {
                    if (shiftReq.ResponsibilityRequirements == null || shiftReq.ResponsibilityRequirements.Count == 0) continue;

                    var slotAssignments = solution.GetShiftAssignments(shiftReq.ShiftId, date)
                        .Where(a => !a.IsOnCall)
                        .ToList();

                    foreach (var req in shiftReq.ResponsibilityRequirements)
                    {
                        var dayCounts = req.ForDay(isHoliday);
                        if (dayCounts.RequiredTotalCount <= 0) continue;

                        var assigneesWithResp = slotAssignments
                            .Where(a => a.ResponsibilityId == req.DepartmentResponsibilityId)
                            .ToList();

                        var males = assigneesWithResp.Count(a => usersMap.TryGetValue(a.UserId, out var u) && u.Gender == UserGender.Male);
                        var females = assigneesWithResp.Count(a => usersMap.TryGetValue(a.UserId, out var u) && u.Gender == UserGender.Female);
                        var total = assigneesWithResp.Count;

                        if (total < dayCounts.RequiredTotalCount ||
                            (dayCounts.RequiredMaleCount > 0 && males < dayCounts.RequiredMaleCount) ||
                            (dayCounts.RequiredFemaleCount > 0 && females < dayCounts.RequiredFemaleCount))
                        {
                            var title = !string.IsNullOrEmpty(req.ResponsibilityTitle)
                                ? req.ResponsibilityTitle
                                : constraints.ResponsibilityTitles.GetValueOrDefault(req.DepartmentResponsibilityId, "مسئولیت");

                            violations.Add($"کمبود نیرو برای مسئولیت '{title}' در شیفت {shiftReq.ShiftLabel} تاریخ {persianDate} ({date:yyyy-MM-dd}) " +
                                           $"(نیاز: کل={dayCounts.RequiredTotalCount}، مرد={dayCounts.RequiredMaleCount}، زن={dayCounts.RequiredFemaleCount} | موجود: کل={total}، مرد={males}، زن={females})");
                        }
                    }
                }
            }

            return violations;
        }

        /// <summary>
        /// فاز جبران و ترمیم کسری مسئولیت‌ها:
        /// شناسایی اسلات‌هایی که کمبود نقش دارند و جایگزینی پرسنل مازاد/پیش‌فرض با پرسنل واجد شرایط.
        /// </summary>
        public static void RepairResponsibilityDeficits(ShiftSolution solution, ShiftConstraints constraints)
        {
            if (solution == null || constraints == null || !IsApplicable(constraints))
                return;

            var usersMap = constraints.UserConstraints.GroupBy(u => u.UserId).ToDictionary(g => g.Key, g => g.First());

            for (var pass = 0; pass < 3; pass++)
            {
                var deficitsFound = false;

                for (var date = constraints.StartDate.Date; date <= constraints.EndDate.Date; date = date.AddDays(1))
                {
                    var isHoliday = constraints.IsHoliday(date);

                    foreach (var shiftReq in constraints.ShiftRequirements)
                    {
                        if (shiftReq.ResponsibilityRequirements == null || shiftReq.ResponsibilityRequirements.Count == 0)
                            continue;

                        var slotAssignments = solution.GetShiftAssignments(shiftReq.ShiftId, date)
                            .Where(a => !a.IsOnCall)
                            .ToList();

                        if (slotAssignments.Count == 0) continue;

                        AssignResponsibilitiesToSlot(
                            slotAssignments,
                            shiftReq,
                            date,
                            isHoliday,
                            constraints.UserConstraints,
                            constraints.DefaultDepartmentResponsibilityId);

                        foreach (var req in shiftReq.ResponsibilityRequirements)
                        {
                            var dayCounts = req.ForDay(isHoliday);
                            if (dayCounts.RequiredTotalCount <= 0) continue;

                            var assigneesWithResp = slotAssignments
                                .Where(a => a.ResponsibilityId == req.DepartmentResponsibilityId)
                                .ToList();

                            var males = assigneesWithResp.Count(a => usersMap.TryGetValue(a.UserId, out var u) && u.Gender == UserGender.Male);
                            var females = assigneesWithResp.Count(a => usersMap.TryGetValue(a.UserId, out var u) && u.Gender == UserGender.Female);
                            var total = assigneesWithResp.Count;

                            var missingTotal = dayCounts.RequiredTotalCount - total;
                            var missingMale = Math.Max(0, dayCounts.RequiredMaleCount - males);
                            var missingFemale = Math.Max(0, dayCounts.RequiredFemaleCount - females);

                            if (missingTotal <= 0 && missingMale <= 0 && missingFemale <= 0)
                                continue;

                            deficitsFound = true;

                            // کاندیداهای معتبر: پرسنل فعال و واجد صلاحیت مسئولیت که با رعایت سقف روزانه دپارتمان و مجوزهای نوع شیفت کاربر مجاز به اخذ این شیفت هستند
                            var candidates = constraints.UserConstraints
                                .Where(u => u.IsActive)
                                .Where(u => u.SpecialtyId == 0 || shiftReq.SpecialtyRequirements.Any(sr => sr.SpecialtyId == u.SpecialtyId) || constraints.StaffingMode == DepartmentStaffingMode.ResponsibilityBased)
                                .Where(u => u.CanPerformResponsibility(req.DepartmentResponsibilityId, req.IsDefault))
                                .Where(u => missingMale == 0 || u.Gender == UserGender.Male)
                                .Where(u => missingFemale == 0 || u.Gender == UserGender.Female)
                                // نباید مرخصی یا عدم دسترسی در این تاریخ داشته باشد
                                .Where(u => !u.UnavailableDates.Any(d => d.Date == date.Date))
                                .Where(u => !u.UnavailableShiftSlots.Any(s => s.Date.Date == date.Date && s.ShiftLabel == shiftReq.ShiftLabel))
                                // بررسی سقف شیفت روزانه دپارتمان و مجوزهای نوع شیفت کاربر (یک شیفت یا دو شیفت مجاز)
                                .Where(u =>
                                {
                                    var existingAssignments = solution.GetUserAssignments(u.UserId, date)
                                        .Where(a => !a.IsOnCall)
                                        .ToList();

                                    var maxPerDay = constraints.HardRules.EnforceMaxShiftsPerDay
                                        ? Math.Max(1, constraints.GlobalConstraints.MaxShiftsPerDay)
                                        : 2;
                                    var approvedCount = u.RequiredShiftSlots.Count(s => s.Date.Date == date.Date);
                                    var effectiveMax = Math.Max(maxPerDay, approvedCount);

                                    var existingLabels = existingAssignments.Select(a => a.ShiftLabel).Distinct().ToList();
                                    var trialLabels = existingLabels.Append(shiftReq.ShiftLabel).Distinct().ToList();
                                    if (u.CanBypassMaxShiftsPerDay(trialLabels, date) && effectiveMax < 2)
                                    {
                                        effectiveMax = 2;
                                    }

                                    if (existingAssignments.Count >= effectiveMax)
                                    {
                                        return false;
                                    }

                                    if (!DailyAssignmentRules.IsValidDaySet(trialLabels, effectiveMax, constraints.HardRules.ForbidDuplicateDailyAssignments))
                                    {
                                        return false;
                                    }

                                    return ShiftEligibilityResolver.IsAssignmentAllowed(
                                        u,
                                        existingLabels,
                                        shiftReq.ShiftLabel,
                                        effectiveMax,
                                        constraints.HardRules.ForbidDuplicateDailyAssignments,
                                        date,
                                        constraints.StartDate);
                                })
                                // عدم تداخل توالی شیفت‌های مجاور (شب قبل از صبح، عصر بعد از شب و ...)
                                .Where(u => !AdjacentShiftRestRules.WouldConflict(solution.GetUserAllAssignments(u.UserId), date, shiftReq.ShiftLabel, constraints))
                                // عدم تجاوز از سقف روزهای کاری متوالی
                                .Where(u => !MaxConsecutiveWorkdayRules.WouldExceedMaxConsecutiveWorkdays(solution, constraints, u, date))
                                .OrderBy(u => u.ResponsibilityIds.Count)
                                .ToList();

                            // پرسنل قابل تعویض در این اسلات: بدون درخواست تأییدشده یا قفل اسکلتون، با اولویت نقش پیش‌فرض
                            var defaultId = constraints.DefaultDepartmentResponsibilityId ?? (shiftReq.ResponsibilityRequirements.FirstOrDefault(r => r.IsDefault)?.DepartmentResponsibilityId ?? 0);
                            var replaceable = slotAssignments
                                .Where(a =>
                                {
                                    var u = constraints.UserConstraints.FirstOrDefault(x => x.UserId == a.UserId);
                                    if (u == null) return false;
                                    if (ApprovedRequestGuard.IsApprovedRequiredSlot(u, date, a.ShiftLabel, a.ShiftId)) return false;
                                    if (solution.IsLockedSkeleton(a.UserId, a.ShiftId, date)) return false;
                                    return true;
                                })
                                .OrderByDescending(a => a.ResponsibilityId == defaultId ? 1 : 0)
                                .ToList();

                            var prevSatisfied = shiftReq.ResponsibilityRequirements.Sum(r => Math.Min(r.ForDay(isHoliday).RequiredTotalCount, slotAssignments.Count(x => x.ResponsibilityId == r.DepartmentResponsibilityId)));
                            var repaired = false;
                            foreach (var cand in candidates)
                            {
                                foreach (var rep in replaceable)
                                {
                                    // شبیه‌سازی جابجایی با حفظ راه‌حل پشتیبان
                                    solution.RemoveAssignment(rep.UserId, shiftReq.ShiftId, date);
                                    solution.AddAssignment(cand.UserId, shiftReq.ShiftId, date, shiftReq.ShiftLabel);

                                    var isSafe = true;
                                    // ۱. حفظ الزامات مسئول شیفت
                                    if (ShiftManagerRules.RequiresAnyManager(shiftReq) &&
                                        !OvertimeBalanceGuard.WouldPreserveManagerMix(solution, constraints, shiftReq.ShiftId, date, rep.UserId, cand.UserId))
                                    {
                                        isSafe = false;
                                    }
                                    // ۲. عدم ایجاد هرگونه نقض توالی مجاور، نوع شیفت یا تکرار روزانه
                                    else if (AdjacentShiftRestGuard.GetViolations(solution, constraints).Count > 0 ||
                                             ShiftEligibilityGuard.GetViolations(solution, constraints).Count > 0 ||
                                             DailyDuplicateAssignmentGuard.GetViolations(solution, constraints).Count > 0)
                                    {
                                        isSafe = false;
                                    }

                                    if (isSafe)
                                    {
                                        var newSlotAssignees = solution.GetShiftAssignments(shiftReq.ShiftId, date)
                                            .Where(a => !a.IsOnCall)
                                            .ToList();

                                        AssignResponsibilitiesToSlot(
                                            newSlotAssignees,
                                            shiftReq,
                                            date,
                                            isHoliday,
                                            constraints.UserConstraints,
                                            constraints.DefaultDepartmentResponsibilityId);

                                        var newSatisfied = shiftReq.ResponsibilityRequirements.Sum(r => Math.Min(r.ForDay(isHoliday).RequiredTotalCount, newSlotAssignees.Count(x => x.ResponsibilityId == r.DepartmentResponsibilityId)));
                                        if (newSatisfied > prevSatisfied)
                                        {
                                            repaired = true;
                                            break;
                                        }
                                        else
                                        {
                                            isSafe = false;
                                        }
                                    }

                                    if (!isSafe)
                                    {
                                        solution.RemoveAssignment(cand.UserId, shiftReq.ShiftId, date);
                                        solution.AddAssignment(rep.UserId, shiftReq.ShiftId, date, shiftReq.ShiftLabel);
                                    }
                                }

                                if (repaired) break;
                            }
                        }
                    }
                }

                if (!deficitsFound) break;
            }
            AssignResponsibilitiesToAllSlots(solution, constraints);
        }
    }
}
