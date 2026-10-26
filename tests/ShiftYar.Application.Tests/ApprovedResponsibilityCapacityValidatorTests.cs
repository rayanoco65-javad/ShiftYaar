using ShiftYar.Application.Common.Utilities;
using ShiftYar.Domain.Entities.DepartmentModel;
using ShiftYar.Domain.Entities.ShiftModel;
using ShiftYar.Domain.Entities.ShiftRequestModel;
using ShiftYar.Domain.Entities.UserModel;
using System;
using System.Collections.Generic;
using Xunit;
using ShiftYar.Domain.Enums.ShiftRequestModel;
using static ShiftYar.Domain.Enums.DepartmentModel.DepartmentEnums;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;
using static ShiftYar.Domain.Enums.ShiftRequestModel.RequestStatus;
using static ShiftYar.Domain.Enums.UserModel.UserEnums;

namespace ShiftYar.Application.Tests;

public class ApprovedResponsibilityCapacityValidatorTests
{
    [Fact]
    public void IsResponsibilityBased_DetectsModeAndRequirementsCorrectly()
    {
        Assert.True(ApprovedResponsibilityCapacityValidator.IsResponsibilityBased(DepartmentStaffingMode.ResponsibilityBased));
        Assert.True(ApprovedResponsibilityCapacityValidator.IsResponsibilityBased(DepartmentStaffingMode.Hybrid));
        Assert.False(ApprovedResponsibilityCapacityValidator.IsResponsibilityBased(DepartmentStaffingMode.LevelBased));
        Assert.False(ApprovedResponsibilityCapacityValidator.IsResponsibilityBased(DepartmentStaffingMode.Simple));

        // اگر نقش در دپارتمان تعریف شده باشد حتی اگر حالت Simple باشد، تشخیص می‌دهد
        var responsibilities = new List<DepartmentResponsibility> { new() { Id = 1, Title = "اسکراب اول" } };
        Assert.True(ApprovedResponsibilityCapacityValidator.IsResponsibilityBased(DepartmentStaffingMode.Simple, null, responsibilities));

        // اگر شیفت نیازمندی مسئولیت داشته باشد
        var shifts = new List<Shift>
        {
            new() { RequiredResponsibilities = new List<ShiftRequiredResponsibility> { new() { DepartmentResponsibilityId = 1 } } }
        };
        Assert.True(ApprovedResponsibilityCapacityValidator.IsResponsibilityBased(DepartmentStaffingMode.LevelBased, shifts, null));
    }

    [Fact]
    public void CanUserPerformResponsibility_DefaultRoleAllowedForAll_SpecificRoleRequiresAssignment()
    {
        var user = new User
        {
            Id = 1,
            FullName = "علی رضایی",
            UserResponsibilities = new List<UserDepartmentResponsibility>
            {
                new() { DepartmentResponsibilityId = 101 } // فقط اسکراب اول
            }
        };

        // نقش پیش‌فرض (سیرکولر با شناسه 103) برای همه مجاز است
        Assert.True(ApprovedResponsibilityCapacityValidator.CanUserPerformResponsibility(user, 103, isDefault: true));

        // نقش غیرپیش‌فرض تخصیص‌یافته
        Assert.True(ApprovedResponsibilityCapacityValidator.CanUserPerformResponsibility(user, 101, isDefault: false));

        // نقش غیرپیش‌فرض غیرتخصیص‌یافته
        Assert.False(ApprovedResponsibilityCapacityValidator.CanUserPerformResponsibility(user, 102, isDefault: false));
    }

    [Fact]
    public void ResolveResponsibilityDemands_HolidayAndWeekdayMergedProperly()
    {
        var shift = new Shift
        {
            Id = 1,
            Label = ShiftLabel.Morning,
            RequiredSpecialties = new List<ShiftRequiredSpecialty>
            {
                new()
                {
                    DepartmentResponsibilityId = 1,
                    DepartmentResponsibility = new DepartmentResponsibility { Id = 1, Title = "اسکراب اول" },
                    RequiredTottalCount = 2,
                    HolidayRequiredTottalCount = 1
                }
            },
            RequiredResponsibilities = new List<ShiftRequiredResponsibility>
            {
                new()
                {
                    DepartmentResponsibilityId = 2,
                    DepartmentResponsibility = new DepartmentResponsibility { Id = 2, Title = "سیرکولر", IsDefault = true },
                    RequiredTotalCount = 3,
                    HolidayRequiredTotalCount = 2
                }
            }
        };

        var weekdayDemands = ApprovedResponsibilityCapacityValidator.ResolveResponsibilityDemands(shift, isHoliday: false);
        Assert.Equal(2, weekdayDemands.Count);
        Assert.Equal(2, weekdayDemands[0].RequiredTotalCount);
        Assert.Equal(3, weekdayDemands[1].RequiredTotalCount);

        var holidayDemands = ApprovedResponsibilityCapacityValidator.ResolveResponsibilityDemands(shift, isHoliday: true);
        Assert.Equal(1, holidayDemands[0].RequiredTotalCount);
        Assert.Equal(2, holidayDemands[1].RequiredTotalCount);
    }

