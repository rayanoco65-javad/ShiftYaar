using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;
using static ShiftYar.Domain.Enums.UserModel.UserEnums;

namespace ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;

/// <summary>
/// پخش عادلانهٔ شیفت صبح/عصر روزهای تعطیل بین پرسنل گردشی.
/// بدون این پاس، تعادل ماهانهٔ M/E می‌تواند همزمان با تمرکز تعطیلات روی چند نفر برقرار باشد.
/// </summary>
public static class HolidayMorningEveningFairnessGuard
{
    public static void Enforce(ShiftSolution solution, ShiftConstraints constraints)
    {
        var holidayDates = Enumerable.Range(0, (constraints.EndDate.Date - constraints.StartDate.Date).Days + 1)
            .Select(i => constraints.StartDate.Date.AddDays(i))
            .Where(constraints.IsHoliday)
            .ToList();

        if (holidayDates.Count == 0)
        {
            return;
        }

        foreach (var specialtyGroup in constraints.UserConstraints
                     .Where(u => u.IsActive && u.ShiftType != ShiftTypes.FixedShift)
                     .GroupBy(u => u.SpecialtyId))
        {
            var peers = specialtyGroup.ToList();
            if (peers.Count < 2)
            {
                continue;
            }

            foreach (var label in new[] { ShiftLabel.Morning, ShiftLabel.Evening })
            {
                RedistributeLabel(solution, constraints, peers, holidayDates, label);
            }
        }
    }

    private static void RedistributeLabel(
        ShiftSolution solution,
        ShiftConstraints constraints,
        List<UserConstraint> peers,
        List<DateTime> holidayDates,
        ShiftLabel label)
    {
        var eligible = peers
            .Where(u => ShiftEligibilityResolver.IsLabelAllowed(u.AllowedShiftLabels, label))
            .ToList();
        if (eligible.Count < 2)
        {
            return;
        }

        for (var pass = 0; pass < 48; pass++)
        {
            var ranked = eligible
                .Select(u => (
                    User: u,
                    Count: CountHolidayLabel(solution, constraints, u.UserId, label)))
                .OrderByDescending(x => x.Count)
                .ThenBy(x => x.User.UserId)
                .ToList();

            var donorEntry = ranked.First();
            if (donorEntry.Count == 0)
            {
                break;
            }

            var donorHolidayAssignments = solution.GetUserAllAssignments(donorEntry.User.UserId)
                .Where(a => !a.IsOnCall && a.ShiftLabel == label && constraints.IsHoliday(a.Date))
                .OrderByDescending(a => a.Date)
                .ToList();

            var moved = false;
            foreach (var receiverEntry in ranked.OrderBy(x => x.Count).ThenBy(x => x.User.UserId))
            {
                if (receiverEntry.User.UserId == donorEntry.User.UserId)
                {
                    continue;
                }

                if (donorEntry.Count - receiverEntry.Count < 2)
                {
                    break;
                }

                foreach (var assignment in donorHolidayAssignments)
                {
                    if (solution.HasAssignment(receiverEntry.User.UserId, assignment.ShiftId, assignment.Date))
                    {
                        continue;
                    }

                    if (!CanTakeHolidayMe(
                            solution, constraints, receiverEntry.User, assignment.Date, assignment.ShiftLabel, assignment.ShiftId))
                    {
                        continue;
                    }

                    solution.RemoveAssignment(donorEntry.User.UserId, assignment.ShiftId, assignment.Date);
                    solution.AddAssignment(
                        receiverEntry.User.UserId,
                        assignment.ShiftId,
                        assignment.Date,
                        assignment.ShiftLabel,
                        assignment.IsOnCall);
                    moved = true;
                    break;
                }

                if (moved)
                {
                    break;
                }
            }

            if (!moved)
            {
                break;
            }
        }
    }

    public static int CountHolidayLabel(
        ShiftSolution solution,
        ShiftConstraints constraints,
        int userId,
        ShiftLabel label) =>
        solution.GetUserAllAssignments(userId)
            .Count(a => !a.IsOnCall && a.ShiftLabel == label && constraints.IsHoliday(a.Date));

    private static bool CanTakeHolidayMe(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        DateTime date,
        ShiftLabel label,
        int shiftId)
    {
        if (user.UnavailableDates.Any(d => d.Date == date.Date))
        {
            return false;
        }

        if (user.UnavailableShiftSlots.Any(s => s.Date.Date == date.Date && s.ShiftLabel == label))
        {
            return false;
        }

        if (solution.HasAssignment(user.UserId, shiftId, date))
        {
            return false;
        }

        // روی تعطیل معمولاً شب دارند یا استراحت مجاور — احترام بگذار
        var existing = solution.GetUserAssignments(user.UserId, date)
            .Where(a => !(a.ShiftId == shiftId && a.Date.Date == date.Date))
            .Select(a => a.ShiftLabel);
        var maxPerDay = constraints.HardRules.EnforceMaxShiftsPerDay
            ? Math.Max(1, constraints.GlobalConstraints.MaxShiftsPerDay)
            : 2;
        if (!DailyAssignmentRules.CanAddShift(
                existing,
                label,
                maxPerDay,
                constraints.HardRules.ForbidDuplicateDailyAssignments))
        {
            return false;
        }

        if (AdjacentShiftRestRules.WouldConflict(
                solution.GetUserAllAssignments(user.UserId),
                date,
                label))
        {
            return false;
        }

        return true;
    }
}
