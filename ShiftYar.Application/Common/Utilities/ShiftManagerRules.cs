using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ShiftYar.Application.Common.Utilities;

/// <summary>
/// قواعد مسئول شیفت سطح‌دار:
/// Level null = مسئول نیست؛ 1 = سطح ۱؛ 2 = سطح ۲.
/// الزام ترکیب فقط از تعریف شیفت (ShiftRequirement) خوانده می‌شود.
/// </summary>
public static class ShiftManagerRules
{
    public const byte Level1 = 1;
    public const byte Level2 = 2;

    public static byte? NormalizeLevel(byte? level) =>
        level is Level1 or Level2 ? level : null;

    /// <summary>
    /// سطح مؤثر: از ShiftManagerLevel؛ در نبود آن اگر CanBeShiftManager=true → سطح ۱ (سازگاری).
    /// </summary>
    public static byte EffectiveLevel(UserConstraint user)
    {
        var normalized = NormalizeLevel(user.ShiftManagerLevel);
        if (normalized.HasValue)
        {
            return normalized.Value;
        }

        return user.CanBeShiftManager ? Level1 : (byte)0;
    }

    public static bool IsManager(UserConstraint user) => EffectiveLevel(user) > 0;

    public static bool IsLevel1(UserConstraint user) => EffectiveLevel(user) == Level1;

    public static (int RequiredTotal, int MinLevel1) GetRequirement(ShiftRequirement shift)
    {
        var required = Math.Max(0, shift.ManagerRequiredCount);
        var minL1 = Math.Clamp(Math.Max(0, shift.ManagerMinLevel1Count), 0, required);
        return (required, minL1);
    }

    public static bool RequiresAnyManager(ShiftRequirement shift)
    {
        var (required, _) = GetRequirement(shift);
        return required > 0;
    }

    public static bool IsSatisfied(
        IEnumerable<UserConstraint> assignees,
        int requiredTotal,
        int minLevel1)
    {
        if (requiredTotal <= 0)
        {
            return true;
        }

        var managers = assignees.Where(IsManager).ToList();
        if (managers.Count < requiredTotal)
        {
            return false;
        }

        var level1 = managers.Count(IsLevel1);
        return level1 >= Math.Min(minLevel1, requiredTotal);
    }

    public static bool IsSatisfied(
        IEnumerable<UserConstraint> assignees,
        ShiftRequirement shift)
    {
        var (required, minL1) = GetRequirement(shift);
        return IsSatisfied(assignees, required, minL1);
    }

    /// <summary>
    /// نرمال‌سازی فیلدهای مسئول روی DTO کاربر قبل از ذخیره.
    /// </summary>
    public static void NormalizeUserDto(byte? shiftManagerLevel, bool? canBeShiftManager, out byte? level, out bool? canBe)
    {
        level = NormalizeLevel(shiftManagerLevel);
        if (level.HasValue)
        {
            canBe = true;
            return;
        }

        if (canBeShiftManager == true)
        {
            level = Level1;
            canBe = true;
            return;
        }

        level = null;
        canBe = false;
    }

    public static string? ValidateShiftManagerCounts(int? requiredCount, int? minLevel1Count)
    {
        var required = requiredCount ?? 0;
        var minL1 = minLevel1Count ?? 0;
        if (required < 0 || minL1 < 0)
        {
            return "تعداد مسئول شیفت نمی‌تواند منفی باشد.";
        }

        if (minL1 > required)
        {
            return "حداقل مسئول سطح ۱ نمی‌تواند از تعداد کل مسئول بیشتر باشد.";
        }

        return null;
    }

    public static void NormalizeShiftDto(int? requiredCount, int? minLevel1Count, out int required, out int minL1)
    {
        required = Math.Max(0, requiredCount ?? 0);
        minL1 = Math.Clamp(Math.Max(0, minLevel1Count ?? 0), 0, required);
    }

    /// <summary>
    /// آیا حذف این انتساب ترکیب مسئول شیفت همان روز/شیفت را می‌شکند؟
    /// </summary>
    public static bool IsCriticalForManagerMix(
        ShiftConstraints constraints,
        ShiftSolution solution,
        SaShiftAssignment assignment)
    {
        var shiftReq = constraints.ShiftRequirements.FirstOrDefault(s => s.ShiftId == assignment.ShiftId);
        if (shiftReq == null)
        {
            return false;
        }

        var (requiredTotal, minLevel1) = GetRequirement(shiftReq);
        if (requiredTotal <= 0)
        {
            return false;
        }

        var assignees = solution.GetShiftAssignments(assignment.ShiftId, assignment.Date)
            .Where(a => !a.IsOnCall)
            .Select(a => constraints.UserConstraints.FirstOrDefault(u => u.UserId == a.UserId))
            .Where(u => u != null)
            .Cast<UserConstraint>()
            .ToList();

        if (!IsSatisfied(assignees, requiredTotal, minLevel1))
        {
            return false;
        }

        var user = assignees.FirstOrDefault(u => u.UserId == assignment.UserId);
        if (user == null || !IsManager(user))
        {
            return false;
        }

        var without = assignees.Where(u => u.UserId != assignment.UserId).ToList();
        return !IsSatisfied(without, requiredTotal, minLevel1);
    }
}