    [Fact]
    public void ResolveShiftCapacity_CalculatesTotalShiftCapacity()
    {
        var shift = new Shift
        {
            Id = 1,
            Label = ShiftLabel.Morning,
            RequiredSpecialties = new List<ShiftRequiredSpecialty>
            {
                new() { DepartmentResponsibilityId = 1, RequiredTottalCount = 1 },
                new() { DepartmentResponsibilityId = 2, RequiredTottalCount = 2 }
            }
        };

        Assert.Equal(3, ApprovedResponsibilityCapacityValidator.ResolveShiftCapacity(shift, isHoliday: false));
    }

    [Fact]
    public void ValidateOnShiftCapacity_WhenShiftCapacityFull_ReturnsErrorMessage()
    {
        var shift = new Shift
        {
            Id = 1,
            Label = ShiftLabel.Morning,
            RequiredResponsibilities = new List<ShiftRequiredResponsibility>
            {
                new() { DepartmentResponsibilityId = 1, RequiredTotalCount = 2 }
            }
        };

        var user = new User { Id = 3, FullName = "سارا احمدی" };
        var approved = new List<ShiftRequest>
        {
            new() { Id = 10, UserId = 1, ShiftLabel = ShiftLabel.Morning, RequestDate = new DateTime(2026, 10, 10), User = new User { FullName = "علی رضایی" } },
            new() { Id = 11, UserId = 2, ShiftLabel = ShiftLabel.Morning, RequestDate = new DateTime(2026, 10, 10), User = new User { FullName = "مریم محمدی" } }
        };

        var error = ApprovedResponsibilityCapacityValidator.ValidateOnShiftCapacity(
            new[] { shift },
            ShiftLabel.Morning,
            new DateTime(2026, 10, 10),
            isHoliday: false,
            user,
            new List<DepartmentResponsibility>(),
            approved,
            new List<User>());

        Assert.NotNull(error);
        Assert.Contains("ظرفیت شیفت صبح", error);
        Assert.Contains("تکمیل شده است", error);
        Assert.Contains("2 درخواست حضور تأییدشده", error);
    }

    [Fact]
    public void ValidateOnShiftCapacity_WhenUserLacksAnyRequiredRole_ReturnsErrorMessage()
    {
        var shift = new Shift
        {
            Id = 1,
            Label = ShiftLabel.Morning,
            RequiredResponsibilities = new List<ShiftRequiredResponsibility>
            {
                new()
                {
                    DepartmentResponsibilityId = 101,
                    DepartmentResponsibility = new DepartmentResponsibility { Id = 101, Title = "اسکراب اول", IsDefault = false },
                    RequiredTotalCount = 2
                }
            }
        };

        var user = new User
        {
            Id = 5,
            FullName = "رضا کاظمی",
            UserResponsibilities = new List<UserDepartmentResponsibility>() // هیچ نقشی ندارد و نقش پیش‌فرضی هم نیست
        };

        var error = ApprovedResponsibilityCapacityValidator.ValidateOnShiftCapacity(
            new[] { shift },
            ShiftLabel.Morning,
            new DateTime(2026, 10, 10),
            isHoliday: false,
            user,
            new List<DepartmentResponsibility>(),
            new List<ShiftRequest>(),
            new List<User>());

        Assert.NotNull(error);
        Assert.Contains("صلاحیت هیچ‌یک از نقش‌های مورد نیاز شیفت صبح", error);
        Assert.Contains("اسکراب اول", error);
    }

