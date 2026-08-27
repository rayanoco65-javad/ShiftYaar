using ShiftYar.Domain.Entities.DepartmentModel;
using ShiftYar.Domain.Entities.ShiftModel;
using ShiftYar.Domain.Entities.UserModel;
using System.Linq;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;
using static ShiftYar.Domain.Enums.UserModel.UserEnums;

namespace ShiftYar.Application.Common.Utilities;

/// <summary>
/// استخراج پروفایل دپارتمان از داده‌های پایه.
/// </summary>
public static class DepartmentSchedulingProfileFactory
{
    public static DepartmentSchedulingProfile Create(
        Department department,
        IReadOnlyCollection<User> activeUsers,
        IReadOnlyCollection<Shift> departmentShifts)
    {
        ArgumentNullException.ThrowIfNull(department);

        activeUsers ??= Array.Empty<User>();
        departmentShifts ??= Array.Empty<Shift>();

        var rotatingUsers = activeUsers
            .Where(u => u.ShiftType == ShiftTypes.RotatingShift)
            .ToList();

        var nightHeadcount = departmentShifts
            .Where(s => s.Label == ShiftLabel.Night)
            .SelectMany(s => s.RequiredSpecialties ?? Enumerable.Empty<ShiftRequiredSpecialty>())
            .Sum(r => Math.Max(0, r.RequiredTottalCount ?? 0));

        var genders = activeUsers
            .Where(u => u.Gender.HasValue)
            .Select(u => u.Gender!.Value)
            .Distinct()
            .Count();

        return new DepartmentSchedulingProfile
        {
            DepartmentId = department.Id ?? 0,
            IsNightLover = department.IsNightLover,
            ActiveUserCount = activeUsers.Count,
            RotatingUserCount = rotatingUsers.Count,
            ThreeShiftRotatingUserCount = rotatingUsers.Count(u => u.ShiftSubType == ShiftSubTypes.ThreeShifts),
            TwoShiftRotatingUserCount = rotatingUsers.Count(u => u.ShiftSubType == ShiftSubTypes.TwoShifts),
            ShiftManagerCount = activeUsers.Count(u =>
                u.CanBeShiftManager == true
                || u.ShiftManagerLevel == 1
                || u.ShiftManagerLevel == 2),
            HasMixedGenderStaff = genders > 1,
            HasNightShift = departmentShifts.Any(s => s.Label == ShiftLabel.Night),
            NightHeadcountPerShift = Math.Max(0, nightHeadcount),
            AllowsSameDayMultiShift = rotatingUsers.Any(AllowsSameDayMultiShift)
        };
    }

    private static bool AllowsSameDayMultiShift(User user)
    {
        if (user.AllowedShiftPermissions.HasValue)
        {
            var permissions = (UserShiftPermission)user.AllowedShiftPermissions.Value;
            if ((permissions & (UserShiftPermission.MorningEveningSameDay | UserShiftPermission.MorningNightSameDay)) != 0)
            {
                return true;
            }
        }

        return user.ShiftSubType is ShiftSubTypes.TwoShifts or ShiftSubTypes.ThreeShifts;
    }
}
