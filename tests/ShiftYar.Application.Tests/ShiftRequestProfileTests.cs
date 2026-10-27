using AutoMapper;
using ShiftYar.Application.Common.Mappings;
using ShiftYar.Application.DTOs.ShiftModel.ShiftRequestModel;
using ShiftYar.Domain.Entities.ShiftRequestModel;
using ShiftYar.Domain.Entities.UserModel;
using ShiftYar.Domain.Enums.ShiftRequestModel;
using System;
using Xunit;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.Tests;

public class ShiftRequestProfileTests
{
    private readonly IMapper _mapper;

    public ShiftRequestProfileTests()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<ShiftRequestProfile>();
        });
        _mapper = config.CreateMapper();
    }

    [Fact]
    public void ShiftRequest_MapsToDtoGet_WithPersianDateAndUserDetails()
    {
        // 2026-11-05 corresponds to 1405/08/15 in Persian calendar
        var gregorianDate = new DateTime(2026, 11, 5, 0, 0, 0, DateTimeKind.Unspecified);
        var request = new ShiftRequest
        {
            Id = 3074,
            UserId = 34,
            RequestDate = gregorianDate,
            ShiftLabel = ShiftLabel.Night,
            RequestAction = RequestAction.RequestToBeOnShift,
            RequestType = RequestType.SpecificShift,
            Status = RequestStatus.Pending,
            User = new User
            {
                Id = 34,
                FullName = "سمانه نظری",
                DepartmentId = 3
            },
            Supervisor = new User
            {
                Id = 10,
                FullName = "سوپروایزر بخش"
            }
        };

        var dto = _mapper.Map<ShiftRequestDtoGet>(request);

        Assert.NotNull(dto);
        Assert.Equal(3074, dto.Id);
        Assert.Equal(34, dto.UserId);
        Assert.Equal(gregorianDate, dto.RequestDate);
        Assert.Equal("1405/08/14", dto.RequestPersianDate);
        Assert.Equal(1405, dto.PersianYear);
        Assert.Equal(8, dto.PersianMonth);
        Assert.Equal(14, dto.PersianDay);
        Assert.Equal(3, dto.DepartmentId);
        Assert.Equal("سمانه نظری", dto.UserFullName);
        Assert.Equal("سوپروایزر بخش", dto.SupervisorFullName);
    }

    [Fact]
    public void ShiftRequest_WithNullDate_MapsSafely()
    {
        var request = new ShiftRequest
        {
            Id = 1,
            UserId = 10,
            RequestDate = null
        };

        var dto = _mapper.Map<ShiftRequestDtoGet>(request);

        Assert.NotNull(dto);
        Assert.Null(dto.RequestPersianDate);
        Assert.Null(dto.PersianYear);
        Assert.Null(dto.PersianMonth);
        Assert.Null(dto.PersianDay);
        Assert.Null(dto.DepartmentId);
        Assert.Null(dto.UserFullName);
    }
}