    [Fact]
    public void ValidateOnShiftCapacity_WhenRoleSlotsSaturatedByOtherUsers_ReturnsErrorMessage()
    {
        // شیفت نیاز دارد به: ۱ اسکراب اول (اولویت ۱۰) و ۱ سیرکولر (پیش‌فرض)
        var shift = new Shift
        {
            Id = 1,
            Label = ShiftLabel.Morning,
            RequiredResponsibilities = new List<ShiftRequiredResponsibility>
            {
                new()
                {
                    DepartmentResponsibilityId = 101,
                    DepartmentResponsibility = new DepartmentResponsibility { Id = 101, Title = "اسکراب اول", Priority = 10, IsDefault = false },
                    RequiredTotalCount = 1
                },
                new()
                {
                    DepartmentResponsibilityId = 102,
                    DepartmentResponsibility = new DepartmentResponsibility { Id = 102, Title = "سیرکولر", Priority = 1, IsDefault = true },
                    RequiredTotalCount = 1
                }
            }
        };

        // کاربر ۱ (فقط سیرکولر، بدون اسکراب) از قبل تأیید شده است
        var user1 = new User { Id = 1, FullName = "کاربر اول", UserResponsibilities = new List<UserDepartmentResponsibility>() };
        // کاربر ۲ (او هم فقط سیرکولر، بدون اسکراب) متقاضی حضور است
        var user2 = new User { Id = 2, FullName = "کاربر دوم", UserResponsibilities = new List<UserDepartmentResponsibility>() };

        var approved = new List<ShiftRequest>
        {
            new() { Id = 10, UserId = 1, ShiftLabel = ShiftLabel.Morning, RequestDate = new DateTime(2026, 10, 10), User = user1 }
        };

        var deptUsers = new List<User> { user1, user2 };

        var error = ApprovedResponsibilityCapacityValidator.ValidateOnShiftCapacity(
            new[] { shift },
            ShiftLabel.Morning,
            new DateTime(2026, 10, 10),
            isHoliday: false,
            user2,
            new List<DepartmentResponsibility>(),
            approved,
            deptUsers);

        Assert.NotNull(error);
        Assert.Contains("ظرفیت نقش‌ها و مسئولیت‌های قابل پوشش توسط کاربر", error);
        Assert.Contains("تکمیل شده است", error);
        Assert.Contains("کسر نیرو در نقش‌های تخصصی شیفت", error);
    }

    [Fact]
    public void ValidateOnShiftCapacity_WhenValidRoleAssignmentPossible_ReturnsNull()
    {
        // شیفت نیاز دارد به: ۱ اسکراب اول و ۱ سیرکولر
        var shift = new Shift
        {
            Id = 1,
            Label = ShiftLabel.Morning,
            RequiredResponsibilities = new List<ShiftRequiredResponsibility>
            {
                new()
                {
                    DepartmentResponsibilityId = 101,
                    DepartmentResponsibility = new DepartmentResponsibility { Id = 101, Title = "اسکراب اول", Priority = 10, IsDefault = false },
                    RequiredTotalCount = 1
                },
                new()
                {
                    DepartmentResponsibilityId = 102,
                    DepartmentResponsibility = new DepartmentResponsibility { Id = 102, Title = "سیرکولر", Priority = 1, IsDefault = true },
                    RequiredTotalCount = 1
                }
            }
        };

        // کاربر ۱ (اسکراب اول) از قبل تأیید شده است
        var user1 = new User
        {
            Id = 1,
            FullName = "علی (اسکراب کار)",
            UserResponsibilities = new List<UserDepartmentResponsibility> { new() { DepartmentResponsibilityId = 101 } }
        };
        // کاربر ۲ (سیرکولر) متقاضی حضور است
        var user2 = new User
        {
            Id = 2,
            FullName = "مریم (سیرکولر)",
            UserResponsibilities = new List<UserDepartmentResponsibility>()
        };

        var approved = new List<ShiftRequest>
        {
            new() { Id = 10, UserId = 1, ShiftLabel = ShiftLabel.Morning, RequestDate = new DateTime(2026, 10, 10), User = user1 }
        };

        var error = ApprovedResponsibilityCapacityValidator.ValidateOnShiftCapacity(
            new[] { shift },
            ShiftLabel.Morning,
            new DateTime(2026, 10, 10),
            isHoliday: false,
            user2,
            new List<DepartmentResponsibility>(),
            approved,
            new List<User> { user1, user2 });

        Assert.Null(error);
    }

