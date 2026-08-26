using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.Common.Utilities;

/// <summary>
/// محاسبه ساعات مؤثر کار برای موظفی/عملکرد.
/// اولویت با چهار فیلد قابل‌تنظیم سوپروایزر روی شیفت است؛ در نبود آن‌ها از مدت Start/End و ضریب شب/تعطیل استفاده می‌شود.
/// </summary>
public static class ProductivityWorkedHoursCalculator
{
    public const double DefaultNightHolidayMultiplier = 1.5;
    public const double DefaultHandoverHours = 1.0;
    public const double DefaultMaxConsecutiveWorkHours = 12.0;
    public const double DefaultMaxMonthlyOvertimeHours = 80.0;

    public sealed record ShiftWorkInfo(
        int ShiftId,
        ShiftLabel Label,
        double DurationHours,
        TimeSpan StartTime,
        TimeSpan EndTime,
        double? WeekdayNonProductivityHours = null,
        double? HolidayNonProductivityHours = null,
        double? WeekdayProductivityPlanHours = null,
        double? HolidayProductivityPlanHours = null);

    /// <summary>
    /// ساعات محاسبه‌شده برای یک نوبت (بدون تحویل‌کار)، بر اساس تنظیمات شیفت و وضعیت کاربر/روز.
    /// </summary>
    public static double ResolveCreditedHours(
        ShiftWorkInfo info,
        bool isHoliday,
        bool includedInProductivityPlan,
        double nightHolidayMultiplier = DefaultNightHolidayMultiplier)
    {
        double? configured = (includedInProductivityPlan, isHoliday) switch
        {
            (true, true) => info.HolidayProductivityPlanHours,
            (true, false) => info.WeekdayProductivityPlanHours,
            (false, true) => info.HolidayNonProductivityHours,
            (false, false) => info.WeekdayNonProductivityHours
        };

        if (configured is > 0)
        {
            return configured.Value;
        }

        var clockHours = info.DurationHours > 0 ? info.DurationHours : 8.0;
        var applyMultiplier = isHoliday || info.Label == ShiftLabel.Night;
        return applyMultiplier ? clockHours * nightHolidayMultiplier : clockHours;
    }

    /// <summary>
    /// تخمین ساعات یک انتساب شامل تحویل پیش‌فرض (برای گاردهای جابه‌جایی).
    /// </summary>
    public static double EstimateAssignmentHours(
        SaShiftAssignment assignment,
        IReadOnlyDictionary<int, ShiftWorkInfo> shiftInfoById,
        bool isHoliday,
        bool includedInProductivityPlan,
        double handoverHours = DefaultHandoverHours,
        double nightHolidayMultiplier = DefaultNightHolidayMultiplier)
    {
        if (!shiftInfoById.TryGetValue(assignment.ShiftId, out var info))
        {
            info = new ShiftWorkInfo(
                assignment.ShiftId,
                assignment.ShiftLabel,
                8,
                TimeSpan.Zero,
                TimeSpan.Zero);
        }

        return handoverHours + ResolveCreditedHours(
            info, isHoliday, includedInProductivityPlan, nightHolidayMultiplier);
    }

    public static double CalculateEffectiveWorkedHours(
        IEnumerable<SaShiftAssignment> assignments,
        IReadOnlyDictionary<int, ShiftWorkInfo> shiftInfoById,
        Func<DateTime, bool> isHoliday,
        Func<int, bool>? isIncludedInProductivityPlan = null,
        double nightHolidayMultiplier = DefaultNightHolidayMultiplier,
        double handoverHours = DefaultHandoverHours)
    {
        var ordered = OrderAssignments(assignments).Where(a => !a.IsOnCall).ToList();
        if (ordered.Count == 0)
        {
            return 0;
        }

        double total = 0;
        for (var i = 0; i < ordered.Count; i++)
        {
            var assignment = ordered[i];
            if (!shiftInfoById.TryGetValue(assignment.ShiftId, out var info))
            {
                info = new ShiftWorkInfo(
                    assignment.ShiftId,
                    assignment.ShiftLabel,
                    8,
                    DefaultStartForLabel(assignment.ShiftLabel),
                    TimeSpan.Zero);
            }

            var holiday = isHoliday(assignment.Date.Date);
            var inPlan = isIncludedInProductivityPlan?.Invoke(assignment.UserId) == true;
            var credited = ResolveCreditedHours(info, holiday, inPlan, nightHolidayMultiplier);
            var handover = i > 0 ? handoverHours : 0;
            total += credited + handover;
        }

        return total;
    }

