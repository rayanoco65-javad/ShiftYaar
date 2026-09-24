using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using ShiftYar.Domain.Entities.UserModel;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.Common.Utilities;

/// <summary>
/// اعتبارسنجی سازگاری سهمیه با نوع/مجوز شیفت کاربر.
/// </summary>
public static class ShiftQuotaTypeValidator
{
    public static string? ValidateMorningEveningQuotaAllowed(User user, UserShiftPermission permissions)
    {
        if (user.ShiftType == ShiftTypes.FixedShift)
        {
            return DescribeUser(user) +
                   " پرسنل شیفت ثابت است و نمی‌تواند سهمیه ترکیبی صبح/عصر داشته باشد. " +
                   DescribeShiftType(user);
        }

        if (!CanUseMorningEveningPattern(user, permissions))
        {
            return DescribeUser(user) +
                   " مجوز یا نوع شیفتش اجازهٔ سهمیه ترکیبی صبح/عصر را نمی‌دهد. " +
                   DescribeShiftType(user) +
                   " — ابتدا مجوز نوع شیفت کاربر را اصلاح کنید.";
        }

        return null;
    }

    public static string? ValidateMorningNightQuotaAllowed(User user, UserShiftPermission permissions)
    {
        if (user.ShiftType == ShiftTypes.FixedShift)
        {
            return DescribeUser(user) +
                   " پرسنل شیفت ثابت است و نمی‌تواند سهمیه ترکیبی صبح/شب داشته باشد. " +
                   DescribeShiftType(user);
        }

        if (!CanUseMorningNightPattern(user, permissions))
        {
            return DescribeUser(user) +
                   " مجوز یا نوع شیفتش اجازهٔ سهمیه ترکیبی صبح/شب را نمی‌دهد. " +
                   DescribeShiftType(user) +
                   " — ابتدا مجوز نوع شیفت کاربر را اصلاح کنید.";
        }

        return null;
    }

    public static bool CanUseMorningEveningPattern(User user, UserShiftPermission permissions)
    {
        if (user.ShiftType == ShiftTypes.FixedShift)
        {
            return false;
        }

        if (permissions.HasFlag(UserShiftPermission.MorningEveningSameDay))
        {
            return true;
        }

        var labels = GetStandaloneLabels(permissions);
        if (!labels.Contains(ShiftLabel.Morning) || !labels.Contains(ShiftLabel.Evening))
        {
            return false;
        }

        return user.ShiftSubType == ShiftSubTypes.ThreeShifts
               || (user.ShiftSubType == ShiftSubTypes.TwoShifts
                   && user.TwoShiftRotationPattern != TwoShiftRotationPattern.MorningNight);
    }

    public static bool CanUseMorningNightPattern(User user, UserShiftPermission permissions)
    {
        if (user.ShiftType == ShiftTypes.FixedShift)
        {
            return false;
        }

        if (permissions.HasFlag(UserShiftPermission.MorningNightSameDay))
        {
            return true;
        }

        var labels = GetStandaloneLabels(permissions);
        if (!labels.Contains(ShiftLabel.Morning))
        {
            return false;
        }

        return user.ShiftSubType == ShiftSubTypes.ThreeShifts && labels.Contains(ShiftLabel.Night)
               || user.ShiftSubType == ShiftSubTypes.TwoShifts
                  && user.TwoShiftRotationPattern == TwoShiftRotationPattern.MorningNight
                  && labels.Contains(ShiftLabel.Night);
    }

    public static bool IsThreeShiftRotating(User user) =>
        user.ShiftType == ShiftTypes.RotatingShift
        && user.ShiftSubType == ShiftSubTypes.ThreeShifts;

    private static IReadOnlyList<ShiftLabel> GetStandaloneLabels(UserShiftPermission permissions) =>
        ShiftEligibilityResolver.GetStandaloneLabels(permissions);

    private static string DescribeUser(User user) =>
        string.IsNullOrWhiteSpace(user.FullName) ? $"کاربر {user.Id}" : user.FullName!;

    private static string DescribeShiftType(User user)
    {
        if (user.ShiftType == ShiftTypes.FixedShift)
        {
            return user.ShiftSubType switch
            {
                ShiftSubTypes.FixedNight => "(شیفت ثابت شب)",
                ShiftSubTypes.FixedEvening => "(شیفت ثابت عصر)",
                _ => "(شیفت ثابت صبح)"
            };
        }

        if (user.ShiftSubType == ShiftSubTypes.ThreeShifts)
        {
            return "(گردشی سه‌نوبته)";
        }

        if (user.ShiftSubType == ShiftSubTypes.TwoShifts)
        {
            return user.TwoShiftRotationPattern switch
            {
                TwoShiftRotationPattern.MorningNight => "(گردشی دو نوبته: صبح/شب)",
                TwoShiftRotationPattern.EveningNight => "(گردشی دو نوبته: عصر/شب)",
                _ => "(گردشی دو نوبته: صبح/عصر)"
            };
        }

        return "(نوع شیفت نامشخص)";
    }
}
