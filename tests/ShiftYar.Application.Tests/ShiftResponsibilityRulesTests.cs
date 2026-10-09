using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static ShiftYar.Domain.Enums.DepartmentModel.DepartmentEnums;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;
using static ShiftYar.Domain.Enums.UserModel.UserEnums;

namespace ShiftYar.Application.Tests;

public class ShiftResponsibilityRulesTests
{
    [Fact]
    public void ResponsibilityRequirement_ForDay_WeekdayAndHoliday()
    {
        var req = new ResponsibilityRequirement
        {
            DepartmentResponsibilityId = 1,
            ResponsibilityTitle = "اسکراب اول",
            RequiredMaleCount = 1,
            RequiredFemaleCount = 1,
            RequiredTotalCount = 2,
            HolidayRequiredMaleCount = 0,
            HolidayRequiredFemaleCount = 1,
            HolidayRequiredTotalCount = 1
        };

        var weekday = req.ForDay(isHoliday: false);
        Assert.Equal(1, weekday.RequiredMaleCount);
        Assert.Equal(1, weekday.RequiredFemaleCount);
        Assert.Equal(2, weekday.RequiredTotalCount);

        var holiday = req.ForDay(isHoliday: true);
        Assert.Equal(0, holiday.RequiredMaleCount);
        Assert.Equal(1, holiday.RequiredFemaleCount);
        Assert.Equal(1, holiday.RequiredTotalCount);
    }

    [Fact]
    public void CanPerformResponsibility_AllowsDefaultRoleForAnyUser_AndEnforcesSpecificRole()
    {
        var user = new UserConstraint
        {
            UserId = 1,
            UserName = "علی رضایی",
            ResponsibilityIds = new HashSet<int> { 101 } // فقط اسکراب اول
        };

        // نقش غیراستاندارد با شناسه ۱۰۱ -> مجاز
        Assert.True(user.CanPerformResponsibility(101, isDefault: false));

        // نقش غیراستاندارد با شناسه ۱۰۲ (اسکراب دوم) که کاربر ندارد -> غیرمجاز
        Assert.False(user.CanPerformResponsibility(102, isDefault: false));

        // نقش پیش‌فرض (سیرکولر با شناسه ۱۰۳) -> برای همه پرسنل مجاز است
        Assert.True(user.CanPerformResponsibility(103, isDefault: true));
    }