    public static double GetMaxAllowedHours(
        decimal? requiredHours,
        bool overtimeConsent,
        double maxMonthlyOvertimeHours = DefaultMaxMonthlyOvertimeHours)
    {
        if (!requiredHours.HasValue)
        {
            return double.MaxValue;
        }

        var baseHours = (double)requiredHours.Value;
        return overtimeConsent ? baseHours + maxMonthlyOvertimeHours : baseHours;
    }

    public static bool ExceedsMaxConsecutiveWorkHours(
        IEnumerable<SaShiftAssignment> assignments,
        IReadOnlyDictionary<int, ShiftWorkInfo> shiftInfoById,
        double maxConsecutiveHours = DefaultMaxConsecutiveWorkHours,
        double handoverHours = DefaultHandoverHours)
    {
        var ordered = OrderAssignments(assignments).Where(a => !a.IsOnCall).ToList();
        if (ordered.Count == 0)
        {
            return false;
        }

        var segments = new List<(DateTime Start, DateTime End)>();
        foreach (var assignment in ordered)
        {
            segments.Add(GetShiftWindow(assignment.Date, assignment.ShiftId, shiftInfoById));
        }

        segments.Sort((a, b) => a.Start.CompareTo(b.Start));

        var mergedStart = segments[0].Start;
        var mergedEnd = segments[0].End;

        for (var i = 1; i < segments.Count; i++)
        {
            var gap = (segments[i].Start - mergedEnd).TotalHours;
            if (gap <= handoverHours)
            {
                mergedEnd = mergedEnd > segments[i].End ? mergedEnd : segments[i].End;
            }
            else
            {
                if ((mergedEnd - mergedStart).TotalHours > maxConsecutiveHours)
                {
                    return true;
                }

                mergedStart = segments[i].Start;
                mergedEnd = segments[i].End;
            }
        }

        return (mergedEnd - mergedStart).TotalHours > maxConsecutiveHours;
    }

    public static Dictionary<int, ShiftWorkInfo> BuildShiftInfoLookup(IEnumerable<ShiftRequirement> shiftRequirements)
    {
        return shiftRequirements
            .GroupBy(s => s.ShiftId)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var shift = g.First();
                    var duration = shift.DurationHours > 0 ? shift.DurationHours : 8;
                    var start = shift.StartTime;
                    var end = shift.EndTime;
                    if (start == default && end == default)
                    {
                        start = DefaultStartForLabel(shift.ShiftLabel);
                        end = start.Add(TimeSpan.FromHours(duration));
                    }

                    return new ShiftWorkInfo(
                        shift.ShiftId,
                        shift.ShiftLabel,
                        duration,
                        start,
                        end,
                        shift.WeekdayNonProductivityHours,
                        shift.HolidayNonProductivityHours,
                        shift.WeekdayProductivityPlanHours,
                        shift.HolidayProductivityPlanHours);
                });
    }

    public static Func<int, bool> BuildProductivityPlanLookup(IEnumerable<UserConstraint> users)
    {
        var map = users.ToDictionary(u => u.UserId, u => u.IncludedInProductivityPlan);
        return userId => map.TryGetValue(userId, out var inPlan) && inPlan;
    }

    private static (DateTime Start, DateTime End) GetShiftWindow(
        DateTime date,
        int shiftId,
        IReadOnlyDictionary<int, ShiftWorkInfo> shiftInfoById)
    {
        if (!shiftInfoById.TryGetValue(shiftId, out var info))
        {
            var start = date.Date.AddHours(7);
            return (start, start.AddHours(8));
        }

        var shiftStart = date.Date + info.StartTime;
        var shiftEnd = date.Date + info.EndTime;
        if (shiftEnd <= shiftStart)
        {
            shiftEnd = shiftEnd.AddDays(1);
        }

        return (shiftStart, shiftEnd);
    }

    private static TimeSpan DefaultStartForLabel(ShiftLabel label) => label switch
    {
        ShiftLabel.Evening => TimeSpan.FromHours(15),
        ShiftLabel.Night => TimeSpan.FromHours(23),
        _ => TimeSpan.FromHours(7)
    };

    private static List<SaShiftAssignment> OrderAssignments(IEnumerable<SaShiftAssignment> assignments)
    {
        return assignments
            .OrderBy(a => a.Date.Date)
            .ThenBy(a => AdjacentShiftRestRules.LabelOrder(a.ShiftLabel))
            .ThenBy(a => a.ShiftId)
            .ToList();
    }
}
