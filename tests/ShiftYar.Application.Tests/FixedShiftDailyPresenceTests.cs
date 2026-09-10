using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using Xunit;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;
using static ShiftYar.Domain.Enums.UserModel.UserEnums;

namespace ShiftYar.Application.Tests;

public class FixedShiftDailyPresenceTests
{
    private static SimulatedAnnealingParameters FastParameters => new()
    {
        InitialTemperature = 400,
        FinalTemperature = 0.1,
        CoolingRate = 0.93,
        MaxIterations = 1500,
        MaxIterationsWithoutImprovement = 250,
        PenaltyWeight = 1000
    };

    [Fact]
    public void FixedMorningUser_WorksEveryNonHolidayDay_DespiteRestRules()
    {
        var start = new DateTime(2026, 8, 23);
        var days = 7;
        var holiday = start.AddDays(5); // جمعه فرضی

        var fixedUser = new UserConstraint
        {
            UserId = 10,
            Gender = UserGender.Female,
            SpecialtyId = 10,
            IsActive = true,
            ShiftType = ShiftTypes.FixedShift,
            ShiftSubType = ShiftSubTypes.FixedMorning,
            AllowedShiftLabels = [ShiftLabel.Morning],
            // مقادیر سخت‌گیرانه که بدون استثنای فیکس، حضور روزانه را ناممکن می‌کرد
            MaxConsecutiveShifts = 3,
            MinRestDaysBetweenShifts = 1,
            MaxShiftsPerWeek = 5
        };

        var rotating1 = RotatingUser(11, UserGender.Male);
        var rotating2 = RotatingUser(12, UserGender.Female);
        var rotating3 = RotatingUser(13, UserGender.Male);

        var constraints = new ShiftConstraints
        {
            DepartmentId = 1,
            StartDate = start,
            EndDate = start.AddDays(days - 1),
            HolidayDates = [holiday.Date],
            UserConstraints = [fixedUser, rotating1, rotating2, rotating3],
            ShiftRequirements =
            [
                Shift(1, ShiftLabel.Morning, requiredTotal: 2),
                Shift(2, ShiftLabel.Evening, requiredTotal: 1),
                Shift(3, ShiftLabel.Night, requiredTotal: 1)
            ],
            HardRules = new HardRuleSet
            {
                ForbidDuplicateDailyAssignments = true,
                EnforceMaxShiftsPerDay = true,
                EnforceMinRestDays = true,
                EnforceMaxConsecutiveShifts = true,
                EnforceWeeklyMaxShifts = true,
                EnforceSpecialtyCapacity = true
            },
            GlobalConstraints = new GlobalConstraints { MaxShiftsPerDay = 1 }
        };

        // شبیه‌سازی LoadConstraints: پرسنل فیکس در همه روزهای غیرتعطیل باید شیفت خودش باشد
        // و روزهای تعطیل برایش عدم‌حضور سخت است
        for (var d = start.Date; d <= constraints.EndDate.Date; d = d.AddDays(1))
        {
            if (constraints.HolidayDates.Contains(d))
            {
                fixedUser.UnavailableDates.Add(d);
                continue;
            }

            fixedUser.RequiredShiftSlots.Add(new ShiftSlotConstraint
            {
                Date = d,
                ShiftLabel = ShiftLabel.Morning
            });
        }

        for (var run = 0; run < 5; run++)
        {
            var solution = new SimulatedAnnealingScheduler(constraints, FastParameters).Optimize();

            for (var d = start.Date; d <= constraints.EndDate.Date; d = d.AddDays(1))
            {
                var dayAssignments = solution.GetUserAssignments(fixedUser.UserId, d).ToList();
                if (constraints.HolidayDates.Contains(d))
                {
                    Assert.True(
                        dayAssignments.Count == 0,
                        $"Run {run}: fixed-morning user assigned on holiday {d:yyyy-MM-dd}");
                    continue;
                }

                Assert.True(
                    dayAssignments.Any(a => a.ShiftLabel == ShiftLabel.Morning && !a.IsOnCall),
                    $"Run {run}: fixed-morning user missing on {d:yyyy-MM-dd}");
            }

            // هیچ انتساب غیر صبح نباید داشته باشد
            Assert.All(
                solution.GetUserAllAssignments(fixedUser.UserId),
                a => Assert.Equal(ShiftLabel.Morning, a.ShiftLabel));

            Assert.Empty(ApprovedRequestGuard.GetUnmetViolations(solution, constraints));
        }
    }

