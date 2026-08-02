using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.Common.Utilities;

/// <summary>
/// ممنوعیت توالی بدون فاصله: عصر→شب (همان روز) و شب→صبح (روز بعد).
/// صبح+شب همان روز مجاز است چون شیفت عصر بین آن‌ها فاصله زمانی ایجاد می‌کند.
/// </summary>
public static class AdjacentShiftRestRules
{
    public static int LabelOrder(ShiftLabel label) => label switch
    {
        ShiftLabel.Morning => 0,
        ShiftLabel.Evening => 1,
        ShiftLabel.Night => 2,
        _ => 99
    };

    /// <summary>
    /// آیا دو انتساب متوالی از نظر زمانی (بدون نوبت میانی) ممنوع‌اند؟
    /// </summary>
    public static bool IsForbiddenBackToBack(
        ShiftLabel earlierLabel,
        DateTime earlierDate,
        ShiftLabel laterLabel,
        DateTime laterDate)
    {
        var d0 = earlierDate.Date;
        var d1 = laterDate.Date;

        // عصر روز D بلافاصله شب همان روز
        if (earlierLabel == ShiftLabel.Evening &&
            laterLabel == ShiftLabel.Night &&
            d0 == d1)
        {
            return true;
        }

        // شب روز D بلافاصله صبح روز D+1
        if (earlierLabel == ShiftLabel.Night &&
            laterLabel == ShiftLabel.Morning &&
            d1 == d0.AddDays(1))
        {
            return true;
        }

        // صبح+شب همان روز مجاز است (عصر بین آن‌ها فاصله زمانی است)

        return false;
    }

    public static bool HasForbiddenAdjacentPair(IEnumerable<SaShiftAssignment> assignments)
    {
        var ordered = OrderAssignments(assignments);
        for (var i = 1; i < ordered.Count; i++)
        {
            if (IsForbiddenBackToBack(
                    ordered[i - 1].ShiftLabel, ordered[i - 1].Date,
                    ordered[i].ShiftLabel, ordered[i].Date))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// اگر انتساب جدید (date, label) به لیست فعلی اضافه شود، توالی ممنوع ایجاد می‌شود؟
    /// </summary>
    public static bool WouldConflict(
        IEnumerable<SaShiftAssignment> existingAssignments,
        DateTime date,
        ShiftLabel label,
        int? ignoreShiftId = null)
    {
        var proposed = existingAssignments
            .Where(a => !(ignoreShiftId.HasValue &&
                          a.ShiftId == ignoreShiftId.Value &&
                          a.Date.Date == date.Date))
            .Select(a => (a.Date.Date, a.ShiftLabel, a.ShiftId))
            .Append((date.Date, label, ignoreShiftId ?? -1))
            .OrderBy(x => x.Item1)
            .ThenBy(x => LabelOrder(x.Item2))
            .ToList();

        for (var i = 1; i < proposed.Count; i++)
        {
            if (IsForbiddenBackToBack(
                    proposed[i - 1].Item2, proposed[i - 1].Item1,
                    proposed[i].Item2, proposed[i].Item1))
            {
                return true;
            }
        }

        return false;
    }

    public static List<(SaShiftAssignment Earlier, SaShiftAssignment Later)> FindForbiddenPairs(
        IEnumerable<SaShiftAssignment> assignments)
    {
        var ordered = OrderAssignments(assignments);
        var pairs = new List<(SaShiftAssignment, SaShiftAssignment)>();
        for (var i = 1; i < ordered.Count; i++)
        {
            if (IsForbiddenBackToBack(
                    ordered[i - 1].ShiftLabel, ordered[i - 1].Date,
                    ordered[i].ShiftLabel, ordered[i].Date))
            {
                pairs.Add((ordered[i - 1], ordered[i]));
            }
        }

        return pairs;
    }

    private static List<SaShiftAssignment> OrderAssignments(IEnumerable<SaShiftAssignment> assignments)
    {
        return assignments
            .OrderBy(a => a.Date.Date)
            .ThenBy(a => LabelOrder(a.ShiftLabel))
            .ThenBy(a => a.ShiftId)
            .ToList();
    }
}
