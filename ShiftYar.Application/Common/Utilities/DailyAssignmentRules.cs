using System.Collections.Generic;
using System.Linq;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.Common.Utilities;

/// <summary>
/// قوانین ترکیب شیفت در یک روز:
/// - صبح+عصر مجاز است
/// - صبح+شب مجاز است (عصر بین آن‌ها فاصله زمانی است؛ متوالی نیستند)
/// - عصر+شب ممنوع است (متوالی و بیش از ۱۲ ساعت)
/// - صبح+عصر+شب ممنوع است (شامل عصر+شب)
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

        return IsValidDaySet(existing.Append(newLabel), maxShiftsPerDay <= 0 ? existing.Count + 1 : maxShiftsPerDay, forbidDuplicateLabels);
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

        // عصر و شب متوالی‌اند و بیش از ۱۲ ساعت می‌شوند
        if (list.Contains(ShiftLabel.Evening) && list.Contains(ShiftLabel.Night))
        {
            return false;
        }

        return true;
    }
}