    private static UserConstraint RotatingUser(int id, UserGender gender) => new()
    {
        UserId = id,
        Gender = gender,
        SpecialtyId = 10,
        IsActive = true,
        ShiftType = ShiftTypes.RotatingShift,
        ShiftSubType = ShiftSubTypes.ThreeShifts,
        AllowedShiftLabels = [ShiftLabel.Morning, ShiftLabel.Evening, ShiftLabel.Night],
        MaxConsecutiveShifts = 7,
        MinRestDaysBetweenShifts = 0,
        MaxShiftsPerWeek = 7
    };

    private static ShiftRequirement Shift(int id, ShiftLabel label, int requiredTotal) => new()
    {
        ShiftId = id,
        ShiftLabel = label,
        DepartmentId = 1,
        DurationHours = 8,
        SpecialtyRequirements =
        [
            new SpecialtyRequirement { SpecialtyId = 10, RequiredTotalCount = requiredTotal }
        ]
    };

    /// <summary>
    /// وقتی پرسنل فیکس صبح برای یک روز تعطیل درخواست شیفت تأییدشده (ON) دارد،
    /// باید در آن روز تعطیل در برنامه حضور داشته باشد.
    /// درخواست شیفت تأییدشده بر تعطیلی مقدم است.
    /// </summary>
    [Fact]
    public void FixedMorningUser_WithApprovedOnRequest_WorksOnHoliday()
    {
        var start = new DateTime(2026, 8, 23);
        var days = 7;
        var holiday = start.AddDays(5); // روز تعطیل

        var fixedUser = new UserConstraint
        {
            UserId = 10,
            Gender = UserGender.Female,
            SpecialtyId = 10,
            IsActive = true,
            ShiftType = ShiftTypes.FixedShift,
            ShiftSubType = ShiftSubTypes.FixedMorning,
            AllowedShiftLabels = [ShiftLabel.Morning],
            MaxConsecutiveShifts = 7,
            MinRestDaysBetweenShifts = 0,
            MaxShiftsPerWeek = 7
        };

        var rotating1 = RotatingUser(11, UserGender.Male);
        var rotating2 = RotatingUser(12, UserGender.Female);
        var rotating3 = RotatingUser(13, UserGender.Male);

        var constraints = new ShiftConstraints
        {
            DepartmentId = 1,
            StartDate = start,
            EndDate = start.AddDays(days - 1),
            HolidayDates = [holiday.Date],
            UserConstraints = [fixedUser, rotating1, rotating2, rotating3],
            ShiftRequirements =
            [
                Shift(1, ShiftLabel.Morning, requiredTotal: 2),
                Shift(2, ShiftLabel.Evening, requiredTotal: 1),
                Shift(3, ShiftLabel.Night, requiredTotal: 1)
            ],
            HardRules = new HardRuleSet
            {
                ForbidDuplicateDailyAssignments = true,
                EnforceMaxShiftsPerDay = true,
                EnforceMinRestDays = true,
                EnforceMaxConsecutiveShifts = true,
                EnforceWeeklyMaxShifts = true,
                EnforceSpecialtyCapacity = true
            },
            GlobalConstraints = new GlobalConstraints { MaxShiftsPerDay = 1 }
        };

        // شبیه‌سازی LoadConstraints: approved ON request برای روز تعطیل
        // مطابق کد LoadConstraintsAsync:
        // - ابتدا approved requests اعمال می‌شوند → RequiredShiftSlots برای تعطیل اضافه می‌شود
        // - سپس در حلقه تعطیلات: چون hasApprovedPresence=true، UnavailableDates اضافه نمی‌شود
        for (var d = start.Date; d <= constraints.EndDate.Date; d = d.AddDays(1))
        {
            // روز تعطیل: درخواست ON تأییدشده وجود دارد → slot اضافه شود، UnavailableDates اضافه نشود
            // سایر روزها: slot اجباری اضافه شود
            fixedUser.RequiredShiftSlots.Add(new ShiftSlotConstraint
            {
                Date = d,
                ShiftLabel = ShiftLabel.Morning
            });
            // توجه: در روز تعطیل، چون RequiredShiftSlots وجود دارد، UnavailableDates اضافه نمی‌شود
        }

        for (var run = 0; run < 5; run++)
        {
            var solution = new SimulatedAnnealingScheduler(constraints, FastParameters).Optimize();

            // روز تعطیل با درخواست ON: باید حضور داشته باشد
            var holidayAssignments = solution.GetUserAssignments(fixedUser.UserId, holiday).ToList();
            Assert.True(
                holidayAssignments.Any(a => a.ShiftLabel == ShiftLabel.Morning && !a.IsOnCall),
                $"Run {run}: fixed-morning user should be assigned on holiday {holiday:yyyy-MM-dd} due to approved ON request");

            // سایر روزها هم باید حضور داشته باشد
            for (var d = start.Date; d <= constraints.EndDate.Date; d = d.AddDays(1))
            {
                var dayAssignments = solution.GetUserAssignments(fixedUser.UserId, d).ToList();
                Assert.True(
                    dayAssignments.Any(a => a.ShiftLabel == ShiftLabel.Morning && !a.IsOnCall),
                    $"Run {run}: fixed-morning user missing on {d:yyyy-MM-dd}");
            }

            Assert.All(
                solution.GetUserAllAssignments(fixedUser.UserId),
                a => Assert.Equal(ShiftLabel.Morning, a.ShiftLabel));

            Assert.Empty(ApprovedRequestGuard.GetUnmetViolations(solution, constraints));
        }
    }

