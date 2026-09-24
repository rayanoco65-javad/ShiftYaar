using ShiftYar.Application.Features.ShiftModel.Services;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.Common.Utilities;

/// <summary>
/// تعیین شیفت‌های مجاز: از فیلد صریح <see cref="UserShiftPermission"/> یا (سازگاری عقب‌رو) ShiftType/SubType.
/// </summary>
public static class ShiftEligibilityResolver
{
    public static UserShiftPermission AllPermissions =>
        UserShiftPermission.Morning
        | UserShiftPermission.Evening
        | UserShiftPermission.Night
        | UserShiftPermission.MorningEveningSameDay
        | UserShiftPermission.MorningNightSameDay;

    public static UserShiftPermission ResolvePermissions(
        UserShiftPermission? explicitPermissions,
        ShiftTypes shiftType,
        ShiftSubTypes shiftSubType,
        TwoShiftRotationPattern? twoShiftPattern) =>
        explicitPermissions ?? MapLegacyToPermissions(shiftType, shiftSubType, twoShiftPattern);

    public static UserShiftPermission MapLegacyToPermissions(
        ShiftTypes shiftType,
        ShiftSubTypes shiftSubType,
        TwoShiftRotationPattern? twoShiftPattern)
    {
        if (shiftType == ShiftTypes.FixedShift)
        {
            return shiftSubType switch
            {
                ShiftSubTypes.FixedNight => UserShiftPermission.Night,
                ShiftSubTypes.FixedEvening => UserShiftPermission.Evening,
                _ => UserShiftPermission.Morning
            };
        }

        return shiftSubType switch
        {
            ShiftSubTypes.ThreeShifts => AllPermissions,
            ShiftSubTypes.TwoShifts => twoShiftPattern switch
            {
                TwoShiftRotationPattern.MorningNight =>
                    UserShiftPermission.Morning
                    | UserShiftPermission.Night
                    | UserShiftPermission.MorningNightSameDay,
                TwoShiftRotationPattern.EveningNight =>
                    UserShiftPermission.Evening | UserShiftPermission.Night,
                _ => UserShiftPermission.Morning
                     | UserShiftPermission.Evening
                     | UserShiftPermission.MorningEveningSameDay
            },
            ShiftSubTypes.FixedNight => UserShiftPermission.Night,
            ShiftSubTypes.FixedEvening => UserShiftPermission.Evening,
            ShiftSubTypes.FixedMorning => UserShiftPermission.Morning,
            _ => AllPermissions
        };
    }

    public static UserShiftPermission NormalizePermissions(UserShiftPermission value)
    {
        if (value == UserShiftPermission.None)
        {
            return value;
        }

        if (value.HasFlag(UserShiftPermission.MorningEveningSameDay))
        {
            value |= UserShiftPermission.Morning | UserShiftPermission.Evening;
        }

        if (value.HasFlag(UserShiftPermission.MorningNightSameDay))
        {
            value |= UserShiftPermission.Morning;
        }

        return value;
    }

    public static string? ValidatePermissions(UserShiftPermission? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        var normalized = NormalizePermissions(value.Value);
        if (normalized == UserShiftPermission.None)
        {
            return "حداقل یک نوع شیفت مجاز باید انتخاب شود.";
        }

        if (((int)normalized & ~(int)AllPermissions) != 0)
        {
            return "مقدار مجوز شیفت نامعتبر است.";
        }

        return null;
    }

    public static IReadOnlyList<ShiftLabel> GetAllowedLabels(
        ShiftTypes shiftType,
        ShiftSubTypes shiftSubType,
        TwoShiftRotationPattern? twoShiftPattern) =>
        GetStandaloneLabels(MapLegacyToPermissions(shiftType, shiftSubType, twoShiftPattern));

    public static IReadOnlyList<ShiftLabel> GetStandaloneLabels(UserShiftPermission permissions)
    {
        var list = new List<ShiftLabel>(3);
        if (permissions.HasFlag(UserShiftPermission.Morning))
        {
            list.Add(ShiftLabel.Morning);
        }

        if (permissions.HasFlag(UserShiftPermission.Evening))
        {
            list.Add(ShiftLabel.Evening);
        }

        if (permissions.HasFlag(UserShiftPermission.Night))
        {
            list.Add(ShiftLabel.Night);
        }

        return list;
    }

    public static void ApplyPermissionsToUserConstraint(
        UserConstraint user,
        UserShiftPermission? explicitPermissions)
    {
        user.AllowedShiftPermissions = ResolvePermissions(
            explicitPermissions,
            user.ShiftType,
            user.ShiftSubType,
            user.TwoShiftRotationPattern);
        user.AllowedShiftLabels = GetStandaloneLabels(user.AllowedShiftPermissions).ToList();
    }

