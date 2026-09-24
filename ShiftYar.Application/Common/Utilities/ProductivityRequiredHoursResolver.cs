using ShiftYar.Application.DTOs.ProductivityModel;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using ShiftYar.Domain.Entities.UserModel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ShiftYar.Application.Common.Utilities;

/// <summary>
/// تعیین ساعت موظفی مؤثر کاربر: مقدار دستی «حداکثر ساعت موظفی» یا محاسبهٔ خودکار.
/// </summary>
public static class ProductivityRequiredHoursResolver
{
    public static bool HasManualOverride(User user) =>
        user.MaxProductivityRequiredHours.HasValue && user.MaxProductivityRequiredHours.Value > 0;

    public static void ApplyToUserConstraint(
        User user,
        UserConstraint userConstraint,
        WorkingHoursCalculationResultDto? calculatedSnapshot)
    {
        if (HasManualOverride(user))
        {
            var manualHours = Math.Round(user.MaxProductivityRequiredHours!.Value, MidpointRounding.AwayFromZero);
            userConstraint.IncludedInProductivityPlan = true;
            userConstraint.ProductivityRequiredHours = manualHours;

            if (calculatedSnapshot != null)
            {
                calculatedSnapshot.FinalMonthlyRequiredHours = manualHours;
                calculatedSnapshot.Breakdown ??= new WorkingHoursCalculationBreakdownDto();
                calculatedSnapshot.Breakdown.Notes ??= new List<string>();
                if (!calculatedSnapshot.Breakdown.Notes.Any(n =>
                        n.Contains("حداکثر ساعت موظفی", StringComparison.Ordinal)))
                {
                    calculatedSnapshot.Breakdown.Notes.Add(
                        "ساعت موظفی از فیلد دستی «حداکثر ساعت موظفی» کاربر اعمال شد (به‌جای مقدار محاسبه‌شده).");
                }

                userConstraint.ProductivitySnapshot = calculatedSnapshot;
                return;
            }

            userConstraint.ProductivitySnapshot = new WorkingHoursCalculationResultDto
            {
                BaseMonthlyHours = manualHours,
                TotalDeductions = 0,
                FinalMonthlyRequiredHours = manualHours,
                Breakdown = new WorkingHoursCalculationBreakdownDto
                {
                    Notes = new List<string>
                    {
                        "ساعت موظفی فقط از فیلد دستی «حداکثر ساعت موظفی» کاربر تنظیم شده است."
                    }
                }
            };
            return;
        }

        if (calculatedSnapshot == null)
        {
            return;
        }

        userConstraint.IncludedInProductivityPlan = calculatedSnapshot.Breakdown?.IsIncludedInProductivityPlan ?? (user.IncludedProductivityPlan != false);
        userConstraint.ProductivitySnapshot = calculatedSnapshot;
        userConstraint.ProductivityRequiredHours = Math.Round(calculatedSnapshot.FinalMonthlyRequiredHours, MidpointRounding.AwayFromZero);
    }
}
