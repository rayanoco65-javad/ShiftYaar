using System;
using System.Collections.Generic;
using System.Linq;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.Common.Utilities;

/// <summary>
/// تعیین شیفت‌های مجاز بر اساس نوع شیفت کاربر (فیکس / گردشی دو یا سه نوبته).
/// </summary>
public static class ShiftEligibilityResolver
{
    private static readonly ShiftLabel[] AllLabels =
    {
        ShiftLabel.Morning,
        ShiftLabel.Evening,
        ShiftLabel.Night
    };

    public static IReadOnlyList<ShiftLabel> GetAllowedLabels(
        ShiftTypes shiftType,
        ShiftSubTypes shiftSubType,
        TwoShiftRotationPattern? twoShiftPattern)
    {
        if (shiftType == ShiftTypes.FixedShift)
        {
            return shiftSubType switch
            {
                ShiftSubTypes.FixedEvening => new[] { ShiftLabel.Evening },
                // FixedMorning و هر مقدار نامعتبر برای فیکس → فقط صبح
                _ => new[] { ShiftLabel.Morning }
            };
        }

        // RotatingShift
        return shiftSubType switch
        {
            ShiftSubTypes.TwoShifts => ResolveTwoShiftLabels(twoShiftPattern),
            ShiftSubTypes.ThreeShifts => AllLabels,
            // زیرنوع فیکس روی کاربر گردشی: محافظه‌کارانه همان فیکس
            ShiftSubTypes.FixedEvening => new[] { ShiftLabel.Evening },
            ShiftSubTypes.FixedMorning => new[] { ShiftLabel.Morning },
            _ => AllLabels
        };
    }

    public static bool IsLabelAllowed(
        IReadOnlyCollection<ShiftLabel>? allowedLabels,
        ShiftLabel label)
    {
        // لیست خالی = سازگاری عقب‌رو (تست‌های قدیمی / داده ناقص) → همه مجاز
        if (allowedLabels == null || allowedLabels.Count == 0)
        {
            return true;
        }

        return allowedLabels.Contains(label);
    }

    private static IReadOnlyList<ShiftLabel> ResolveTwoShiftLabels(TwoShiftRotationPattern? pattern)
    {
        return pattern switch
        {
            TwoShiftRotationPattern.MorningNight => new[] { ShiftLabel.Morning, ShiftLabel.Night },
            TwoShiftRotationPattern.EveningNight => new[] { ShiftLabel.Evening, ShiftLabel.Night },
            // پیش‌فرض و MorningEvening
            _ => new[] { ShiftLabel.Morning, ShiftLabel.Evening }
        };
    }
}
