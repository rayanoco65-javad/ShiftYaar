using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using System.Collections.Generic;
using System.Linq;

namespace ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;

/// <summary>
/// اولویت پر کردن ساعت موظفی: پرسنل غیرطرحی قبل از پرسنل طرحی.
/// پرسنل طرحی فقط تا سقف موظفی شیفت می‌گیرند (بدون اضافه‌کار).
/// </summary>
public static class ProjectPersonnelProductivityPriority
{
    /// <summary>آستانه کسری/مازاد برای اولویت غیرطرحی نسبت به طرحی (دقیق‌تر از تعادل هم‌رده).</summary>
    public const double CrossTierToleranceHours = 0.25;

    public static bool IsProjectPersonnel(UserConstraint user) => user.IsProjectPersonnel == true;

    /// <summary>۰ = غیرطرحی (اول)، ۱ = طرحی (بعد)</summary>
    public static int FillTier(UserConstraint user) => IsProjectPersonnel(user) ? 1 : 0;

    /// <summary>
    /// سقف ساعات مؤثر در شیفت‌بندی: طرحی = فقط موظفی؛ غیرطرحی = موظفی + اضافه‌کار مجاز.
    /// </summary>
    public static double GetMaxAllowedSchedulingHours(UserConstraint user)
    {
        if (!user.ProductivityRequiredHours.HasValue)
        {
            return double.MaxValue;
        }

        if (IsProjectPersonnel(user))
        {
            return (double)user.ProductivityRequiredHours.Value;
        }

        return ProductivityWorkedHoursCalculator.GetMaxAllowedHours(
            user.ProductivityRequiredHours,
            user.OvertimeConsent,
            user.MaxMonthlyOvertimeHours);
    }

    public static bool WouldExceedSchedulingCap(UserConstraint user, double projectedWorkedHours, double tolerance = 0.25) =>
        projectedWorkedHours > GetMaxAllowedSchedulingHours(user) + tolerance;

    public static bool IsAtOrAboveRequiredHours(UserConstraint user, double workedHours, double tolerance = 0.25)
    {
        if (!user.ProductivityRequiredHours.HasValue)
        {
            return false;
        }

        return workedHours >= (double)user.ProductivityRequiredHours.Value - tolerance;
    }

    public static IEnumerable<UserConstraint> SplitNonProjectFirst(IEnumerable<UserConstraint> users)    {
        var list = users as IList<UserConstraint> ?? users.ToList();
        foreach (var user in list.Where(u => !IsProjectPersonnel(u)))
        {
            yield return user;
        }

        foreach (var user in list.Where(IsProjectPersonnel))
        {
            yield return user;
        }
    }
}