    [Fact]
    public void ValidateDailyLeaveCapacity_WhenOverallLeaveCapacityExceeded_ReturnsErrorMessage()
    {
        var shifts = new List<Shift>
        {
            new()
            {
                Label = ShiftLabel.Morning,
                RequiredResponsibilities = new List<ShiftRequiredResponsibility> { new() { DepartmentResponsibilityId = 1, RequiredTotalCount = 4 } }
            },
            new()
            {
                Label = ShiftLabel.Night,
                RequiredResponsibilities = new List<ShiftRequiredResponsibility> { new() { DepartmentResponsibilityId = 1, RequiredTotalCount = 2 } }
            }
        };

        // کل پرسنل فعال: ۷ نفر. نیاز امروز: ۶ نفر. استراحت شب قبل: ۲ نفر. حداقل لازم: ۸ نفر > ۷ نفر فعال
        // سقف مرخصی = max(0, 7 - 8) = 0
        var users = new List<User>();
        for (var i = 1; i <= 7; i++)
        {
            users.Add(new User { Id = i, FullName = $"کاربر {i}" });
        }

        var requestingUser = users[0];

        var error = ApprovedResponsibilityCapacityValidator.ValidateDailyLeaveCapacity(
            shifts,
            new DateTime(2026, 10, 10),
            isHolidayToday: false,
            isHolidayYesterday: false,
            requestingUser,
            new List<DepartmentResponsibility>(),
            new List<ShiftRequest>(),
            users);

        Assert.NotNull(error);
        Assert.Contains("به دلیل محدودیت شدید نیرو در تاریخ", error);
        Assert.Contains("امکان اعطای مرخصی وجود ندارد", error);
    }

    [Fact]
    public void ValidateDailyLeaveCapacity_WhenRoleSpecificCapacityExceeded_ReturnsRoleShortageErrorMessage()
    {
        var respScrub = new DepartmentResponsibility { Id = 101, Title = "اسکراب اول", IsDefault = false };
        var respCirc = new DepartmentResponsibility { Id = 102, Title = "سیرکولر", IsDefault = true };

        var shifts = new List<Shift>
        {
            new()
            {
                Label = ShiftLabel.Morning,
                RequiredResponsibilities = new List<ShiftRequiredResponsibility>
                {
                    new() { DepartmentResponsibilityId = 101, DepartmentResponsibility = respScrub, RequiredTotalCount = 2 },
                    new() { DepartmentResponsibilityId = 102, DepartmentResponsibility = respCirc, RequiredTotalCount = 2 }
                }
            }
        };

        // کل پرسنل بخش: ۱۰ نفر (سقف کل مرخصی کافی است: ۱۰ - ۴ = ۶ نفر)
        // اما فقط ۲ نفر صلاحیت اسکراب اول دارند (کاربر ۱ و کاربر ۲)
        // نیاز شیفت‌های امروز برای اسکراب اول: ۲ نفر
        // حداقل مورد نیاز اسکراب اول: ۲ نفر
        // حداکثر سقف مجاز مرخصی اسکراب اول: max(0, 2 - 2) = 0
        var user1 = new User
        {
            Id = 1,
            FullName = "علی (اسکراب)",
            UserResponsibilities = new List<UserDepartmentResponsibility> { new() { DepartmentResponsibilityId = 101 } }
        };
        var user2 = new User
        {
            Id = 2,
            FullName = "رضا (اسکراب)",
            UserResponsibilities = new List<UserDepartmentResponsibility> { new() { DepartmentResponsibilityId = 101 } }
        };

        var allUsers = new List<User> { user1, user2 };
        for (var i = 3; i <= 10; i++)
        {
            allUsers.Add(new User { Id = i, FullName = $"پرسنل {i}" });
        }

        var error = ApprovedResponsibilityCapacityValidator.ValidateDailyLeaveCapacity(
            shifts,
            new DateTime(2026, 10, 10),
            isHolidayToday: false,
            isHolidayYesterday: false,
            user1,
            new List<DepartmentResponsibility> { respScrub, respCirc },
            new List<ShiftRequest>(),
            allUsers);

        Assert.NotNull(error);
        Assert.Contains("به دلیل محدودیت نیرو برای مسئولیت «اسکراب اول»", error);
        Assert.Contains("امکان اعطای مرخصی وجود ندارد", error);
        Assert.Contains("حداقل 2 نفر نیروی واجد شرایط", error);
    }