    [Fact]
    public void AssignResponsibilitiesToSlot_AssignsPrioritizedRolesAndFallbackDefault()
    {
        // آماده‌سازی کاربران
        var users = new List<UserConstraint>
        {
            new() { UserId = 1, UserName = "سارا", Gender = UserGender.Female, ResponsibilityIds = new HashSet<int> { 101, 102 } }, // اسکراب ۱ و ۲
            new() { UserId = 2, UserName = "رضا", Gender = UserGender.Male, ResponsibilityIds = new HashSet<int> { 102 } },       // اسکراب ۲
            new() { UserId = 3, UserName = "مریم", Gender = UserGender.Female, ResponsibilityIds = new HashSet<int>() },          // فقط سیرکولر (پیش‌فرض)
            new() { UserId = 4, UserName = "حسین", Gender = UserGender.Male, ResponsibilityIds = new HashSet<int>() }            // فقط سیرکولر (پیش‌فرض)
        };

        // نیازهای شیفت اتاق عمل:
        // ۱- اسکراب اول (اولویت ۱۰، تعداد ۱)
        // ۲- اسکراب دوم (اولویت ۵، تعداد ۱)
        // ۳- سیرکولر (پیش‌فرض، تعداد ۱ حداقل)
        var shiftReq = new ShiftRequirement
        {
            ShiftId = 1,
            ShiftLabel = ShiftLabel.Morning,
            ResponsibilityRequirements = new List<ResponsibilityRequirement>
            {
                new() { DepartmentResponsibilityId = 101, ResponsibilityTitle = "اسکراب اول", Priority = 10, RequiredTotalCount = 1 },
                new() { DepartmentResponsibilityId = 102, ResponsibilityTitle = "اسکراب دوم", Priority = 5, RequiredTotalCount = 1 },
                new() { DepartmentResponsibilityId = 103, ResponsibilityTitle = "سیرکولر", Priority = 1, IsDefault = true, RequiredTotalCount = 1 }
            }
        };

        var assignments = new List<SaShiftAssignment>
        {
            new() { UserId = 1, ShiftId = 1, Date = new DateTime(2026, 10, 10) },
            new() { UserId = 2, ShiftId = 1, Date = new DateTime(2026, 10, 10) },
            new() { UserId = 3, ShiftId = 1, Date = new DateTime(2026, 10, 10) },
            new() { UserId = 4, ShiftId = 1, Date = new DateTime(2026, 10, 10) }
        };

        ShiftResponsibilityRules.AssignResponsibilitiesToSlot(
            assignments,
            shiftReq,
            new DateTime(2026, 10, 10),
            isHoliday: false,
            users,
            defaultDepartmentResponsibilityId: 103);

        // کاربر ۱ تنها کسی بود که اسکراب ۱ بلد بود -> باید اسکراب ۱ بگیرد
        Assert.Equal(101, assignments.First(a => a.UserId == 1).ResponsibilityId);

        // کاربر ۲ اسکراب ۲ بلد بود -> باید اسکراب ۲ بگیرد
        Assert.Equal(102, assignments.First(a => a.UserId == 2).ResponsibilityId);

        // کاربران ۳ و ۴ باید نقش پیش‌فرض سیرکولر (۱۰۳) بگیرند
        Assert.Equal(103, assignments.First(a => a.UserId == 3).ResponsibilityId);
        Assert.Equal(103, assignments.First(a => a.UserId == 4).ResponsibilityId);
    }

    [Fact]
    public void AssignResponsibilitiesToSlot_RespectsGenderBreakdown()
    {
        var users = new List<UserConstraint>
        {
            new() { UserId = 1, UserName = "سارا", Gender = UserGender.Female, ResponsibilityIds = new HashSet<int> { 101 } },
            new() { UserId = 2, UserName = "علی", Gender = UserGender.Male, ResponsibilityIds = new HashSet<int> { 101 } }
        };

        // نیاز به ۱ نفر مرد برای اسکراب ۱
        var shiftReq = new ShiftRequirement
        {
            ShiftId = 1,
            ShiftLabel = ShiftLabel.Morning,
            ResponsibilityRequirements = new List<ResponsibilityRequirement>
            {
                new()
                {
                    DepartmentResponsibilityId = 101,
                    ResponsibilityTitle = "اسکراب اول",
                    Priority = 10,
                    RequiredMaleCount = 1,
                    RequiredFemaleCount = 0,
                    RequiredTotalCount = 1
                }
            }
        };

        var assignments = new List<SaShiftAssignment>
        {
            new() { UserId = 1, ShiftId = 1, Date = new DateTime(2026, 10, 10) },
            new() { UserId = 2, ShiftId = 1, Date = new DateTime(2026, 10, 10) }
        };

        ShiftResponsibilityRules.AssignResponsibilitiesToSlot(
            assignments,
            shiftReq,
            new DateTime(2026, 10, 10),
            isHoliday: false,
            users,
            defaultDepartmentResponsibilityId: 103);

        // سهمیه مرد بوده، پس کاربر ۲ (علی) باید اسکراب ۱ بگیرد
        Assert.Equal(101, assignments.First(a => a.UserId == 2).ResponsibilityId);
        // سارا نقش پیش‌فرض ۱۰۳ را می‌گیرد
        Assert.Equal(103, assignments.First(a => a.UserId == 1).ResponsibilityId);
    }

