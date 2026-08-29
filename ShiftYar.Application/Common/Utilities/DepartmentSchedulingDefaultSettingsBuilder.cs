using ShiftYar.Application.DTOs.DepartmentModel;

namespace ShiftYar.Application.Common.Utilities;

/// <summary>
/// ساخت تنظیمات پیش‌فرض بهینهٔ زمان‌بندی بر اساس پروفایل دپارتمان.
/// </summary>
public static class DepartmentSchedulingDefaultSettingsBuilder
{
    public static DepartmentSchedulingSettingsDtoAdd Build(DepartmentSchedulingProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var maxShiftsPerDay = ResolveMaxShiftsPerDay(profile);
        var maxShiftsPerWeek = ResolveMaxShiftsPerWeek(profile);
        var maxConsecutiveShifts = 2;
        var nightDistributionType = ResolveNightDistributionType(profile);

        return new DepartmentSchedulingSettingsDtoAdd
        {
            DepartmentId = profile.DepartmentId,

            ForbidDuplicateDailyAssignments = true,
            EnforceMaxShiftsPerDay = true,
            EnforceMinRestDays = profile.RotatingUserCount > 0,
            EnforceMaxConsecutiveShifts = profile.RotatingUserCount > 0,
            EnforceWeeklyMaxShifts = profile.RotatingUserCount > 0,
            EnforceNightShiftMonthlyCap = false,
            EnforceSpecialtyCapacity = true,

            MinRestDaysBetweenShifts = profile.RotatingUserCount > 0 ? 1 : 0,
            MaxConsecutiveShifts = maxConsecutiveShifts,
            MaxShiftsPerWeek = maxShiftsPerWeek,
            MaxNightShiftsPerMonth = null,
            MaxShiftsPerDay = maxShiftsPerDay,
            MaxConsecutiveNightShifts = profile.HasNightShift ? 1 : null,

            AllowEveningAfterNightShift = false,
            AllowNightShiftAfterNightShift = false,

            GenderBalanceWeight = profile.HasMixedGenderStaff ? 1.0 : 0.5,
            SpecialtyPreferenceWeight = 1.5,
            UserUnwantedShiftWeight = 2.0,
            UserPreferredShiftWeight = 2.5,
            WeeklyMaxWeight = profile.RotatingUserCount > 0 ? 1.5 : 0.0,
            MonthlyNightCapWeight = profile.HasNightShift ? 1.0 : 0.0,
            FairShiftCountBalanceWeight = 3.0,
            ExtraShiftRotationWeight = 1.5,
            ShiftLabelBalanceWeight = 2.0,
            FairnessLookbackMonths = 6,

            EnforceMinimumShiftsForRotatingStaff = false,
            MinMorningShiftsForThreeShiftRotation = null,
            MinEveningShiftsForThreeShiftRotation = null,
            MinNightShiftsForThreeShiftRotation = null,
            MinFirstShiftForTwoShiftRotation = null,
            MinSecondShiftForTwoShiftRotation = null,

            EnableNightShiftPreference = false,
            NightShiftPreferenceType = nightDistributionType,
            NightShiftPreferenceWeight = 0.0,

            ShiftManagerRequirementWeight = 0.0,

            EnableMorningShiftDistributionBySeniority = false,
            MorningShiftDistributionType = 2,
            MorningShiftDistributionWeight = 0.0,

            EnableEveningShiftDistributionBySeniority = false,
            EveningShiftDistributionType = 2,
            EveningShiftDistributionWeight = 0.0,

            EnableNightShiftDistributionBySeniority = false,
            NightShiftDistributionType = nightDistributionType,
            NightShiftDistributionWeight = 0.0,
            SeniorityDistributionSlope = 1.0,

            AllowCurrentMonthScheduling = false,
            AllowMonthlyRescheduleWithAutoDelete = false
        };
    }

    private static int ResolveMaxShiftsPerDay(DepartmentSchedulingProfile profile) =>
        profile.AllowsSameDayMultiShift ? 2 : 1;

    private static int ResolveMaxShiftsPerWeek(DepartmentSchedulingProfile profile)
    {
        if (profile.RotatingUserCount <= 0)
        {
            return 5;
        }

        if (profile.ActiveUserCount < 8)
        {
            return 5;
        }

        if (profile.ActiveUserCount <= 15)
        {
            return 6;
        }

        return 6;
    }

    private static int ResolveNightDistributionType(DepartmentSchedulingProfile profile)
    {
        if (profile.IsNightLover == true)
        {
            return 0;
        }

        if (profile.IsNightLover == false)
        {
            return 1;
        }

        return 2;
    }
}
