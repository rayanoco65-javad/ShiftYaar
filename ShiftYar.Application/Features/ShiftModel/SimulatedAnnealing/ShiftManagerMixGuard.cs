using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using System;
using System.Collections.Generic;
using System.Linq;

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
                        $"Shift manager mix unmet for {shiftReq.ShiftLabel} on {date:yyyy-MM-dd} " +
                        $"(need total≥{requiredTotal}, level1≥{minLevel1}).");
                }
            }
        }

        return warnings;
    }

    private static SimulatedAnnealingScheduler CreateScheduler(ShiftConstraints constraints) =>
        new(constraints, new SimulatedAnnealingParameters
        {
            MaxIterations = 1,
            InitialTemperature = 1,
            FinalTemperature = 0.1
        });
}