    public static bool UsesPermissionModel(UserConstraint user) =>
        user.AllowedShiftPermissions != UserShiftPermission.None;

    public static bool IsLabelAllowed(
        IReadOnlyCollection<ShiftLabel>? allowedLabels,
        ShiftLabel label)
    {
        if (allowedLabels == null || allowedLabels.Count == 0)
        {
            return true;
        }

        return allowedLabels.Contains(label);
    }

    public static bool MayEverTakeLabel(UserConstraint user, ShiftLabel label)
    {
        // درخواست شیفت تأییدشده صریح بر محدودیت‌های مجوز اولیه اولویت دارد
        if (user.RequiredShiftSlots.Any(s => s.ShiftLabel == label))
        {
            return true;
        }

        if (UsesPermissionModel(user))
        {
            if (HasSinglePermission(user.AllowedShiftPermissions, label))
            {
                return true;
            }

            return label == ShiftLabel.Night
                   && user.AllowedShiftPermissions.HasFlag(UserShiftPermission.MorningNightSameDay);
        }

        return IsLabelAllowed(user.AllowedShiftLabels, label);
    }

    /// <summary>
    /// بررسی می‌کند که آیا کاربر مجوز ذاتی (بدون نیاز به درخواست تأییدشده) برای این نوع شیفت دارد.
    /// برخلاف <see cref="MayEverTakeLabel"/>، درخواست‌های تأییدشده را در نظر نمی‌گیرد.
    /// برای فیلتر کاندیداها در گاردهایی که شیفت routine (غیر ON) اضافه می‌کنند استفاده شود.
    /// </summary>
    public static bool HasInherentPermission(UserConstraint user, ShiftLabel label)
    {
        if (UsesPermissionModel(user))
        {
            if (HasSinglePermission(user.AllowedShiftPermissions, label))
            {
                return true;
            }

            return label == ShiftLabel.Night
                   && user.AllowedShiftPermissions.HasFlag(UserShiftPermission.MorningNightSameDay);
        }

        return IsLabelAllowed(user.AllowedShiftLabels, label);
    }

    /// <summary>
    /// بررسی می‌کند آیا کاربر می‌تواند این نوع شیفت را روی یک تاریخ خاص بگیرد:
    /// یا مجوز ذاتی دارد، یا درخواست تأییدشده‌ای دقیقاً برای همان تاریخ دارد.
    /// </summary>
    public static bool MayTakeLabelOnDate(UserConstraint user, ShiftLabel label, DateTime date, DateTime? rangeStartDate = null)
    {
        // درخواست تأییدشده دقیقاً برای همین تاریخ
        if (user.RequiredShiftSlots.Any(s => s.ShiftLabel == label && s.Date.Date == date.Date)
            || user.RequiredPresenceDates.Any(d => d.Date == date.Date))
        {
            return true;
        }

        // بررسی قانون تناوب هفتگی شیفت‌های روزانه (صبح و عصر)
        if (user.IsWeeklyAlternatingActive && user.FirstWeekShiftLabel.HasValue &&
            (label == ShiftLabel.Morning || label == ShiftLabel.Evening))
        {
            var refStart = rangeStartDate ?? WeeklyAlternatingShiftService.GetPersianMonthStart(date);
            var allowed = WeeklyAlternatingShiftService.GetAllowedDayShiftForDate(date, refStart, user.FirstWeekShiftLabel.Value);
            if (label != allowed)
            {
                return false;
            }
        }

        return HasInherentPermission(user, label);
    }

    public static bool SupportsMorningEveningCombo(UserConstraint user) =>
        UsesPermissionModel(user)
            ? user.AllowedShiftPermissions.HasFlag(UserShiftPermission.MorningEveningSameDay)
            : IsLabelAllowed(user.AllowedShiftLabels, ShiftLabel.Morning)
              && IsLabelAllowed(user.AllowedShiftLabels, ShiftLabel.Evening);

    public static bool SupportsMorningNightCombo(UserConstraint user) =>
        UsesPermissionModel(user)
            ? user.AllowedShiftPermissions.HasFlag(UserShiftPermission.MorningNightSameDay)
            : user.ShiftSubType == ShiftSubTypes.TwoShifts
              && user.TwoShiftRotationPattern == TwoShiftRotationPattern.MorningNight;