    /// <summary>
    /// کاربر گردشی با دسترسی محدود (مثلاً فقط عصر و شب، بدون مجوز صبح):
    /// در صورت داشتن درخواست شیفت صبح تأییدشده (ON)، باید در آن روز شیفت صبح به او تخصیص یابد
    /// و هیچ خطای عدم صلاحیت (ShiftEligibilityGuard) یا عدم تحقق درخواست ثبت نشود.
    /// </summary>
    [Fact]
    public void RotatingUser_WithApprovedRequestForNonPermittedShift_IsScheduledAndEligible()
    {
        var start = new DateTime(2026, 8, 23);
        var requestedDate = start.AddDays(2);

        // کاربری که طبق تنظیمات فقط مجاز به عصر و شب است
        var restrictedUser = new UserConstraint
        {
            UserId = 20,
            Gender = UserGender.Female,
            SpecialtyId = 10,
            IsActive = true,
            ShiftType = ShiftTypes.RotatingShift,
            ShiftSubType = ShiftSubTypes.ThreeShifts,
            AllowedShiftLabels = [ShiftLabel.Evening, ShiftLabel.Night],
            AllowedShiftPermissions = UserShiftPermission.Evening | UserShiftPermission.Night,
            MaxConsecutiveShifts = 7,
            MinRestDaysBetweenShifts = 0,
            MaxShiftsPerWeek = 7
        };

        // درخواست تأییدشده شیفت صبح در روز دوم
        restrictedUser.RequiredShiftSlots.Add(new ShiftSlotConstraint
        {
            Date = requestedDate,
            ShiftLabel = ShiftLabel.Morning
        });

        var rotating1 = RotatingUser(21, UserGender.Male);
        var rotating2 = RotatingUser(22, UserGender.Female);

        var constraints = new ShiftConstraints
        {
            DepartmentId = 1,
            StartDate = start,
            EndDate = start.AddDays(4),
            HolidayDates = [],
            UserConstraints = [restrictedUser, rotating1, rotating2],
            ShiftRequirements =
            [
                Shift(1, ShiftLabel.Morning, requiredTotal: 1),
                Shift(2, ShiftLabel.Evening, requiredTotal: 1),
                Shift(3, ShiftLabel.Night, requiredTotal: 1)
            ],
            HardRules = new HardRuleSet
            {
                ForbidDuplicateDailyAssignments = true,
                EnforceMaxShiftsPerDay = true,
                EnforceMinRestDays = true,
                EnforceMaxConsecutiveShifts = true,
                EnforceWeeklyMaxShifts = true,
                EnforceSpecialtyCapacity = true
            },
            GlobalConstraints = new GlobalConstraints { MaxShiftsPerDay = 1 }
        };

        var solution = new SimulatedAnnealingScheduler(constraints, FastParameters).Optimize();

        // بررسی انتساب: در تاریخ درخواست، کاربر باید حتماً صبح باشد
        var assigned = solution.GetUserAssignments(restrictedUser.UserId, requestedDate).ToList();
        Assert.True(
            assigned.Any(a => a.ShiftLabel == ShiftLabel.Morning),
            $"Restricted user must be assigned Morning on {requestedDate:yyyy-MM-dd} due to approved request");

        // بررسی عدم وجود نقض صلاحیت شیفت
        var eligibilityViolations = ShiftEligibilityGuard.GetViolations(solution, constraints);
        Assert.Empty(eligibilityViolations);

        // بررسی عدم وجود نقض درخواست‌های تایید شده
        var unmetViolations = ApprovedRequestGuard.GetUnmetViolations(solution, constraints);
        Assert.Empty(unmetViolations);
    }

