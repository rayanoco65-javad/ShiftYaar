using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using ShiftYar.Domain.Entities.DepartmentModel;

namespace ShiftYar.Application.Common.Utilities;

/// <summary>
/// هم‌تراز کردن قواعد استراحت بعد از شب با تنظیمات دپارتمان.
/// </summary>
public static class DepartmentPostNightShiftRulesApplier
{
    public static void Apply(
        ShiftConstraints constraints,
        DepartmentSchedulingSettings settings)
    {
        ArgumentNullException.ThrowIfNull(constraints);
        ArgumentNullException.ThrowIfNull(settings);

        constraints.HardRules.AllowEveningAfterNightShift = settings.AllowEveningAfterNightShift;
        constraints.HardRules.AllowNightShiftAfterNightShift = settings.AllowNightShiftAfterNightShift;

        if (settings.MaxConsecutiveNightShifts.HasValue && settings.MaxConsecutiveNightShifts.Value > 0)
        {
            constraints.GlobalConstraints.MaxConsecutiveNightShifts =
                Math.Max(1, settings.MaxConsecutiveNightShifts.Value);
        }

        if (constraints.HardRules.AllowNightShiftAfterNightShift)
        {
            constraints.GlobalConstraints.AllowConsecutiveNightShifts = true;

            if (constraints.GlobalConstraints.MaxConsecutiveNightShifts < 2)
            {
                constraints.GlobalConstraints.MaxConsecutiveNightShifts = 2;
            }

            // فاصلهٔ اجباری بین شب‌های کاربران باید با این فلگ هم‌خوان باشد
            foreach (var user in constraints.UserConstraints)
            {
                user.MinDaysBetweenNightShifts = 0;
            }
        }
    }
}
