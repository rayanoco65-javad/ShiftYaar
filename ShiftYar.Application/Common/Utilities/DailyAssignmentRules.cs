using System.Collections.Generic;
using System.Linq;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.Common.Utilities;

/// <summary>
/// قوانین ترکیب شیفت در یک روز:
/// - صبح+عصر مجاز است
/// - شب فقط به‌تنهایی (بدون صبح/عصر همان روز)
/// - تکرار همان نوع شیفت در یک روز ممنوع
/// </summary>
public static class DailyAssignmentRules
{
    public static bool CanAddShift(
        IEnumerable<ShiftLabel> existingLabels,
        ShiftLabel newLabel,
        int maxShiftsPerDay,
        bool forbidDuplicateLabels = true)
    {
        var existing = existingLabels.ToList();
        if (forbidDuplicateLabels && existing.Contains(newLabel))
        {
            return false;
        }

        if (maxShiftsPerDay > 0 && existing.Count >= maxShiftsPerDay)
        {
            return false;
        }

        // شب با هیچ شیفت دیگری در همان روز ترکیب نمی‌شود
        if (newLabel == ShiftLabel.Night && existing.Count > 0)
        {
            return false;
        }

        if (existing.Contains(ShiftLabel.Night))
        {
            return false;
        }

        return true;
    }

    public static bool IsValidDaySet(IEnumerable<ShiftLabel> labels, int maxShiftsPerDay, bool forbidDuplicateLabels = true)
    {
        var list = labels.ToList();
        if (maxShiftsPerDay > 0 && list.Count > maxShiftsPerDay)
        {
            return false;
        }

        if (forbidDuplicateLabels && list.Count != list.Distinct().Count())
        {
            return false;
        }

        if (list.Contains(ShiftLabel.Night) && list.Count > 1)
        {
            return false;
        }

        return true;
    }
}
