using ShiftYar.Application.Common.Utilities;
using ShiftYar.Domain.Entities.UserModel;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;
using static ShiftYar.Domain.Enums.UserModel.UserEnums;
using Xunit;

namespace ShiftYar.Application.Tests;

public class NightQuotaPermissionValidatorTests
{
    [Fact]
    public void Validate_AllowsNightQuotaForFixedMorningUser()
    {
        var user = new User
        {
            Id = 34,
            FullName = "سمانه نظری",
            ShiftType = ShiftTypes.FixedShift,
            ShiftSubType = ShiftSubTypes.FixedMorning,
            AllowedShiftPermissions = UserShiftPermission.Morning
        };

        var permissions = ShiftEligibilityResolver.ResolvePermissions(
            user.AllowedShiftPermissions, user.ShiftType!.Value, user.ShiftSubType!.Value, null);

        var error = NightQuotaPermissionValidator.Validate(
            user,
            permissions,
            exactNightShiftCount: 1,
            nightFallbackParticipation: false,
            exactHolidayWeekendNightShiftCount: null,
            holidayWeekendNightFallbackParticipation: null);

        Assert.Null(error);
    }
}
