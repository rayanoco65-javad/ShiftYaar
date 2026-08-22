using ShiftYar.Domain.Entities.UserModel;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.Common.Utilities;

/// <summary>
/// تعیین مشارکت در توزیع مازاد برای سهمیه ترکیبی صبح/عصر و صبح/شب.
/// </summary>
public static class ComboShiftQuotaEligibility
{
    public static bool HasComboQuotaConfigured(UserConstraint user) =>
        user.MorningEveningShiftCount.HasValue
        || user.MorningEveningHolidayCount.HasValue
        || user.MorningNightShiftCount.HasValue
        || user.MorningNightHolidayCount.HasValue
        || user.MorningEveningFallbackParticipation != null
        || user.MorningEveningHolidayFallback != null
        || user.MorningNightFallbackParticipation != null
        || user.MorningNightHolidayFallback != null;

    public static bool CanAssignInCoverageFill(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        ShiftLabel label,
        DateTime date)
    {
        if (!HasComboQuotaConfigured(user))
        {
            return true;
        }

        if (label == ShiftLabel.Morning || label == ShiftLabel.Evening)
        {
            if (!ShiftQuotaTypeValidator.CanUseMorningEveningPattern(
                    ToUserStub(user), user.AllowedShiftPermissions))
            {
                return true;
            }

            return CanAssignMorningEveningLabel(solution, constraints, user, label, date);
        }

        if (label == ShiftLabel.Night
            && ShiftQuotaTypeValidator.CanUseMorningNightPattern(
                ToUserStub(user), user.AllowedShiftPermissions))
        {
            return CanAssignMorningNightLabel(solution, constraints, user, date);
        }

        return true;
    }

    public static int GetMaxAllowedMorningEveningTotal(UserConstraint user)
    {
        if (user.MorningEveningShiftCount.HasValue && user.MorningEveningFallbackParticipation == false)
        {
            return user.MorningEveningShiftCount.Value;
        }

        return int.MaxValue;
    }

    public static int GetMaxAllowedMorningNightTotal(UserConstraint user)
    {
        if (user.MorningNightShiftCount.HasValue && user.MorningNightFallbackParticipation == false)
        {
            return user.MorningNightShiftCount.Value;
        }

        return int.MaxValue;
    }

    public static int CountMorningEveningAssignments(ShiftSolution solution, int userId) =>
        solution.GetUserAllAssignments(userId)
            .Count(a => !a.IsOnCall && (a.ShiftLabel == ShiftLabel.Morning || a.ShiftLabel == ShiftLabel.Evening));

    public static int CountMorningEveningHolidayAssignments(
        ShiftSolution solution,
        ShiftConstraints constraints,
        int userId) =>
        solution.GetUserAllAssignments(userId)
            .Count(a => !a.IsOnCall
                        && constraints.IsHoliday(a.Date)
                        && (a.ShiftLabel == ShiftLabel.Morning || a.ShiftLabel == ShiftLabel.Evening));

    public static int CountMorningNightAssignments(ShiftSolution solution, int userId) =>
        solution.GetUserAllAssignments(userId)
            .Count(a => !a.IsOnCall && (a.ShiftLabel == ShiftLabel.Morning || a.ShiftLabel == ShiftLabel.Night));

    public static int CountMorningNightHolidayAssignments(
        ShiftSolution solution,
        ShiftConstraints constraints,
        int userId) =>
        solution.GetUserAllAssignments(userId)
            .Count(a => !a.IsOnCall
                        && constraints.IsHoliday(a.Date)
                        && (a.ShiftLabel == ShiftLabel.Morning || a.ShiftLabel == ShiftLabel.Night));

    private static bool CanAssignMorningEveningLabel(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        ShiftLabel label,
        DateTime date)
    {
        if (!ShiftEligibilityResolver.MayEverTakeLabel(user, label))
        {
            return false;
        }

        var total = CountMorningEveningAssignments(solution, user.UserId);
        var exact = user.MorningEveningShiftCount;
        var fallback = user.MorningEveningFallbackParticipation;
        var holidayExact = user.MorningEveningHolidayCount;
        var holidayFallback = user.MorningEveningHolidayFallback;
        var isHoliday = constraints.IsHoliday(date);

        if (exact.HasValue && total < exact.Value)
        {
            return true;
        }

        if (isHoliday && holidayExact.HasValue)
        {
            var holidayCount = CountMorningEveningHolidayAssignments(solution, constraints, user.UserId);
            if (holidayCount < holidayExact.Value)
            {
                return true;
            }
        }

        if (isHoliday)
        {
            return DayShiftQuotaEligibility.AllowsHolidaySurplus(fallback, holidayFallback);
        }

        return DayShiftQuotaEligibility.AllowsSurplus(fallback);
    }

    private static bool CanAssignMorningNightLabel(
        ShiftSolution solution,
        ShiftConstraints constraints,
        UserConstraint user,
        DateTime date)
    {
        if (!ShiftEligibilityResolver.MayEverTakeLabel(user, ShiftLabel.Night)
            && !ShiftEligibilityResolver.MayEverTakeLabel(user, ShiftLabel.Morning))
        {
            return false;
        }

        var total = CountMorningNightAssignments(solution, user.UserId);
        var exact = user.MorningNightShiftCount;
        var fallback = user.MorningNightFallbackParticipation;
        var holidayExact = user.MorningNightHolidayCount;
        var holidayFallback = user.MorningNightHolidayFallback;
        var isHoliday = constraints.IsHoliday(date);

        if (exact.HasValue && total < exact.Value)
        {
            return true;
        }

        if (isHoliday && holidayExact.HasValue)
        {
            var holidayCount = CountMorningNightHolidayAssignments(solution, constraints, user.UserId);
            if (holidayCount < holidayExact.Value)
            {
                return true;
            }
        }

        if (isHoliday)
        {
            return DayShiftQuotaEligibility.AllowsHolidaySurplus(fallback, holidayFallback);
        }

        return DayShiftQuotaEligibility.AllowsSurplus(fallback);
    }

    private static User ToUserStub(UserConstraint user) => new()
    {
        Id = user.UserId,
        FullName = user.UserName,
        ShiftType = user.ShiftType,
        ShiftSubType = user.ShiftSubType,
        TwoShiftRotationPattern = user.TwoShiftRotationPattern
    };
}
