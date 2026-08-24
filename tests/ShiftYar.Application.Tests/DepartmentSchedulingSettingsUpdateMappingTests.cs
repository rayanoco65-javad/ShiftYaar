using AutoMapper;
using ShiftYar.Application.Common.Mappings;
using ShiftYar.Application.DTOs.DepartmentModel;
using ShiftYar.Domain.Entities.DepartmentModel;
using Xunit;

namespace ShiftYar.Application.Tests;

public class DepartmentSchedulingSettingsUpdateMappingTests
{
    private static IMapper CreateMapper()
    {
        var config = new MapperConfiguration(cfg => cfg.AddProfile<UserProfile>());
        return config.CreateMapper();
    }

    [Fact]
    public void Map_WhenPostNightFlagsOmitted_PreservesExistingEntityValues()
    {
        var mapper = CreateMapper();
        var entity = new DepartmentSchedulingSettings
        {
            DepartmentId = 2,
            MaxShiftsPerDay = 2,
            AllowEveningAfterNightShift = true,
            AllowNightShiftAfterNightShift = true
        };

        var dto = new DepartmentSchedulingSettingsDtoAdd
        {
            DepartmentId = 2,
            MaxShiftsPerDay = 1
            // Allow* intentionally omitted → null
        };

        mapper.Map(dto, entity);

        Assert.Equal(1, entity.MaxShiftsPerDay);
        Assert.True(entity.AllowEveningAfterNightShift);
        Assert.True(entity.AllowNightShiftAfterNightShift);
    }

    [Fact]
    public void Map_WhenPostNightFlagsExplicitFalse_UpdatesEntity()
    {
        var mapper = CreateMapper();
        var entity = new DepartmentSchedulingSettings
        {
            DepartmentId = 2,
            AllowEveningAfterNightShift = true,
            AllowNightShiftAfterNightShift = true
        };

        var dto = new DepartmentSchedulingSettingsDtoAdd
        {
            DepartmentId = 2,
            AllowEveningAfterNightShift = false,
            AllowNightShiftAfterNightShift = false
        };

        mapper.Map(dto, entity);

        Assert.False(entity.AllowEveningAfterNightShift);
        Assert.False(entity.AllowNightShiftAfterNightShift);
    }
}
