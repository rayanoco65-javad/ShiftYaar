namespace ShiftYar.Application.Common.Utilities;

/// <summary>
/// خلاصهٔ شرایط دپارتمان برای پیشنهاد تنظیمات بهینهٔ زمان‌بندی.
/// </summary>
public sealed class DepartmentSchedulingProfile
{
    public int DepartmentId { get; init; }

    public bool? IsNightLover { get; init; }

    public int ActiveUserCount { get; init; }

    public int RotatingUserCount { get; init; }

    public int ThreeShiftRotatingUserCount { get; init; }

    public int TwoShiftRotatingUserCount { get; init; }

    public int ShiftManagerCount { get; init; }

    public bool HasMixedGenderStaff { get; init; }

    public bool HasNightShift { get; init; }

    public int NightHeadcountPerShift { get; init; }

    public bool AllowsSameDayMultiShift { get; init; }
}
