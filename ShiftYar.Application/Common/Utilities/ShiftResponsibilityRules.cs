using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using static ShiftYar.Domain.Enums.DepartmentModel.DepartmentEnums;
using static ShiftYar.Domain.Enums.UserModel.UserEnums;

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

            var activeReqs = (shiftReq.ResponsibilityRequirements ?? new List<ResponsibilityRequirement>())
                .Where(r => r.ForDay(isHoliday).RequiredTotalCount > 0)
                .OrderByDescending(r => r.Priority)
                .ThenBy(r => r.IsDefault ? 1 : 0) // نقش‌های غیراستاندارد/تخصصی‌تر زودتر تخصیص می‌یابند
                .ToList();

            var usersMap = allUsers.GroupBy(u => u.UserId).ToDictionary(g => g.Key, g => g.First());
            var assignedUserIds = new HashSet<int>();

            // ۱. تخصیص نقش‌های الزامی تعریف‌شده
            foreach (var req in activeReqs)
            {
                var dayCounts = req.ForDay(isHoliday);
                var requiredMale = dayCounts.RequiredMaleCount;
                var requiredFemale = dayCounts.RequiredFemaleCount;
                var requiredTotal = dayCounts.RequiredTotalCount;
                var currentCount = 0;

                // الف: سهمیه مرد
                if (requiredMale > 0)
                {
                    var maleCandidates = slotAssignments
                        .Where(a => !assignedUserIds.Contains(a.UserId) && !a.IsOnCall)
                        .Where(a => usersMap.TryGetValue(a.UserId, out var u) && u.Gender == UserGender.Male && u.CanPerformResponsibility(req.DepartmentResponsibilityId, req.IsDefault))
                        .Take(requiredMale)
                        .ToList();

                    foreach (var asg in maleCandidates)
                    {
                        asg.ResponsibilityId = req.DepartmentResponsibilityId;
                        assignedUserIds.Add(asg.UserId);
                        currentCount++;
                    }
                }

                // ب: سهمیه زن
                if (requiredFemale > 0)
                {
                    var femaleCandidates = slotAssignments
                        .Where(a => !assignedUserIds.Contains(a.UserId) && !a.IsOnCall)
                        .Where(a => usersMap.TryGetValue(a.UserId, out var u) && u.Gender == UserGender.Female && u.CanPerformResponsibility(req.DepartmentResponsibilityId, req.IsDefault))
                        .Take(requiredFemale)
                        .ToList();

                    foreach (var asg in femaleCandidates)
                    {
                        asg.ResponsibilityId = req.DepartmentResponsibilityId;
                        assignedUserIds.Add(asg.UserId);
                        currentCount++;
                    }
                }

                // ج: تکمیل باقیمانده سهمیه کل (شناور بدون محدودیت جنسیت)
                var remainingTotalNeeded = requiredTotal - currentCount;
                if (remainingTotalNeeded > 0)
                {
                    var neutralCandidates = slotAssignments
                        .Where(a => !assignedUserIds.Contains(a.UserId) && !a.IsOnCall)
                        .Where(a => usersMap.TryGetValue(a.UserId, out var u) && u.CanPerformResponsibility(req.DepartmentResponsibilityId, req.IsDefault))
                        .Take(remainingTotalNeeded)
                        .ToList();

                    foreach (var asg in neutralCandidates)
                    {
                        asg.ResponsibilityId = req.DepartmentResponsibilityId;
                        assignedUserIds.Add(asg.UserId);
                    }
                }
            }

            // ۲. تخصیص نقش پیش‌فرض (سیرکولر) به سایر نفراتی که در شیفت هستند اما نقش خاصی نگرفته‌اند
            var defaultRespId = (shiftReq.ResponsibilityRequirements ?? new List<ResponsibilityRequirement>())
                .FirstOrDefault(r => r.IsDefault)?.DepartmentResponsibilityId ?? defaultDepartmentResponsibilityId;

            if (defaultRespId.HasValue)
            {
                var remainingAssignees = slotAssignments
                    .Where(a => !assignedUserIds.Contains(a.UserId) && !a.IsOnCall)
                    .ToList();

                foreach (var asg in remainingAssignees)
                {
                    asg.ResponsibilityId = defaultRespId.Value;
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
                            penalty += (dayCounts.RequiredMaleCount - males) * 150.0;
                        }
                        if (dayCounts.RequiredFemaleCount > 0 && females < dayCounts.RequiredFemaleCount)
                        {
                            penalty += (dayCounts.RequiredFemaleCount - females) * 150.0;
                        }
                        if (total < dayCounts.RequiredTotalCount)
                        {
                            penalty += (dayCounts.RequiredTotalCount - total) * 200.0;
                        }
                    }
                }
            }

            return penalty;
        }

        /// <summary>
        /// استخراج خطاهای عدم پوشش مسئولیت‌های مورد نیاز در صورت وجود
        /// </summary>
        public static List<string> GetViolations(ShiftSolution solution, ShiftConstraints constraints)
        {
            var violations = new List<string>();
            if (!IsApplicable(constraints)) return violations;

            var usersMap = constraints.UserConstraints.GroupBy(u => u.UserId).ToDictionary(g => g.Key, g => g.First());

            for (var date = constraints.StartDate.Date; date <= constraints.EndDate.Date; date = date.AddDays(1))
            {
                var isHoliday = constraints.IsHoliday(date);
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

                            violations.Add($"کمبود نیرو برای مسئولیت '{title}' در شیفت {shiftReq.ShiftLabel} تاریخ {date:yyyy-MM-dd} " +
                                           $"(نیاز: کل={dayCounts.RequiredTotalCount}، مرد={dayCounts.RequiredMaleCount}، زن={dayCounts.RequiredFemaleCount} | موجود: کل={total}، مرد={males}، زن={females})");
                        }
                    }
                }
            }

            return violations;
        }
    }
}