    [Fact]
    public void ValidateDailyLeaveCapacity_WhenRoleLeaveWithinCapacity_ReturnsNull()
    {
        var respScrub = new DepartmentResponsibility { Id = 101, Title = "اسکراب اول", IsDefault = false };

        var shifts = new List<Shift>
        {
            new()
            {
                Label = ShiftLabel.Morning,
                RequiredResponsibilities = new List<ShiftRequiredResponsibility>
                {
                    new() { DepartmentResponsibilityId = 101, DepartmentResponsibility = respScrub, RequiredTotalCount = 1 }
                }
            }
        };

        // ۳ نفر اسکراب اول داریم، نیاز شیفت ۱ نفر است => سقف مجاز مرخصی اسکراب اول = ۲ نفر
        var user1 = new User { Id = 1, FullName = "علی", UserResponsibilities = new List<UserDepartmentResponsibility> { new() { DepartmentResponsibilityId = 101 } } };
        var user2 = new User { Id = 2, FullName = "رضا", UserResponsibilities = new List<UserDepartmentResponsibility> { new() { DepartmentResponsibilityId = 101 } } };
        var user3 = new User { Id = 3, FullName = "سارا", UserResponsibilities = new List<UserDepartmentResponsibility> { new() { DepartmentResponsibilityId = 101 } } };

        var allUsers = new List<User> { user1, user2, user3 };

        var error = ApprovedResponsibilityCapacityValidator.ValidateDailyLeaveCapacity(
            shifts,
            new DateTime(2026, 10, 10),
            isHolidayToday: false,
            isHolidayYesterday: false,
            user1,
            new List<DepartmentResponsibility> { respScrub },
            new List<ShiftRequest>(),
            allUsers);

        Assert.Null(error);
    }

