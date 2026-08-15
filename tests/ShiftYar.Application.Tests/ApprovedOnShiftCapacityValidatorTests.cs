using ShiftYar.Application.Common.Utilities;
using ShiftYar.Domain.Entities.ShiftModel;
using Xunit;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.Tests;

public class ApprovedOnShiftCapacityValidatorTests
{
    [Fact]
    public void GetEffectiveRequiredTotalCount_UsesHolidayOverrideWhenPresent()
    {
        var specialty = new ShiftRequiredSpecialty
        {
            RequiredTottalCount = 2,
            HolidayRequiredTottalCount = 1
        };

        Assert.Equal(2, ApprovedOnShiftCapacityValidator.GetEffectiveRequiredTotalCount(specialty, isHoliday: false));
        Assert.Equal(1, ApprovedOnShiftCapacityValidator.GetEffectiveRequiredTotalCount(specialty, isHoliday: true));
    }

    [Fact]
    public void ResolveCapacityForSpecialty_PicksMatchingShiftAndSpecialty()
    {
        var shifts = new List<Shift>
        {
            new()
            {
                Label = ShiftLabel.Morning,
                RequiredSpecialties =
                [
                    new ShiftRequiredSpecialty { SpecialtyId = 10, RequiredTottalCount = 2 },
                    new ShiftRequiredSpecialty { SpecialtyId = 20, RequiredTottalCount = 1 }
                ]
            },
            new()
            {
                Label = ShiftLabel.Evening,
                RequiredSpecialties =
                [
                    new ShiftRequiredSpecialty { SpecialtyId = 10, RequiredTottalCount = 3 }
                ]
            }
        };

        Assert.Equal(2, ApprovedOnShiftCapacityValidator.ResolveCapacityForSpecialty(
            shifts, ShiftLabel.Morning, specialtyId: 10, isHoliday: false));
        Assert.Equal(3, ApprovedOnShiftCapacityValidator.ResolveCapacityForSpecialty(
            shifts, ShiftLabel.Evening, specialtyId: 10, isHoliday: false));
        Assert.Equal(0, ApprovedOnShiftCapacityValidator.ResolveCapacityForSpecialty(
            shifts, ShiftLabel.Night, specialtyId: 10, isHoliday: false));
    }

    [Theory]
    [InlineData(0, 2, false)]
    [InlineData(1, 2, false)]
    [InlineData(2, 2, true)]
    [InlineData(3, 2, true)]
    public void WouldExceedCapacity_UsesApprovedCountPlusOne(int approvedCount, int capacity, bool expected)
    {
        Assert.Equal(expected, ApprovedOnShiftCapacityValidator.WouldExceedCapacity(approvedCount, capacity));
    }

    [Fact]
    public void BuildExceededCapacityMessage_ReturnsPersianMessageWhenFull()
    {
        var message = ApprovedOnShiftCapacityValidator.BuildExceededCapacityMessage(
            approvedOnCount: 2,
            capacity: 2,
            ShiftLabel.Morning,
            new DateTime(2026, 8, 10),
            ["علی رضایی", "مینا جاهدی"]);

        Assert.NotNull(message);
        Assert.Contains("ظرفیت شیفت صبح", message);
        Assert.Contains("2 درخواست حضور تأییدشده", message);
        Assert.Contains("علی رضایی", message);
        Assert.Contains("امکان تأیید درخواست حضور بیش از ظرفیت وجود ندارد", message);
    }

    [Fact]
    public void BuildExceededCapacityMessage_ReturnsNullWhenCapacityAvailable()
    {
        var message = ApprovedOnShiftCapacityValidator.BuildExceededCapacityMessage(
            approvedOnCount: 1,
            capacity: 2,
            ShiftLabel.Morning,
            new DateTime(2026, 8, 10),
            ["علی رضایی"]);

        Assert.Null(message);
    }
}
