using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;

/// <summary>
/// گارد الزام شیفت‌های مقید/همگام روزانه (Coupled Shift Rules):
/// 1- صبح گرفت -> عصر همان روز را هم بگیرد (MorningRequiresEvening)
/// 2- صبح گرفت -> شب همان روز را هم بگیرد (MorningRequiresNight)
/// 3- عصر گرفت -> صبح همان روز را هم بگیرد (EveningRequiresMorning)
/// 4- شب گرفت -> صبح همان روز را هم بگیرد (NightRequiresMorning)
///
/// این تنظیم بر سقف حداکثر شیفت در روز (MaxShiftsPerDay) در تنظیمات زمان‌بندی دپارتمان اولویت قطعی دارد.
/// </summary>
public static class CoupledShiftRuleGuard
{
    public static void Enforce(ShiftSolution solution, ShiftConstraints constraints)
    {
        var usersWithCoupledRules = constraints.UserConstraints
            .Where(u => u.HasCoupledShiftRules)
            .ToList();

        if (usersWithCoupledRules.Count == 0)
        {
            return;
        }

        foreach (var user in usersWithCoupledRules)
        {
            var userAssignments = solution.GetUserAllAssignments(user.UserId).ToList();
            var groupedByDate = userAssignments
                .GroupBy(a => a.Date.Date)
                .ToList();

            foreach (var group in groupedByDate)
            {
                var date = group.Key;
                var currentLabels = group.Select(a => a.ShiftLabel).ToHashSet();

                var requiredCoupledLabels = new HashSet<ShiftLabel>();
                foreach (var assignment in group)
                {
                    var reqLabel = user.GetRequiredCoupledShift(assignment.ShiftLabel);
                    if (reqLabel.HasValue)
                    {
                        requiredCoupledLabels.Add(reqLabel.Value);
                    }
                }

                foreach (var reqLabel in requiredCoupledLabels)
                {
                    if (currentLabels.Contains(reqLabel))
                    {
                        continue;
                    }

                    // بررسی عدم حضور کاربر برای شیفت مقید
                    if (user.UnavailableDates.Any(d => d.Date == date) ||
                        user.UnavailableShiftSlots.Any(s => s.Date.Date == date && s.ShiftLabel == reqLabel))
                    {
                        // کاربر برای شیفت مقید عدم‌حضور دارد؛ شیفت محرک غیرمجاز را حذف می‌کنیم
                        var triggerAssignments = group
                            .Where(a => user.GetRequiredCoupledShift(a.ShiftLabel) == reqLabel)
                            .ToList();

                        foreach (var trig in triggerAssignments)
                        {
                            if (!ApprovedRequestGuard.IsApprovedRequiredSlot(user, trig.Date, trig.ShiftLabel, trig.ShiftId))
                            {
                                solution.UnlockSkeletonAssignment(trig.UserId, trig.ShiftId, trig.Date);
                                solution.RemoveAssignment(trig.UserId, trig.ShiftId, trig.Date, force: true);
                            }
                        }
                        continue;
                    }

                    var shiftReq = constraints.ShiftRequirements.FirstOrDefault(r => r.ShiftLabel == reqLabel);
                    if (shiftReq != null)
                    {
                        solution.AddAssignment(user.UserId, shiftReq.ShiftId, date, reqLabel, isOnCall: false);
                        currentLabels.Add(reqLabel);
                    }
                }
            }
        }
    }

    public static List<string> GetViolations(ShiftSolution solution, ShiftConstraints constraints)
    {
        var violations = new List<string>();

        var usersWithCoupledRules = constraints.UserConstraints
            .Where(u => u.HasCoupledShiftRules)
            .ToList();

        if (usersWithCoupledRules.Count == 0)
        {
            return violations;
        }

        foreach (var user in usersWithCoupledRules)
        {
            var userAssignments = solution.GetUserAllAssignments(user.UserId).ToList();
            var groupedByDate = userAssignments
                .GroupBy(a => a.Date.Date)
                .ToList();

            foreach (var group in groupedByDate)
            {
                var date = group.Key;
                var currentLabels = group.Select(a => a.ShiftLabel).ToHashSet();

                foreach (var assignment in group)
                {
                    var reqLabel = user.GetRequiredCoupledShift(assignment.ShiftLabel);
                    if (reqLabel.HasValue && !currentLabels.Contains(reqLabel.Value))
                    {
                        var triggerNameFa = assignment.ShiftLabel switch
                        {
                            ShiftLabel.Morning => "صبح",
                            ShiftLabel.Evening => "عصر",
                            ShiftLabel.Night => "شب",
                            _ => assignment.ShiftLabel.ToString()
                        };
                        var reqNameFa = reqLabel.Value switch
                        {
                            ShiftLabel.Morning => "صبح",
                            ShiftLabel.Evening => "عصر",
                            ShiftLabel.Night => "شب",
                            _ => reqLabel.Value.ToString()
                        };

                        violations.Add(
                            $"نقض شیفت مقید: کاربر {user.UserId} ({user.UserName}) در تاریخ {date:yyyy-MM-dd} دارای شیفت {triggerNameFa} است و طبق تنظیمات پرسنل، باید شیفت {reqNameFa} همان روز را نیز دریافت کند.");
                    }
                }
            }
        }

        return violations;
    }
}