    [Fact]
    public void GetViolations_DetectsDeficits_WhenRequiredResponsibilityCannotBeCovered()
    {
        var users = new List<UserConstraint>
        {
            new() { UserId = 1, UserName = "کاربر عادی ۱", Gender = UserGender.Male, ResponsibilityIds = new HashSet<int>() },
            new() { UserId = 2, UserName = "کاربر عادی ۲", Gender = UserGender.Female, ResponsibilityIds = new HashSet<int>() }
        };

        var constraints = new ShiftConstraints
        {
            DepartmentId = 10,
            StaffingMode = DepartmentStaffingMode.ResponsibilityBased,
            StartDate = new DateTime(2026, 10, 10),
            EndDate = new DateTime(2026, 10, 10),
            UserConstraints = users,
            ResponsibilityTitles = new Dictionary<int, string> { { 101, "اسکراب اول" } }
        };

        var shiftReq = new ShiftRequirement
        {
            ShiftId = 1,
            ShiftLabel = ShiftLabel.Morning,
            ResponsibilityRequirements = new List<ResponsibilityRequirement>
            {
                new() { DepartmentResponsibilityId = 101, ResponsibilityTitle = "اسکراب اول", RequiredTotalCount = 1 }
            }
        };
        constraints.ShiftRequirements.Add(shiftReq);

        var solution = new ShiftSolution();
        solution.Assignments["1_1_2026-10-10"] = new SaShiftAssignment { UserId = 1, ShiftId = 1, Date = new DateTime(2026, 10, 10) };
        solution.Assignments["2_1_2026-10-10"] = new SaShiftAssignment { UserId = 2, ShiftId = 1, Date = new DateTime(2026, 10, 10) };

        ShiftResponsibilityRules.AssignResponsibilitiesToAllSlots(solution, constraints);
        var violations = ShiftResponsibilityRules.GetViolations(solution, constraints);

        Assert.NotEmpty(violations);
        Assert.Contains("اسکراب اول", violations[0]);
    }

    [Fact]
    public void CalculateDeficitPenalty_ReturnsZeroWhenSatisfied_AndPenaltyWhenUnmet()
    {
        var users = new List<UserConstraint>
        {
            new() { UserId = 1, UserName = "متخصص ۱", Gender = UserGender.Male, ResponsibilityIds = new HashSet<int> { 101 } }
        };

        var constraints = new ShiftConstraints
        {
            DepartmentId = 10,
            StaffingMode = DepartmentStaffingMode.ResponsibilityBased,
            StartDate = new DateTime(2026, 10, 10),
            EndDate = new DateTime(2026, 10, 10),
            UserConstraints = users
        };

        var shiftReq = new ShiftRequirement
        {
            ShiftId = 1,
            ShiftLabel = ShiftLabel.Morning,
            ResponsibilityRequirements = new List<ResponsibilityRequirement>
            {
                new() { DepartmentResponsibilityId = 101, ResponsibilityTitle = "اسکراب اول", RequiredTotalCount = 1 }
            }
        };
        constraints.ShiftRequirements.Add(shiftReq);

        var satisfiedSolution = new ShiftSolution();
        satisfiedSolution.Assignments["1_1_2026-10-10"] = new SaShiftAssignment { UserId = 1, ShiftId = 1, Date = new DateTime(2026, 10, 10) };

        var satisfiedPenalty = ShiftResponsibilityRules.CalculateDeficitPenalty(satisfiedSolution, constraints);
        Assert.Equal(0, satisfiedPenalty);

        // راه‌حل خالی یا با کاربری که این مهارت را ندارد
        var emptySolution = new ShiftSolution();
        var deficitPenalty = ShiftResponsibilityRules.CalculateDeficitPenalty(emptySolution, constraints);
        Assert.True(deficitPenalty > 0);
    }