    /// <summary>
    /// کاربر فیکس صبح با درخواست تأییدشده شیفت عصر در روز تعطیل:
    /// باید در روز تعطیل شیفت عصر تخصیص یابد بدون خطای عدم صلاحیت یا سلب انتساب.
    /// </summary>
    [Fact]
    public void FixedMorningUser_WithApprovedEveningRequestOnHoliday_WorksEveningOnHoliday()
    {
        var start = new DateTime(2026, 8, 23);
        var holiday = start.AddDays(3);

        var fixedUser = new UserConstraint
        {
            UserId = 30,
            Gender = UserGender.Female,
            SpecialtyId = 10,
            IsActive = true,
            ShiftType = ShiftTypes.FixedShift,
            ShiftSubType = ShiftSubTypes.FixedMorning,
            AllowedShiftLabels = [ShiftLabel.Morning],
            AllowedShiftPermissions = UserShiftPermission.Morning,
            MaxConsecutiveShifts = 7,
            MinRestDaysBetweenShifts = 0,
            MaxShiftsPerWeek = 7
        };

        // روز تعطیل: درخواست تایید شده عصر
        fixedUser.RequiredShiftSlots.Add(new ShiftSlotConstraint
        {
            Date = holiday,
            ShiftLabel = ShiftLabel.Evening
        });

        // سایر روزهای غیرتعطیل: صبح
        for (var d = start.Date; d <= start.AddDays(4); d = d.AddDays(1))
        {
            if (d == holiday.Date) continue;
            fixedUser.RequiredShiftSlots.Add(new ShiftSlotConstraint
            {
                Date = d,
                ShiftLabel = ShiftLabel.Morning
            });
        }

        var rotating1 = RotatingUser(31, UserGender.Male);
        var rotating2 = RotatingUser(32, UserGender.Female);

        var constraints = new ShiftConstraints
        {
            DepartmentId = 1,
            StartDate = start,
            EndDate = start.AddDays(4),
            HolidayDates = [holiday.Date],
            UserConstraints = [fixedUser, rotating1, rotating2],
            ShiftRequirements =
            [
                Shift(1, ShiftLabel.Morning, requiredTotal: 1),
                Shift(2, ShiftLabel.Evening, requiredTotal: 1),
                Shift(3, ShiftLabel.Night, requiredTotal: 1)
            ],
            HardRules = new HardRuleSet
            {
                ForbidDuplicateDailyAssignments = true,
                EnforceMaxShiftsPerDay = true,
                EnforceSpecialtyCapacity = true
            },
            GlobalConstraints = new GlobalConstraints { MaxShiftsPerDay = 1 }
        };

        var solution = new SimulatedAnnealingScheduler(constraints, FastParameters).Optimize();

        // بررسی انتساب روز تعطیل: باید عصر باشد
        var holidayAssignments = solution.GetUserAssignments(fixedUser.UserId, holiday).ToList();
        Assert.True(
            holidayAssignments.Any(a => a.ShiftLabel == ShiftLabel.Evening),
            "Fixed morning user should be assigned Evening on holiday due to approved request");

        // خطای عدم صلاحیت یا درخواست محقق نشده نباید داشته باشیم
        Assert.Empty(ShiftEligibilityGuard.GetViolations(solution, constraints));
        Assert.Empty(ApprovedRequestGuard.GetUnmetViolations(solution, constraints));
    }

