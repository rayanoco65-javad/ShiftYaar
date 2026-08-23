using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using System.Linq;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;

/// <summary>
/// حذف مازاد سهمیه ترکیبی صبح/عصر و صبح/شب وقتی fallback=false.
/// شب‌های موردنیاز سهمیه شب (<see cref="UserConstraint.ExactNightShiftCount"/>) هرگز حذف نمی‌شوند.
/// </summary>
public static class ExactComboShiftQuotaGuard
{
    public static void Enforce(ShiftSolution solution, ShiftConstraints constraints)
    {
        foreach (var user in constraints.UserConstraints.Where(ComboShiftQuotaEligibility.HasComboQuotaConfigured))
        {
            TrimMorningEveningSurplus(solution, constraints, user);
            TrimMorningNightSurplus(solution, constraints, user);
        }
    }

    private static void TrimMorningEveningSurplus(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user)
    {
        var maxTotal = ComboShiftQuotaEligibility.GetMaxAllowedMorningEveningTotal(user);
        if (maxTotal == int.MaxValue)
        {
            return;
        }

        var targetHoliday = user.MorningEveningHolidayCount ?? 0;

        while (ComboShiftQuotaEligibility.CountMorningEveningAssignments(solution, user.UserId) > maxTotal)
        {
            var assignments = solution.GetUserAllAssignments(user.UserId)
                .Where(a => !a.IsOnCall && (a.ShiftLabel == ShiftLabel.Morning || a.ShiftLabel == ShiftLabel.Evening))
                .OrderByDescending(a => a.Date)
                .ToList();

            var holidayCount = ComboShiftQuotaEligibility.CountMorningEveningHolidayAssignments(
                solution, constraints, user.UserId);

            var removable = assignments
                .Select(a => (
                    Assignment: a,
                    IsHoliday: constraints.IsHoliday(a.Date),
                    CanRemove: !constraints.IsHoliday(a.Date) || holidayCount - 1 >= targetHoliday))
                .Where(x => x.CanRemove)
                .OrderBy(x => x.IsHoliday ? 1 : 0)
                .Select(x => x.Assignment)
                .FirstOrDefault();

            if (removable == null)
            {
                break;
            }

            solution.RemoveAssignment(removable.UserId, removable.ShiftId, removable.Date);
            holidayCount = ComboShiftQuotaEligibility.CountMorningEveningHolidayAssignments(
                solution, constraints, user.UserId);
        }
    }

    private static void TrimMorningNightSurplus(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user)
    {
        var maxTotal = ComboShiftQuotaEligibility.GetMaxAllowedMorningNightTotal(user);
        if (maxTotal == int.MaxValue)
        {
            return;
        }

        var targetHoliday = user.MorningNightHolidayCount ?? 0;
        var minNight = user.ExactNightShiftCount ?? 0;

        while (ComboShiftQuotaEligibility.CountMorningNightAssignments(solution, user.UserId) > maxTotal)
        {
            var nightCount = solution.GetUserAllAssignments(user.UserId)
                .Count(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall);

            var assignments = solution.GetUserAllAssignments(user.UserId)
                .Where(a => !a.IsOnCall && (a.ShiftLabel == ShiftLabel.Morning || a.ShiftLabel == ShiftLabel.Night))
                .OrderByDescending(a => a.Date)
                .ToList();

            var holidayCount = ComboShiftQuotaEligibility.CountMorningNightHolidayAssignments(
                solution, constraints, user.UserId);

            var removable = assignments
                .Select(a => (
                    Assignment: a,
                    IsHoliday: constraints.IsHoliday(a.Date),
                    IsMorning: a.ShiftLabel == ShiftLabel.Morning,
                    CanRemoveHoliday: !constraints.IsHoliday(a.Date) || holidayCount - 1 >= targetHoliday,
                    CanRemoveNight: a.ShiftLabel == ShiftLabel.Night && nightCount > minNight))
                .Where(x => x.IsMorning ? x.CanRemoveHoliday : x.CanRemoveNight && x.CanRemoveHoliday)
                .OrderBy(x => x.IsMorning ? 0 : 1)
                .ThenBy(x => x.IsHoliday ? 1 : 0)
                .Select(x => x.Assignment)
                .FirstOrDefault();

            if (removable == null)
            {
                break;
            }

            solution.RemoveAssignment(removable.UserId, removable.ShiftId, removable.Date);
            if (removable.ShiftLabel == ShiftLabel.Night)
            {
                nightCount--;
            }
        }
    }
}
