using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;
namespace ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;

/// <summary>
/// الزام سخت ترکیب مسئول شیفت (Evening/Night). پس از گاردهای سهمیه شب و ON دوباره اعمال می‌شود
/// تا جابجایی‌های سهمیه شب ترکیب مسئول را نشکند.
/// </summary>
public static class ShiftManagerMixGuard
{
    public static void Enforce(ShiftSolution solution, ShiftConstraints constraints)
    {
        var scheduler = CreateScheduler(constraints);
        scheduler.EnforceShiftManagerMix(solution);
    }

    /// <summary>
    /// حلقه تثبیت: سهمیه شب ↔ ترمیم مسئول تا هر دو پایدار شوند.
    /// </summary>
    public static void StabilizeWithNightQuotas(ShiftSolution solution, ShiftConstraints constraints)
    {
        var scheduler = CreateScheduler(constraints);
        scheduler.StabilizeManagerMixAndNightQuotas(solution);
    }

    public static List<string> GetViolations(ShiftSolution solution, ShiftConstraints constraints)
    {
        var warnings = new List<string>();

        for (var date = constraints.StartDate.Date; date <= constraints.EndDate.Date; date = date.AddDays(1))
        {
            foreach (var shiftReq in constraints.ShiftRequirements)
            {
                var (requiredTotal, minLevel1) = ShiftManagerRules.GetRequirement(shiftReq);
                if (requiredTotal <= 0)
                {
                    continue;
                }

                var assignees = solution.GetShiftAssignments(shiftReq.ShiftId, date)
                    .Where(a => !a.IsOnCall)
                    .Select(a => constraints.UserConstraints.FirstOrDefault(u => u.UserId == a.UserId))
                    .Where(u => u != null)
                    .Cast<UserConstraint>()
                    .ToList();

                if (assignees.Count == 0)
                {
                    continue;
                }

                if (!ShiftManagerRules.IsSatisfied(assignees, requiredTotal, minLevel1))
                {
                    warnings.Add(
                        FormatViolation(shiftReq.ShiftLabel, date, requiredTotal, minLevel1));
                }
            }
        }

        return warnings;
    }

    public static string FormatViolation(
        ShiftLabel shiftLabel,
        DateTime date,
        int requiredTotal,
        int minLevel1) =>
        $"Shift manager mix unmet for {shiftLabel} on {date:yyyy-MM-dd} " +
        $"(need total≥{requiredTotal}, level1≥{minLevel1}).";

    /// <summary>
    /// اگر ترکیب مسئول عصر/شب برقرار نباشد، با پیام فارسی fail-fast.
    /// </summary>
    public static void EnsureOrThrow(ShiftSolution solution, ShiftConstraints constraints)
    {
        var violations = GetViolations(solution, constraints);
        if (violations.Count == 0)
        {
            return;
        }

        var persianLines = violations.Select(TranslateViolationToPersian).ToList();
        throw new InvalidOperationException(
            "ترکیب مسئول شیفت (عصر/شب) برآورده نشد. شیفت‌بندی با ترکیب ناقص ذخیره نمی‌شود:\n"
            + string.Join("\n", persianLines));
    }

    private static string TranslateViolationToPersian(string english)
    {
        // "Shift manager mix unmet for Night on 2026-08-23 (need total≥2, level1≥1)."
        if (!english.Contains("Shift manager mix unmet", StringComparison.OrdinalIgnoreCase))
        {
            return english;
        }

        var label = english.Contains("Night", StringComparison.OrdinalIgnoreCase) ? "شب"
            : english.Contains("Evening", StringComparison.OrdinalIgnoreCase) ? "عصر" : "شیفت";
        var dateStart = english.IndexOf("on ", StringComparison.Ordinal);
        var datePart = dateStart >= 0 && english.Length >= dateStart + 13
            ? english.Substring(dateStart + 3, 10)
            : "?";
        var needTotal = "۲";
        var needL1 = "۱";
        var totalIdx = english.IndexOf("total≥", StringComparison.Ordinal);
        if (totalIdx >= 0)
        {
            var endIdx = english.IndexOf(',', totalIdx);
            if (endIdx > totalIdx)
            {
                needTotal = english.Substring(totalIdx + 6, endIdx - totalIdx - 6).Trim();
            }
        }

        var l1Idx = english.IndexOf("level1≥", StringComparison.Ordinal);
        if (l1Idx >= 0)
        {
            var endIdx = english.IndexOf(')', l1Idx);
            if (endIdx > l1Idx)
            {
                needL1 = english.Substring(l1Idx + 7, endIdx - l1Idx - 7).Trim();
            }
        }

        return $"• ترکیب مسئول {label} در تاریخ {datePart}: نیاز به حداقل {needTotal} مسئول شامل {needL1} مسئول سطح‌۱.";
    }

    private static SimulatedAnnealingScheduler CreateScheduler(ShiftConstraints constraints) =>
        new(constraints, new SimulatedAnnealingParameters
        {
            MaxIterations = 1,
            InitialTemperature = 1,
            FinalTemperature = 0.1
        });
}
