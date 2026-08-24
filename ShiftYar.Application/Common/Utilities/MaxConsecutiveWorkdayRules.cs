using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.Common.Utilities;

/// <summary>
/// قوانین روزهای کاری متوالی و جریمهٔ OFFهای طولانیِ بدون درخواست OFF تأییدشده.
/// «روز کاری» = حداقل یک انتساب غیرآن‌کال در آن روز تقویمی.
/// </summary>
public static class MaxConsecutiveWorkdayRules
{
    /// <summary>
    /// بیش از این تعداد روز OFF پشت‌سرهم (بدون احتساب OFF تأییدشده) جریمه نرم می‌گیرد.
    /// </summary>
    public const int DefaultUnexcusedOffRunThreshold = 3;

    public static HashSet<DateTime> GetWorkDates(
        ShiftSolution solution,
        int userId,
        bool ignoreOnCall = true) =>
        GetWorkDatesFromAssignments(solution.GetUserAllAssignments(userId), ignoreOnCall);

    public static HashSet<DateTime> GetWorkDatesFromAssignments(
        IEnumerable<SaShiftAssignment> assignments,
        bool ignoreOnCall = true) =>
        assignments
            .Where(a => !ignoreOnCall || !a.IsOnCall)
            .Select(a => a.Date.Date)
            .ToHashSet();

    public static int CountRunEndingBefore(IReadOnlySet<DateTime> workDates, DateTime date)
    {
        var cursor = date.Date.AddDays(-1);
        var count = 0;
        while (workDates.Contains(cursor))
        {
            count++;
            cursor = cursor.AddDays(-1);
        }

        return count;
    }

    public static int CountRunStartingAfter(IReadOnlySet<DateTime> workDates, DateTime date)
    {
        var cursor = date.Date.AddDays(1);
        var count = 0;
        while (workDates.Contains(cursor))
        {
            count++;
            cursor = cursor.AddDays(1);
        }

        return count;
    }

    public static int ProjectedRunIfWorkDayAdded(IReadOnlySet<DateTime> workDates, DateTime date)
    {
        if (workDates.Contains(date.Date))
        {
            return 0;
        }

        return CountRunEndingBefore(workDates, date)
               + 1
               + CountRunStartingAfter(workDates, date);
    }

    public static bool WouldExceedMaxConsecutiveWorkdays(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        DateTime date)
    {
        if (!constraints.HardRules.EnforceMaxConsecutiveShifts)
        {
            return false;
        }

        if (user.ShiftType == ShiftTypes.FixedShift)
        {
            return false;
        }

        var workDates = GetWorkDates(solution, user.UserId);
        var projected = ProjectedRunIfWorkDayAdded(workDates, date);
        return projected > user.MaxConsecutiveShifts;
    }

    public static bool WouldExceedMaxConsecutiveWorkdays(
        IEnumerable<SaShiftAssignment> assignments,
        UserConstraint user,
        DateTime candidateDate,
        bool enforce)
    {
        if (!enforce || user.ShiftType == ShiftTypes.FixedShift)
        {
            return false;
        }

        var workDates = GetWorkDatesFromAssignments(assignments);
        var projected = ProjectedRunIfWorkDayAdded(workDates, candidateDate);
        return projected > user.MaxConsecutiveShifts;
    }

    public static int GetMaxConsecutiveWorkRun(ShiftSolution solution, int userId)
    {
        var dates = GetWorkDates(solution, userId).OrderBy(d => d).ToList();
        if (dates.Count == 0)
        {
            return 0;
        }

        var max = 1;
        var run = 1;
        for (var i = 1; i < dates.Count; i++)
        {
            if ((dates[i] - dates[i - 1]).Days == 1)
            {
                run++;
                max = Math.Max(max, run);
            }
            else
            {
                run = 1;
            }
        }

        return max;
    }

    /// <summary>
    /// جریمه OFFهای طولانی که با <see cref="UserConstraint.UnavailableDates"/> (OFF تمام‌روز تأییدشده) پوشش داده نشده‌اند.
    /// </summary>
    public static double CalculateOffSpreadPenalty(
        ShiftSolution solution,
        ShiftConstraints constraints,
        int unexcusedOffRunThreshold = DefaultUnexcusedOffRunThreshold)
    {
        if (unexcusedOffRunThreshold < 1)
        {
            return 0;
        }

        double penalty = 0;
        var start = constraints.StartDate.Date;
        var end = constraints.EndDate.Date;
        var totalDays = (end - start).Days + 1;
        if (totalDays <= 0)
        {
            return 0;
        }

        foreach (var user in constraints.UserConstraints.Where(u => u.ShiftType != ShiftTypes.FixedShift))
        {
            var workDates = GetWorkDates(solution, user.UserId);
            var approvedFullDayOff = user.UnavailableDates
                .Select(d => d.Date)
                .ToHashSet();

            var dayIndex = 0;
            while (dayIndex < totalDays)
            {
                var day = start.AddDays(dayIndex);
                if (workDates.Contains(day))
                {
                    dayIndex++;
                    continue;
                }

                var runStart = dayIndex;
                while (dayIndex < totalDays && !workDates.Contains(start.AddDays(dayIndex)))
                {
                    dayIndex++;
                }

                var runLength = dayIndex - runStart;
                if (runLength <= unexcusedOffRunThreshold)
                {
                    continue;
                }

                var excused = 0;
                for (var i = runStart; i < dayIndex; i++)
                {
                    if (approvedFullDayOff.Contains(start.AddDays(i)))
                    {
                        excused++;
                    }
                }

                var unexcused = runLength - excused;
                if (unexcused > unexcusedOffRunThreshold)
                {
                    var excess = unexcused - unexcusedOffRunThreshold;
                    penalty += excess * excess * 6;
                }
            }
        }

        return penalty;
    }

    public static List<string> GetViolations(ShiftSolution solution, ShiftConstraints constraints)
    {
        var violations = new List<string>();
        if (!constraints.HardRules.EnforceMaxConsecutiveShifts)
        {
            return violations;
        }

        foreach (var user in constraints.UserConstraints.Where(u => u.ShiftType != ShiftTypes.FixedShift))
        {
            var maxRun = GetMaxConsecutiveWorkRun(solution, user.UserId);
            if (maxRun > user.MaxConsecutiveShifts)
            {
                violations.Add(
                    $"کاربر {user.UserId}: {maxRun} روز کار متوالی (سقف {user.MaxConsecutiveShifts})");
            }
        }

        return violations;
    }
}