    /// <summary>
    /// بررسی مجاز بودن انتساب شیفت با در نظر گرفتن تاریخ مشخص.
    /// اگر <paramref name="date"/> داده شود، درخواست تأییدشده فقط برای همان تاریخ bypass انجام می‌دهد.
    /// اگر <paramref name="date"/> داده نشود (null)، رفتار قدیمی (بدون بررسی تاریخ) حفظ می‌شود.
    /// </summary>
    public static bool IsAssignmentAllowed(
        UserConstraint user,
        IEnumerable<ShiftLabel> existingOnDay,
        ShiftLabel newLabel,
        int maxShiftsPerDay,
        bool forbidDuplicateLabels = true,
        DateTime? date = null,
        DateTime? rangeStartDate = null)
    {
        // درخواست شیفت تأییدشده صریح بر مجوزهای اولیه اولویت دارد
        // اگر تاریخ داده شده، فقط درخواست‌های همان روز را در نظر می‌گیریم
        bool hasApprovedSlotForLabel = date.HasValue
            ? user.RequiredShiftSlots.Any(s => s.ShiftLabel == newLabel && s.Date.Date == date.Value.Date)
            : user.RequiredShiftSlots.Any(s => s.ShiftLabel == newLabel);

        if (hasApprovedSlotForLabel)
        {
            return DailyAssignmentRules.CanAddShift(
                existingOnDay, newLabel, maxShiftsPerDay, forbidDuplicateLabels);
        }

        // بررسی قانون تناوب هفتگی شیفت‌های روزانه (صبح و عصر)
        if (user.IsWeeklyAlternatingActive && user.FirstWeekShiftLabel.HasValue && date.HasValue &&
            (newLabel == ShiftLabel.Morning || newLabel == ShiftLabel.Evening))
        {
            var refStart = rangeStartDate ?? WeeklyAlternatingShiftService.GetPersianMonthStart(date.Value);
            var allowed = WeeklyAlternatingShiftService.GetAllowedDayShiftForDate(date.Value, refStart, user.FirstWeekShiftLabel.Value);
            if (newLabel != allowed)
            {
                return false;
            }
        }

        if (!UsesPermissionModel(user))
        {
            if (!IsLabelAllowed(user.AllowedShiftLabels, newLabel))
            {
                return false;
            }

            return DailyAssignmentRules.CanAddShift(
                existingOnDay, newLabel, maxShiftsPerDay, forbidDuplicateLabels);
        }

        return CanAssignShift(
            user.AllowedShiftPermissions,
            existingOnDay,
            newLabel,
            maxShiftsPerDay,
            forbidDuplicateLabels);
    }

    public static bool CanAssignShift(
        UserShiftPermission permissions,
        IEnumerable<ShiftLabel> existingOnDay,
        ShiftLabel newLabel,
        int maxShiftsPerDay,
        bool forbidDuplicateLabels = true)
    {
        var existing = existingOnDay.ToList();
        if (!DailyAssignmentRules.CanAddShift(existing, newLabel, maxShiftsPerDay, forbidDuplicateLabels))
        {
            return false;
        }

        var trial = existing.Append(newLabel).Distinct().OrderBy(x => x).ToList();
        return IsValidDaySetForPermissions(permissions, trial);
    }

    public static bool IsValidDaySetForPermissions(
        UserShiftPermission permissions,
        IReadOnlyList<ShiftLabel> labelsOnDay)
    {
        switch (labelsOnDay.Count)
        {
            case 0:
                return true;
            case 1:
                return HasSinglePermission(permissions, labelsOnDay[0]);
            case 2:
                if (labelsOnDay.Contains(ShiftLabel.Morning) && labelsOnDay.Contains(ShiftLabel.Evening))
                {
                    return permissions.HasFlag(UserShiftPermission.MorningEveningSameDay)
                           && HasSinglePermission(permissions, ShiftLabel.Morning)
                           && HasSinglePermission(permissions, ShiftLabel.Evening);
                }

                if (labelsOnDay.Contains(ShiftLabel.Morning) && labelsOnDay.Contains(ShiftLabel.Night))
                {
                    return permissions.HasFlag(UserShiftPermission.MorningNightSameDay)
                           && HasSinglePermission(permissions, ShiftLabel.Morning);
                }

                return false;
            default:
                return false;
        }
    }

    private static bool HasSinglePermission(UserShiftPermission permissions, ShiftLabel label) =>
        label switch
        {
            ShiftLabel.Morning => permissions.HasFlag(UserShiftPermission.Morning),
            ShiftLabel.Evening => permissions.HasFlag(UserShiftPermission.Evening),
            ShiftLabel.Night => permissions.HasFlag(UserShiftPermission.Night),
            _ => false
        };
}
