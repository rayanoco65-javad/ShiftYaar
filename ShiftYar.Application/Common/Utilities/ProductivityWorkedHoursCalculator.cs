using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using ShiftYar.Domain.Entities.ProductivityModel;
using System;
using System.Collections.Generic;
using System.Linq;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.Common.Utilities;

/// <summary>
/// محاسبه ساعات مؤثر کار طبق قانون ارتقای بهره‌وری:
/// ضریب ۱٫۵ برای شب/تعطیل، تحویل ۱ ساعته بین شیفت‌ها، سقف ۱۲ ساعت متوالی.
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
        TimeSpan EndTime);

    public static double CalculateEffectiveWorkedHours(
        IEnumerable<SaShiftAssignment> assignments,
        IReadOnlyDictionary<int, ShiftWorkInfo> shiftInfoById,
        Func<DateTime, bool> isHoliday,
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
            var shiftHours = GetShiftDurationHours(assignment.ShiftId, shiftInfoById);
            var handover = i > 0 ? handoverHours : 0;
            var weighted = IsWeightedShift(assignment, shiftInfoById, isHoliday);

            total += weighted
                ? shiftHours * nightHolidayMultiplier + handover
                : shiftHours + handover;
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

                    return new ShiftWorkInfo(shift.ShiftId, shift.ShiftLabel, duration, start, end);
                });
    }

    private static bool IsWeightedShift(
        SaShiftAssignment assignment,
        IReadOnlyDictionary<int, ShiftWorkInfo> shiftInfoById,
        Func<DateTime, bool> isHoliday)
    {
        if (isHoliday(assignment.Date.Date))
        {
            return true;
        }

        return shiftInfoById.TryGetValue(assignment.ShiftId, out var info) && info.Label == ShiftLabel.Night;
    }

    private static double GetShiftDurationHours(int shiftId, IReadOnlyDictionary<int, ShiftWorkInfo> shiftInfoById)
    {
        if (shiftInfoById.TryGetValue(shiftId, out var info) && info.DurationHours > 0)
        {
            return info.DurationHours;
        }

        return 8;
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