    /// <summary>
    /// در روز تعطیل که ظرفیت شیفت مورد نظر 0 است، اگر کاربر درخواست تأییدشده داشته باشد،
    /// نباید با خطای OverCapacity متوقف شود یا انتساب حذف شود.
    /// </summary>
    [Fact]
    public void UserWithApprovedRequest_OnZeroCapacityHoliday_ScheduledSuccessfully()
    {
        var start = new DateTime(2026, 8, 23);
        var holiday = start.AddDays(1);

        var fixedUser = new UserConstraint
        {
            UserId = 40,
            Gender = UserGender.Female,
            SpecialtyId = 10,
            IsActive = true,
            ShiftType = ShiftTypes.FixedShift,
            ShiftSubType = ShiftSubTypes.FixedMorning,
            AllowedShiftLabels = [ShiftLabel.Morning],
            MaxConsecutiveShifts = 7,
            MinRestDaysBetweenShifts = 0,
            MaxShiftsPerWeek = 7
        };

        // درخواست تایید شده حضور در روز تعطیل
        fixedUser.RequiredShiftSlots.Add(new ShiftSlotConstraint
        {
            Date = holiday,
            ShiftLabel = ShiftLabel.Morning
        });

        var rotating1 = RotatingUser(41, UserGender.Male);

        // شیفت صبح در روز تعطیل ظرفیت 0 دارد (مثلاً HolidayRequiredTotalCount = 0)
        var morningShift = new ShiftRequirement
        {
            ShiftId = 1,
            ShiftLabel = ShiftLabel.Morning,
            DepartmentId = 1,
            DurationHours = 8,
            SpecialtyRequirements =
            [
                new SpecialtyRequirement
                {
                    SpecialtyId = 10,
                    RequiredTotalCount = 1,
                    HolidayRequiredTotalCount = 0
                }
            ]
        };

        var constraints = new ShiftConstraints
        {
            DepartmentId = 1,
            StartDate = start,
            EndDate = holiday,
            HolidayDates = [holiday.Date],
            UserConstraints = [fixedUser, rotating1],
            ShiftRequirements = [morningShift],
            HardRules = new HardRuleSet
            {
                ForbidDuplicateDailyAssignments = true,
                EnforceMaxShiftsPerDay = true,
                EnforceSpecialtyCapacity = true
            },
            GlobalConstraints = new GlobalConstraints { MaxShiftsPerDay = 1 }
        };

        var solution = new SimulatedAnnealingScheduler(constraints, FastParameters).Optimize();

        // کاربر فیکس باید در روز تعطیل صبح باشد علیرغم ظرفیت صفر روز تعطیل
        var holidayAssignments = solution.GetUserAssignments(fixedUser.UserId, holiday).ToList();
        Assert.True(
            holidayAssignments.Any(a => a.ShiftLabel == ShiftLabel.Morning),
            "User should be assigned Morning on zero-capacity holiday due to approved request");

        Assert.Empty(ShiftCoverageGuard.GetOverCapacityViolations(solution, constraints));
        Assert.Empty(ApprovedRequestGuard.GetUnmetViolations(solution, constraints));
    }
}