    [Fact]
    public void ValidateDailyLeaveCapacity_Department3_OpenHeartSurgery_WithLongShiftPermissions_AllowsLeave()
    {
        // شبیه‌سازی کامل سناریوی دپارتمان ۳ (اتاق عمل قلب باز):
        // ۱۲ نفر پرسنل فعال
        // مجموعاً ۱۲ شیفت در روز (۷ صبح، ۵ عصر) و بدون شیفت شب فعال در این روز (استراحت شب قبل: ۰ نفر)
        // ۴ نفر از پرسنل دارای مجوز شیفت لانگ (صبح+عصر: MorningEveningSameDay) هستند.
        var shifts = new List<Shift>
        {
            new()
            {
                Label = ShiftLabel.Morning,
                RequiredResponsibilities = new List<ShiftRequiredResponsibility>
                {
                    new() { DepartmentResponsibilityId = 1, RequiredTotalCount = 7 }
                }
            },
            new()
            {
                Label = ShiftLabel.Evening,
                RequiredResponsibilities = new List<ShiftRequiredResponsibility>
                {
                    new() { DepartmentResponsibilityId = 1, RequiredTotalCount = 5 }
                }
            }
        };

        var users = new List<User>();
        // کاربران ۱ تا ۴: دارای مجوز شیفت لانگ صبح+عصر
        for (var i = 1; i <= 4; i++)
        {
            users.Add(new User
            {
                Id = i,
                FullName = $"پرسنل لانگ {i}",
                AllowedShiftPermissions = UserShiftPermission.Morning | UserShiftPermission.Evening | UserShiftPermission.MorningEveningSameDay
            });
        }
        // کاربران ۵ تا ۱۲: پرسنل عادی بدون شیفت لانگ
        for (var i = 5; i <= 12; i++)
        {
            users.Add(new User
            {
                Id = i,
                FullName = $"پرسنل عادی {i}",
                AllowedShiftPermissions = UserShiftPermission.Morning | UserShiftPermission.Evening | UserShiftPermission.Night
            });
        }

        var requestingUser = users[11]; // کاربر ۱۲ درخواست مرخصی داده است

        // ۰. اگر تنظیم «حداکثر شیفت در روز» دپارتمان روی ۱ باشد، شیفت‌های لانگ فعال نمی‌شوند و پیام محدودیت شدید نیرو داده می‌شود
        var errorWithMax1 = ApprovedResponsibilityCapacityValidator.ValidateDailyLeaveCapacity(
            shifts,
            new DateTime(2026, 10, 28), // ۱۴۰۵/۰۸/۰۷
            isHolidayToday: false,
            isHolidayYesterday: false,
            requestingUser,
            new List<DepartmentResponsibility>(),
            new List<ShiftRequest>(),
            users,
            maxShiftsPerDay: 1);

        Assert.NotNull(errorWithMax1);
        Assert.Contains("محدودیت شدید نیرو", errorWithMax1);
        Assert.Contains("حداقل ۱۲ نفر نیرو برای پوشش شیفت‌های امروز (۱۲ نفر) و استراحت اجباری شیفت شب روز قبل (0 نفر) الزامی است", errorWithMax1);

        // ۱. وقتی تنظیم «حداکثر شیفت در روز» دپارتمان روی ۲ باشد، شیفت‌های لانگ پرسنل واجد شرایط فعال می‌شوند و مرخصی تأیید می‌شود
        var error = ApprovedResponsibilityCapacityValidator.ValidateDailyLeaveCapacity(
            shifts,
            new DateTime(2026, 10, 28), // ۱۴۰۵/۰۸/۰۷
            isHolidayToday: false,
            isHolidayYesterday: false,
            requestingUser,
            new List<DepartmentResponsibility>(),
            new List<ShiftRequest>(),
            users,
            maxShiftsPerDay: 2);

        // نباید خطای «محدودیت شدید نیرو... حداقل ۱۲ نفر برای پوشش ۱۲ شیفت» بدهد، چون ۴ نفر لانگ می‌توانند ۴ شیفت را همپوشانی دهند
        Assert.Null(error);

        // ۲. اگر ۴ مرخصی قبلاً تأیید شده باشد و کاربر پنجم درخواست دهد (سقف مجاز ۴ نفر تکمیل شده است)
        var approvedRequests = new List<ShiftRequest>
        {
            new() { Id = 101, UserId = 9, User = users[8], RequestAction = RequestAction.RequestToBeOffShift, RequestType = RequestType.FullDay, RequestDate = new DateTime(2026, 10, 28) },
            new() { Id = 102, UserId = 10, User = users[9], RequestAction = RequestAction.RequestToBeOffShift, RequestType = RequestType.FullDay, RequestDate = new DateTime(2026, 10, 28) },
            new() { Id = 103, UserId = 11, User = users[10], RequestAction = RequestAction.RequestToBeOffShift, RequestType = RequestType.FullDay, RequestDate = new DateTime(2026, 10, 28) },
            new() { Id = 104, UserId = 8, User = users[7], RequestAction = RequestAction.RequestToBeOffShift, RequestType = RequestType.FullDay, RequestDate = new DateTime(2026, 10, 28) }
        };

        var exceededError = ApprovedResponsibilityCapacityValidator.ValidateDailyLeaveCapacity(
            shifts,
            new DateTime(2026, 10, 28),
            isHolidayToday: false,
            isHolidayYesterday: false,
            requestingUser,
            new List<DepartmentResponsibility>(),
            approvedRequests,
            users,
            maxShiftsPerDay: 2);

        Assert.NotNull(exceededError);
        Assert.Contains("ظرفیت مرخصی روزانه در تاریخ", exceededError);
        Assert.Contains("تکمیل شده است", exceededError);

        // ۳. اگر هر ۴ نفر پرسنل لانگ (کاربران ۱ تا ۴) مرخصی بگیرند، ۸ نفر باقیمانده بدون لانگ فقط ۸ شیفت را پوشش می‌دهند (کسری ۴ شیفت)
        var allLongUsersOff = new List<ShiftRequest>
        {
            new() { Id = 201, UserId = 1, User = users[0], RequestAction = RequestAction.RequestToBeOffShift, RequestType = RequestType.FullDay, RequestDate = new DateTime(2026, 10, 28) },
            new() { Id = 202, UserId = 2, User = users[1], RequestAction = RequestAction.RequestToBeOffShift, RequestType = RequestType.FullDay, RequestDate = new DateTime(2026, 10, 28) },
            new() { Id = 203, UserId = 3, User = users[2], RequestAction = RequestAction.RequestToBeOffShift, RequestType = RequestType.FullDay, RequestDate = new DateTime(2026, 10, 28) }
        };

        var fourthLongUser = users[3]; // کاربر ۴ که چهارمین نفر لانگ است
        var coverageShortageError = ApprovedResponsibilityCapacityValidator.ValidateDailyLeaveCapacity(
            shifts,
            new DateTime(2026, 10, 28),
            isHolidayToday: false,
            isHolidayYesterday: false,
            fourthLongUser,
            new List<DepartmentResponsibility>(),
            allLongUsersOff,
            users,
            maxShiftsPerDay: 2);

        Assert.NotNull(coverageShortageError);
        Assert.Contains("امکان تأیید مرخصی در تاریخ", coverageShortageError);
        Assert.Contains("پرسنل حاضر حداکثر توانایی پوشش ۸ شیفت را دارند", coverageShortageError);
    }
}

