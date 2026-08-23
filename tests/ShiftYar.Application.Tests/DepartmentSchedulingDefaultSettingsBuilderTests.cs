using ShiftYar.Application.Common.Utilities;
using ShiftYar.Domain.Entities.DepartmentModel;
using ShiftYar.Domain.Entities.ShiftModel;
using ShiftYar.Domain.Entities.UserModel;
using Xunit;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;
using static ShiftYar.Domain.Enums.UserModel.UserEnums;

namespace ShiftYar.Application.Tests;

public class DepartmentSchedulingDefaultSettingsBuilderTests
{
    [Fact]
    public void Build_PediatricsLikeProfile_UsesNightAverseRestRulesWithoutSeniorityOrManagerDefaults()
    {
        var profile = new DepartmentSchedulingProfile
        {
            DepartmentId = 2,
            IsNightLover = false,
            ActiveUserCount = 21,
            RotatingUserCount = 19,
            ThreeShiftRotatingUserCount = 19,
            TwoShiftRotatingUserCount = 0,
            ShiftManagerCount = 3,
            HasMixedGenderStaff = true,
            HasNightShift = true,
            NightHeadcountPerShift = 4,
            AllowsSameDayMultiShift = true
        };

        var dto = DepartmentSchedulingDefaultSettingsBuilder.Build(profile);

        Assert.False(dto.AllowEveningAfterNightShift);
        Assert.False(dto.AllowNightShiftAfterNightShift);
        Assert.Equal(1, dto.MaxConsecutiveNightShifts);
        Assert.Equal(1, dto.NightShiftDistributionType);
        Assert.False(dto.EnableNightShiftDistributionBySeniority);
        Assert.False(dto.RequireManagerForEveningShift);
        Assert.False(dto.RequireManagerForNightShift);
        Assert.Equal(0.0, dto.ShiftManagerRequirementWeight);
        Assert.Equal(2, dto.MaxShiftsPerDay);
        Assert.Equal(6, dto.MaxShiftsPerWeek);
        Assert.False(dto.EnforceMinimumShiftsForRotatingStaff);
        Assert.Equal(3.0, dto.FairShiftCountBalanceWeight);
    }

    [Fact]
    public void Build_SmallFixedShiftDepartment_KeepsSingleShiftPerDay()
    {
        var profile = new DepartmentSchedulingProfile
        {
            DepartmentId = 5,
            IsNightLover = null,
            ActiveUserCount = 4,
            RotatingUserCount = 0,
            ShiftManagerCount = 1,
            HasMixedGenderStaff = false,
            HasNightShift = false,
            AllowsSameDayMultiShift = false
        };

        var dto = DepartmentSchedulingDefaultSettingsBuilder.Build(profile);

        Assert.Equal(1, dto.MaxShiftsPerDay);
        Assert.False(dto.EnforceMinRestDays);
        Assert.False(dto.RequireManagerForNightShift);
        Assert.Equal(2, dto.NightShiftDistributionType);
    }

    [Fact]
    public void ProfileFactory_BuildsNightHeadcountFromShiftRequirements()
    {
        var department = new Department { Id = 2, IsNightLover = false };
        var users = new List<User>
        {
            new() { Id = 1, ShiftType = ShiftTypes.RotatingShift, ShiftSubType = ShiftSubTypes.ThreeShifts, Gender = UserGender.Male },
            new() { Id = 2, ShiftType = ShiftTypes.RotatingShift, ShiftSubType = ShiftSubTypes.ThreeShifts, Gender = UserGender.Female, CanBeShiftManager = true }
        };
        var shifts = new List<Shift>
        {
            new()
            {
                Id = 6,
                Label = ShiftLabel.Night,
                RequiredSpecialties = new List<ShiftRequiredSpecialty>
                {
                    new() { RequiredTottalCount = 4 }
                }
            }
        };

        var profile = DepartmentSchedulingProfileFactory.Create(department, users, shifts);

        Assert.Equal(2, profile.ActiveUserCount);
        Assert.True(profile.HasNightShift);
        Assert.Equal(4, profile.NightHeadcountPerShift);
        Assert.True(profile.HasMixedGenderStaff);
        Assert.True(profile.AllowsSameDayMultiShift);
        Assert.Equal(1, profile.ShiftManagerCount);
    }
}