    [Fact]
    public void SimulatedAnnealingScheduler_ResponsibilityBasedScheduling_AssignsRolesToSlots()
    {
        var start = new DateTime(2026, 10, 10);
        var constraints = new ShiftConstraints
        {
            DepartmentId = 5,
            StaffingMode = DepartmentStaffingMode.ResponsibilityBased,
            StartDate = start,
            EndDate = start.AddDays(1), // ۲ روز
            DefaultDepartmentResponsibilityId = 103, // سیرکولر
            ResponsibilityTitles = new Dictionary<int, string>
            {
                { 101, "اسکراب اول" },
                { 102, "اسکراب دوم" },
                { 103, "سیرکولر" }
            },
            ShiftRequirements = new List<ShiftRequirement>
            {
                new()
                {
                    ShiftId = 1,
                    ShiftLabel = ShiftLabel.Morning,
                    DurationHours = 7,
                    ResponsibilityRequirements = new List<ResponsibilityRequirement>
                    {
                        new() { DepartmentResponsibilityId = 101, ResponsibilityTitle = "اسکراب اول", Priority = 10, RequiredTotalCount = 1 },
                        new() { DepartmentResponsibilityId = 102, ResponsibilityTitle = "اسکراب دوم", Priority = 5, RequiredTotalCount = 1 },
                        new() { DepartmentResponsibilityId = 103, ResponsibilityTitle = "سیرکولر", Priority = 1, IsDefault = true, RequiredTotalCount = 1 }
                    },
                    SpecialtyRequirements = new List<SpecialtyRequirement>
                    {
                        new() { SpecialtyId = 1, RequiredTotalCount = 3 }
                    }
                }
            },
            UserConstraints = new List<UserConstraint>
            {
                new() { UserId = 1, UserName = "اسکراب ۱ کار", SpecialtyId = 1, IsActive = true, ShiftType = ShiftTypes.RotatingShift, ResponsibilityIds = new HashSet<int> { 101 } },
                new() { UserId = 2, UserName = "اسکراب ۲ کار", SpecialtyId = 1, IsActive = true, ShiftType = ShiftTypes.RotatingShift, ResponsibilityIds = new HashSet<int> { 102 } },
                new() { UserId = 3, UserName = "سیرکولر کار", SpecialtyId = 1, IsActive = true, ShiftType = ShiftTypes.RotatingShift, ResponsibilityIds = new HashSet<int>() }
            },
            HardRules = new HardRuleSet
            {
                ForbidDuplicateDailyAssignments = true,
                EnforceMaxShiftsPerDay = true
            },
            GlobalConstraints = new GlobalConstraints { MaxShiftsPerDay = 1 }
        };

        var scheduler = new SimulatedAnnealingScheduler(constraints, new SimulatedAnnealingParameters
        {
            InitialTemperature = 100,
            FinalTemperature = 1,
            CoolingRate = 0.9,
            MaxIterations = 100
        });

        var solution = scheduler.Optimize();

        // بررسی انتساب روز اول
        var day1Assignments = solution.GetShiftAssignments(1, start).Where(a => !a.IsOnCall).ToList();
        Assert.Equal(3, day1Assignments.Count);

        // انتساب مسئولیت‌ها باید مقداردهی شده باشند
        var u1 = day1Assignments.FirstOrDefault(a => a.UserId == 1);
        var u2 = day1Assignments.FirstOrDefault(a => a.UserId == 2);
        var u3 = day1Assignments.FirstOrDefault(a => a.UserId == 3);

        Assert.NotNull(u1);
        Assert.NotNull(u2);
        Assert.NotNull(u3);

        Assert.Equal(101, u1.ResponsibilityId);
        Assert.Equal(102, u2.ResponsibilityId);
        Assert.Equal(103, u3.ResponsibilityId);

        // خطای عدم پوشش نباید وجود داشته باشد
        var violations = ShiftResponsibilityRules.GetViolations(solution, constraints);
        Assert.Empty(violations);
    }
}
