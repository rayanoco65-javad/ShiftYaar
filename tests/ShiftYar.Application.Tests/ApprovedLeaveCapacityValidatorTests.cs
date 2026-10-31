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
    public void CalculateDailyShiftDemandForSpecialty_SumsMultipleResponsibilitiesForSameSpecialty()
    {
        var shifts = new List<Shift>
        {
            new()
            {
                Label = ShiftLabel.Morning,
                RequiredSpecialties =
                [
                    new ShiftRequiredSpecialty { SpecialtyId = 2, DepartmentResponsibilityId = 1, RequiredTottalCount = 2 },
                    new ShiftRequiredSpecialty { SpecialtyId = 2, DepartmentResponsibilityId = 2, RequiredTottalCount = 3 }
                ]
            }
        };

        var demand = ApprovedLeaveCapacityValidator.CalculateDailyShiftDemandForSpecialty(
            shifts, specialtyId: 2, isHoliday: false);

        Assert.Equal(5, demand);
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

    [Fact]
    public void CanUserPerformLongShift_DetectsAllowedPermissionsAndCoupledRules_ConsideringMaxShiftsPerDay()
    {
        var userWithPerm = new ShiftYar.Domain.Entities.UserModel.User
        {
            AllowedShiftPermissions = UserShiftPermission.Morning | UserShiftPermission.Evening | UserShiftPermission.MorningEveningSameDay
        };
        // اگر سقف روزانه دپارتمان روی ۱ باشد، شیفت‌های لانگ فعال نمی‌شوند
        Assert.False(ApprovedLeaveCapacityValidator.CanUserPerformMorningEvening(userWithPerm, maxShiftsPerDay: 1));
        Assert.False(ApprovedLeaveCapacityValidator.CanUserPerformLongShift(userWithPerm, maxShiftsPerDay: 1));

        // اگر سقف روزانه دپارتمان روی ۲ باشد، بر اساس مجوزهای نوع شیفت کاربر فعال می‌شود
        Assert.True(ApprovedLeaveCapacityValidator.CanUserPerformMorningEvening(userWithPerm, maxShiftsPerDay: 2));
        Assert.False(ApprovedLeaveCapacityValidator.CanUserPerformMorningNight(userWithPerm, maxShiftsPerDay: 2));
        Assert.True(ApprovedLeaveCapacityValidator.CanUserPerformLongShift(userWithPerm, maxShiftsPerDay: 2));

        var userCoupled = new ShiftYar.Domain.Entities.UserModel.User
        {
            MorningRequiresEvening = true
        };
        Assert.False(ApprovedLeaveCapacityValidator.CanUserPerformLongShift(userCoupled, maxShiftsPerDay: 1));
        Assert.True(ApprovedLeaveCapacityValidator.CanUserPerformMorningEvening(userCoupled, maxShiftsPerDay: 2));
        Assert.True(ApprovedLeaveCapacityValidator.CanUserPerformLongShift(userCoupled, maxShiftsPerDay: 2));

        var userSingle = new ShiftYar.Domain.Entities.UserModel.User
        {
            AllowedShiftPermissions = UserShiftPermission.Morning | UserShiftPermission.Evening
        };
        Assert.False(ApprovedLeaveCapacityValidator.CanUserPerformLongShift(userSingle, maxShiftsPerDay: 2));

        // کاربری که مجوز صریح لانگ ندارد و گردشی ۳ شیفت است نباید لانگ مجاز شود
        var userRotatingDefault = new ShiftYar.Domain.Entities.UserModel.User
        {
            ShiftType = ShiftTypes.RotatingShift,
            ShiftSubType = ShiftSubTypes.ThreeShifts,
            AllowedShiftPermissions = null
        };
        Assert.False(ApprovedLeaveCapacityValidator.CanUserPerformLongShift(userRotatingDefault, maxShiftsPerDay: 1));
        Assert.False(ApprovedLeaveCapacityValidator.CanUserPerformMorningEvening(userRotatingDefault, maxShiftsPerDay: 2));

        // کاربری با شیفت ثابت حتی اگر فلگ صریح لانگ داشته باشد نباید مجاز به لانگ باشد
        var fixedUserWithFlag = new ShiftYar.Domain.Entities.UserModel.User
        {
            ShiftType = ShiftTypes.FixedShift,
            ShiftSubType = ShiftSubTypes.FixedMorning,
            AllowedShiftPermissions = UserShiftPermission.Morning | UserShiftPermission.MorningEveningSameDay
        };
        Assert.False(ApprovedLeaveCapacityValidator.CanUserPerformMorningEvening(fixedUserWithFlag, maxShiftsPerDay: 2));
    }

    [Fact]
    public void CalculateMaxLongShifts_ComputesCorrectPairs_RespectingMaxShiftsPerDay()
    {
        var users = new List<ShiftYar.Domain.Entities.UserModel.User>();
        // ۴ کاربر مجاز به صبح+عصر
        for (int i = 0; i < 4; i++)
        {
            users.Add(new ShiftYar.Domain.Entities.UserModel.User
            {
                Id = i + 1,
                AllowedShiftPermissions = UserShiftPermission.Morning | UserShiftPermission.Evening | UserShiftPermission.MorningEveningSameDay
            });
        }
        // ۸ کاربر عادی
        for (int i = 4; i < 12; i++)
        {
            users.Add(new ShiftYar.Domain.Entities.UserModel.User
            {
                Id = i + 1,
                AllowedShiftPermissions = UserShiftPermission.Morning | UserShiftPermission.Evening
            });
        }

        // ۶ صبح، ۴ عصر، ۲ شب
        // در حالت maxShiftsPerDay = 1، حتی با داشتن مجوزهای لانگ، هیچ شیفت لانگی فعال نمی‌شود
        var maxLongWithCap1 = ApprovedLeaveCapacityValidator.CalculateMaxLongShifts(
            morningDemand: 6, eveningDemand: 4, nightDemand: 2, users, maxShiftsPerDay: 1);
        Assert.Equal(0, maxLongWithCap1);

        // در حالت maxShiftsPerDay = 2، شیفت‌های لانگ بر اساس مجوزهای نوع شیفت فعال می‌شوند
        var maxLongWithCap2 = ApprovedLeaveCapacityValidator.CalculateMaxLongShifts(
            morningDemand: 6, eveningDemand: 4, nightDemand: 2, users, maxShiftsPerDay: 2);
        Assert.Equal(4, maxLongWithCap2);
    }

    [Fact]
    public void CalculateMaxDailyLeaveCapacity_WithLongShifts_ReducesRequiredPersonnelAndAllowsLeave()
    {
        // دپارتمان با ۱۲ نفر و ۱۲ شیفت. ۴ شیفت لانگ ممکن است => نیاز موثر = ۸ نفر
        var withoutLong = ApprovedLeaveCapacityValidator.CalculateMaxDailyLeaveCapacity(
            totalActivePersonnel: 12, todayShiftDemand: 12, yesterdayNightShiftDemand: 0, maxLongShifts: 0);
        Assert.Equal(0, withoutLong); // قبلاً صفر می‌شد و مرخصی داده نمی‌شد

        var withLong = ApprovedLeaveCapacityValidator.CalculateMaxDailyLeaveCapacity(
            totalActivePersonnel: 12, todayShiftDemand: 12, yesterdayNightShiftDemand: 0, maxLongShifts: 4);
        Assert.Equal(4, withLong); // با لانگ، سقف مرخصی به ۴ افزایش می‌یابد
    }

    [Fact]
    public void ValidateAvailablePersonnelCoverage_ReturnsErrorWhenRemainingPersonnelCannotCover()
    {
        // نیاز شیفت ۱۲ عدد است، اما ۸ پرسنل باقیمانده بدون شیفت لانگ حداکثر ۸ شیفت پوشش می‌دهند
        var error = ApprovedLeaveCapacityValidator.ValidateAvailablePersonnelCoverage(
            availablePersonnelCount: 8,
            availableLongShifts: 0,
            todayShiftDemand: 12,
            requestDate: new DateTime(2026, 10, 28));

        Assert.NotNull(error);
        Assert.Contains("حداکثر توانایی پوشش ۸ شیفت را دارند", error);
        Assert.Contains("نیاز شیفت‌های این روز ۱۲ شیفت است", error);
    }

    [Fact]
    public void CalculateMaxLongShifts_DoesNotCountUser_WhenUserHasOffRequestForShift()
    {
        var users = new List<ShiftYar.Domain.Entities.UserModel.User>();
        for (int i = 0; i < 4; i++)
        {
            users.Add(new ShiftYar.Domain.Entities.UserModel.User
            {
                Id = i + 1,
                AllowedShiftPermissions = UserShiftPermission.Morning | UserShiftPermission.Evening | UserShiftPermission.MorningEveningSameDay
            });
        }

        var today = new DateTime(2026, 10, 25);
        // کاربر ۱ در شیفت عصر درخواست عدم حضور تأییدشده دارد
        var approvedRequests = new List<ShiftYar.Domain.Entities.ShiftRequestModel.ShiftRequest>
        {
            new()
            {
                Id = 101,
                UserId = 1,
                RequestAction = ShiftYar.Domain.Enums.ShiftRequestModel.RequestAction.RequestToBeOffShift,
                RequestType = ShiftYar.Domain.Enums.ShiftRequestModel.RequestType.SpecificShift,
                ShiftLabel = ShiftLabel.Evening,
                RequestDate = today,
                Status = ShiftYar.Domain.Enums.ShiftRequestModel.RequestStatus.Approved
            }
        };

        // تقاضا: ۶ صبح، ۵ عصر، ۱ شب
        // کاربر ۱ نباید شمرده شود؛ بنابراین از ۴ کاربر فقط ۳ کاربر می‌توانند لانگ باشند
        var maxLong = ApprovedLeaveCapacityValidator.CalculateMaxLongShifts(
            morningDemand: 6,
            eveningDemand: 5,
            nightDemand: 1,
            users,
            maxShiftsPerDay: 2,
            approvedRequestsOnDate: approvedRequests);

        Assert.Equal(3, maxLong);
    }

    [Fact]
    public void ValidateDailyShiftCoverage_DetectsDeficit_WhenDoubleShiftsAndStaffCannotCoverAllSlots()
    {
        // شبیه‌سازی دقیق دپارتمان ۳ در تاریخ ۳ آبان:
        // ۱۲ پرسنل:
        // کاربر ۳۴: ثابت صبح
        // کاربر ۳۸ و ۴۶: مجوز صبح+عصر
        // کاربر ۴۴: مجوز صبح+عصر ولی درخواست عدم حضور در شیفت عصر دارد
        // کاربر ۴۰ و ۴۵: مرخصی کل‌روز
        // کاربر ۴۷: دیشب شیفت شب بوده (استراحت اجباری امروز)
        // سایر کاربران (۳۵، ۳۶، ۳۷، ۳۹، ۴۳): گردشی عادی تک‌شیفت
        var users = new List<ShiftYar.Domain.Entities.UserModel.User>
        {
            new() { Id = 34, ShiftType = ShiftTypes.FixedShift, ShiftSubType = ShiftSubTypes.FixedMorning, AllowedShiftPermissions = UserShiftPermission.Morning },
            new() { Id = 35, ShiftType = ShiftTypes.RotatingShift, ShiftSubType = ShiftSubTypes.ThreeShifts, AllowedShiftPermissions = UserShiftPermission.Morning | UserShiftPermission.Evening | UserShiftPermission.Night },
            new() { Id = 36, ShiftType = ShiftTypes.RotatingShift, ShiftSubType = ShiftSubTypes.ThreeShifts, AllowedShiftPermissions = UserShiftPermission.Morning | UserShiftPermission.Evening | UserShiftPermission.Night },
            new() { Id = 37, ShiftType = ShiftTypes.RotatingShift, ShiftSubType = ShiftSubTypes.ThreeShifts, AllowedShiftPermissions = UserShiftPermission.Morning | UserShiftPermission.Evening | UserShiftPermission.Night },
            new() { Id = 38, ShiftType = ShiftTypes.RotatingShift, ShiftSubType = ShiftSubTypes.ThreeShifts, AllowedShiftPermissions = UserShiftPermission.Morning | UserShiftPermission.Evening | UserShiftPermission.Night | UserShiftPermission.MorningEveningSameDay },
            new() { Id = 39, ShiftType = ShiftTypes.RotatingShift, ShiftSubType = ShiftSubTypes.ThreeShifts, AllowedShiftPermissions = UserShiftPermission.Morning | UserShiftPermission.Evening | UserShiftPermission.Night },
            new() { Id = 40, ShiftType = ShiftTypes.RotatingShift, ShiftSubType = ShiftSubTypes.ThreeShifts, AllowedShiftPermissions = UserShiftPermission.Morning | UserShiftPermission.Evening | UserShiftPermission.Night | UserShiftPermission.MorningEveningSameDay },
            new() { Id = 43, ShiftType = ShiftTypes.RotatingShift, ShiftSubType = ShiftSubTypes.ThreeShifts, AllowedShiftPermissions = UserShiftPermission.Morning | UserShiftPermission.Evening | UserShiftPermission.Night },
            new() { Id = 44, ShiftType = ShiftTypes.RotatingShift, ShiftSubType = ShiftSubTypes.ThreeShifts, AllowedShiftPermissions = UserShiftPermission.Morning | UserShiftPermission.Evening | UserShiftPermission.Night | UserShiftPermission.MorningEveningSameDay },
            new() { Id = 45, ShiftType = ShiftTypes.RotatingShift, ShiftSubType = ShiftSubTypes.ThreeShifts, AllowedShiftPermissions = UserShiftPermission.Morning | UserShiftPermission.Evening | UserShiftPermission.Night },
            new() { Id = 46, ShiftType = ShiftTypes.RotatingShift, ShiftSubType = ShiftSubTypes.ThreeShifts, AllowedShiftPermissions = UserShiftPermission.Morning | UserShiftPermission.Evening | UserShiftPermission.Night | UserShiftPermission.MorningEveningSameDay },
            new() { Id = 47, ShiftType = ShiftTypes.RotatingShift, ShiftSubType = ShiftSubTypes.ThreeShifts, AllowedShiftPermissions = UserShiftPermission.Morning | UserShiftPermission.Evening | UserShiftPermission.Night },
        };

        var today = new DateTime(2026, 10, 25);
        var yesterday = today.AddDays(-1);

        var approvedToday = new List<ShiftYar.Domain.Entities.ShiftRequestModel.ShiftRequest>
        {
            new() { Id = 2999, UserId = 40, RequestAction = ShiftYar.Domain.Enums.ShiftRequestModel.RequestAction.RequestToBeOffShift, RequestType = ShiftYar.Domain.Enums.ShiftRequestModel.RequestType.FullDay, RequestDate = today, Status = ShiftYar.Domain.Enums.ShiftRequestModel.RequestStatus.Approved },
            new() { Id = 3021, UserId = 44, RequestAction = ShiftYar.Domain.Enums.ShiftRequestModel.RequestAction.RequestToBeOffShift, RequestType = ShiftYar.Domain.Enums.ShiftRequestModel.RequestType.SpecificShift, ShiftLabel = ShiftLabel.Evening, RequestDate = today, Status = ShiftYar.Domain.Enums.ShiftRequestModel.RequestStatus.Approved },
        };

        var approvedYesterday = new List<ShiftYar.Domain.Entities.ShiftRequestModel.ShiftRequest>
        {
            new() { Id = 3056, UserId = 47, RequestAction = ShiftYar.Domain.Enums.ShiftRequestModel.RequestAction.RequestToBeOnShift, RequestType = ShiftYar.Domain.Enums.ShiftRequestModel.RequestType.SpecificShift, ShiftLabel = ShiftLabel.Night, RequestDate = yesterday, Status = ShiftYar.Domain.Enums.ShiftRequestModel.RequestStatus.Approved },
        };

        // کاربر ۴۵ می‌خواهد مرخصی کل‌روز بگیرد:
        var pending45 = new ShiftYar.Domain.Entities.ShiftRequestModel.ShiftRequest
        {
            Id = 3025,
            UserId = 45,
            RequestAction = ShiftYar.Domain.Enums.ShiftRequestModel.RequestAction.RequestToBeOffShift,
            RequestType = ShiftYar.Domain.Enums.ShiftRequestModel.RequestType.FullDay,
            RequestDate = today,
            Status = ShiftYar.Domain.Enums.ShiftRequestModel.RequestStatus.Pending
        };

        var error = ApprovedLeaveCapacityValidator.ValidateDailyShiftCoverage(
            morningDemand: 6,
            eveningDemand: 5,
            nightDemand: 1,
            yesterdayNightDemand: 1,
            departmentUsers: users,
            requestDate: today,
            maxShiftsPerDay: 2,
            approvedRequestsOnDate: approvedToday,
            approvedRequestsYesterday: approvedYesterday,
            pendingRequest: pending45);

        Assert.NotNull(error);
        // خطا باید عدم توانایی پوشش و کسری را با عدد دقیق نشان دهد
        Assert.Contains("امکان تأیید", error);
        Assert.Contains("کمبود نیرو", error);
    }
}

