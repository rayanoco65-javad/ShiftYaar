using ShiftYar.Application.Common.Utilities;
using ShiftYar.Domain.Entities.ShiftModel;
using System;
using System.Collections.Generic;
using Xunit;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.Tests;

public class ApprovedLeaveCapacityValidatorTests
{
    [Fact]
    public void GetEffectiveRequiredTotalCount_UsesHolidayOverrideWhenPresent()
    {
        var specialty = new ShiftRequiredSpecialty
        {
            RequiredTottalCount = 3,
            HolidayRequiredTottalCount = 2
        };

        Assert.Equal(3, ApprovedLeaveCapacityValidator.GetEffectiveRequiredTotalCount(specialty, isHoliday: false));
        Assert.Equal(2, ApprovedLeaveCapacityValidator.GetEffectiveRequiredTotalCount(specialty, isHoliday: true));
    }

    [Fact]
    public void CalculateDailyShiftDemandForSpecialty_SumsAllShiftsCorrectly()
    {
        var shifts = new List<Shift>
        {
            new()
            {
                Label = ShiftLabel.Morning,
                RequiredSpecialties = [new ShiftRequiredSpecialty { SpecialtyId = 2, RequiredTottalCount = 4 }]
            },
            new()
            {
                Label = ShiftLabel.Evening,
                RequiredSpecialties = [new ShiftRequiredSpecialty { SpecialtyId = 2, RequiredTottalCount = 3 }]
            },
            new()
            {
                Label = ShiftLabel.Night,
                RequiredSpecialties = [new ShiftRequiredSpecialty { SpecialtyId = 2, RequiredTottalCount = 4 }]
            }
        };

        var demand = ApprovedLeaveCapacityValidator.CalculateDailyShiftDemandForSpecialty(
            shifts, specialtyId: 2, isHoliday: false);

        Assert.Equal(11, demand);
    }

    [Fact]
    public void CalculateNightShiftDemandForSpecialty_PicksOnlyNightShifts()
    {
        var shifts = new List<Shift>
        {
            new()
            {
                Label = ShiftLabel.Morning,
                RequiredSpecialties = [new ShiftRequiredSpecialty { SpecialtyId = 2, RequiredTottalCount = 4 }]
            },
            new()
            {
                Label = ShiftLabel.Night,
                RequiredSpecialties = [new ShiftRequiredSpecialty { SpecialtyId = 2, RequiredTottalCount = 4, HolidayRequiredTottalCount = 5 }]
            }
        };

        var normalDemand = ApprovedLeaveCapacityValidator.CalculateNightShiftDemandForSpecialty(
            shifts, specialtyId: 2, isHoliday: false);
        var holidayDemand = ApprovedLeaveCapacityValidator.CalculateNightShiftDemandForSpecialty(
            shifts, specialtyId: 2, isHoliday: true);

        Assert.Equal(4, normalDemand);
        Assert.Equal(5, holidayDemand);
    }

    [Theory]
    [InlineData(21, 11, 4, 6)]  // 21 - (11 + 4) = 6
    [InlineData(20, 11, 4, 5)]  // 20 - 15 = 5
    [InlineData(15, 11, 4, 0)]  // 15 - 15 = 0
    [InlineData(14, 11, 4, 0)]  // 14 - 15 = 0 (never negative)
    public void CalculateMaxDailyLeaveCapacity_CalculatesCorrectCap(
        int totalPersonnel, int todayDemand, int yesterdayNightDemand, int expectedCap)
    {
        var cap = ApprovedLeaveCapacityValidator.CalculateMaxDailyLeaveCapacity(
            totalPersonnel, todayDemand, yesterdayNightDemand);

        Assert.Equal(expectedCap, cap);
    }

    [Theory]
    [InlineData(5, 6, false)] // 5 + 1 <= 6 -> OK
    [InlineData(6, 6, true)]  // 6 + 1 > 6 -> Exceeded
    [InlineData(0, 0, true)]  // 0 + 1 > 0 -> Exceeded
    public void WouldExceedCapacity_UsesApprovedCountPlusOne(
        int approvedCount, int maxCapacity, bool expected)
    {
        Assert.Equal(expected, ApprovedLeaveCapacityValidator.WouldExceedCapacity(approvedCount, maxCapacity));
    }

    [Fact]
    public void BuildExceededCapacityMessage_ReturnsDetailedPersianMessageWhenCapacityFull()
    {
        var message = ApprovedLeaveCapacityValidator.BuildExceededCapacityMessage(
            approvedLeaveCount: 6,
            maxCapacity: 6,
            totalActivePersonnel: 21,
            todayShiftDemand: 11,
            yesterdayNightDemand: 4,
            requestDate: new DateTime(2026, 9, 5),
            specialtyName: "پرستار",
            approvedUserDisplayNames: ["بهاره بهاری پور", "حدیث کاظمی", "فاطمه مدهنی"]);

        Assert.NotNull(message);
        Assert.Contains("ظرفیت مرخصی روزانه در تاریخ", message);
        Assert.Contains("پرستار", message);
        Assert.Contains("۶ نفر", message);
        Assert.Contains("۲۱ نفر", message);
        Assert.Contains("۱۱ نفر", message);
        Assert.Contains("۴ نفر", message);
        Assert.Contains("بهاره بهاری پور", message);
        Assert.Contains("امکان تأیید مرخصی جدید در این تاریخ وجود ندارد", message);
    }

    [Fact]
    public void BuildExceededCapacityMessage_ReturnsSevereShortageMessageWhenCapacityIsZero()
    {
        var message = ApprovedLeaveCapacityValidator.BuildExceededCapacityMessage(
            approvedLeaveCount: 0,
            maxCapacity: 0,
            totalActivePersonnel: 14,
            todayShiftDemand: 11,
            yesterdayNightDemand: 4,
            requestDate: new DateTime(2026, 9, 5),
            specialtyName: "پرستار");

        Assert.NotNull(message);
        Assert.Contains("محدودیت شدید نیرو", message);
        Assert.Contains("امکان اعطای مرخصی وجود ندارد", message);
        Assert.Contains("۱۵ نفر", message);
        Assert.Contains("۱۴ نفر", message);
    }

    [Fact]
    public void BuildExceededCapacityMessage_ReturnsNullWhenCapacityIsAvailable()
    {
        var message = ApprovedLeaveCapacityValidator.BuildExceededCapacityMessage(
            approvedLeaveCount: 5,
            maxCapacity: 6,
            totalActivePersonnel: 21,
            todayShiftDemand: 11,
            yesterdayNightDemand: 4,
            requestDate: new DateTime(2026, 9, 5));

        Assert.Null(message);
    }
}
