using ShiftYar.Application.Common.Utilities;
using ShiftYar.Domain.Entities.UserModel;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;
using static ShiftYar.Domain.Enums.UserModel.UserEnums;
using Xunit;

namespace ShiftYar.Application.Tests;

public class DayShiftQuotaPermissionValidatorTests
{
    [Fact]
    public void Validate_RejectsMorningQuotaForFixedEveningUser()
    {
        var user = new User
        {
            Id = 10,
            FullName = "مینا",
            ShiftType = ShiftTypes.FixedShift,
            ShiftSubType = ShiftSubTypes.FixedEvening,
            AllowedShiftPermissions = UserShiftPermission.Evening
        };

        var permissions = ShiftEligibilityResolver.ResolvePermissions(
            user.AllowedShiftPermissions, user.ShiftType!.Value, user.ShiftSubType!.Value, null);

        var error = DayShiftQuotaPermissionValidator.Validate(
            user,
            permissions,
            exactMorningShiftCount: 3,
            morningFallbackParticipation: false,
            exactHolidayMorningShiftCount: null,
            morningHolidayFallbackParticipation: false,
            exactEveningShiftCount: null,
            eveningFallbackParticipation: false,
            exactHolidayEveningShiftCount: null,
            eveningHolidayFallbackParticipation: false);

        Assert.NotNull(error);
        Assert.Contains("صبح", error);
    }

    [Fact]
    public void Validate_AllowsEveningQuotaForFixedEveningUser()
    {
        var user = new User
        {
            Id = 10,
            FullName = "مینا",
            ShiftType = ShiftTypes.FixedShift,
            ShiftSubType = ShiftSubTypes.FixedEvening,
            AllowedShiftPermissions = UserShiftPermission.Evening
        };

        var permissions = ShiftEligibilityResolver.ResolvePermissions(
            user.AllowedShiftPermissions, user.ShiftType!.Value, user.ShiftSubType!.Value, null);

        var error = DayShiftQuotaPermissionValidator.Validate(
            user,
            permissions,
            exactMorningShiftCount: null,
            morningFallbackParticipation: false,
            exactHolidayMorningShiftCount: null,
            morningHolidayFallbackParticipation: false,
            exactEveningShiftCount: 4,
            eveningFallbackParticipation: true,
            exactHolidayEveningShiftCount: 1,
            eveningHolidayFallbackParticipation: false);

        Assert.Null(error);
    }
}
