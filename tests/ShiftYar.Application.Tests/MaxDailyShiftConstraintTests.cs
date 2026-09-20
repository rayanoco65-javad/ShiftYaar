using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;
using static ShiftYar.Domain.Enums.UserModel.UserEnums;

namespace ShiftYar.Application.Tests;

public class MaxDailyShiftConstraintTests
{
    [Fact]
    public void DailyAssignmentRules_WhenMaxShiftsPerDayIsOne_RejectsAnySecondShift()
    {
        // صبح + عصر در سقف ۱ مجاز نیست
        Assert.False(DailyAssignmentRules.CanAddShift(
            new[] { ShiftLabel.Morning }, ShiftLabel.Evening, maxShiftsPerDay: 1));
        Assert.False(DailyAssignmentRules.IsValidDaySet(
            new[] { ShiftLabel.Morning, ShiftLabel.Evening }, maxShiftsPerDay: 1));

        // صبح + شب در سقف ۱ مجاز نیست
        Assert.False(DailyAssignmentRules.CanAddShift(
            new[] { ShiftLabel.Morning }, ShiftLabel.Night, maxShiftsPerDay: 1));
        Assert.False(DailyAssignmentRules.IsValidDaySet(
            new[] { ShiftLabel.Morning, ShiftLabel.Night }, maxShiftsPerDay: 1));

        // شیفت تکی در سقف ۱ مجاز است
        Assert.True(DailyAssignmentRules.CanAddShift(
            Array.Empty<ShiftLabel>(), ShiftLabel.Morning, maxShiftsPerDay: 1));
        Assert.True(DailyAssignmentRules.IsValidDaySet(
            new[] { ShiftLabel.Morning }, maxShiftsPerDay: 1));
    }

    [Fact]
    public void DailyDuplicateAssignmentGuard_WhenMaxShiftsPerDayIsOne_ReportsViolationForMorningAndEvening()
    {
        var date = new DateTime(2026, 8, 23); // معادل ۱ شهریور ۱۴۰۵
        var constraints = new ShiftConstraints
        {
            StartDate = date,
            EndDate = date.AddDays(1),
            HardRules = new HardRuleSet
            {
                EnforceMaxShiftsPerDay = true,
                ForbidDuplicateDailyAssignments = true
            },
            GlobalConstraints = new GlobalConstraints { MaxShiftsPerDay = 1 },
            UserConstraints = new List<UserConstraint>
            {
                new()
                {
                    UserId = 1,
                    UserName = "کاربر تستی",
                    IsActive = true,
                    AllowedShiftPermissions = UserShiftPermission.Morning | UserShiftPermission.Evening | UserShiftPermission.MorningEveningSameDay
                }
            }
        };

        var solution = new ShiftSolution();
        solution.AddAssignment(1, 101, date, ShiftLabel.Morning);
        solution.AddAssignment(1, 102, date, ShiftLabel.Evening);

        var violations = DailyDuplicateAssignmentGuard.GetViolations(solution, constraints);

        Assert.NotEmpty(violations);
        Assert.Contains("invalid same-day shifts", violations[0]);
    }

    [Fact]
    public void DailyDuplicateAssignmentGuard_StripDuplicates_RemovesSecondShiftWhenMaxShiftsPerDayIsOne()
    {
        var date = new DateTime(2026, 8, 23);
        var constraints = new ShiftConstraints
        {
            StartDate = date,
            EndDate = date.AddDays(1),
            HardRules = new HardRuleSet
            {
                EnforceMaxShiftsPerDay = true,
                ForbidDuplicateDailyAssignments = true
            },
            GlobalConstraints = new GlobalConstraints { MaxShiftsPerDay = 1 },
            UserConstraints = new List<UserConstraint>
            {
                new()
                {
                    UserId = 1,
                    UserName = "کاربر تستی",
                    IsActive = true,
                    AllowedShiftPermissions = UserShiftPermission.Morning | UserShiftPermission.Evening
                }
            }
        };

        var solution = new ShiftSolution();
        solution.AddAssignment(1, 101, date, ShiftLabel.Morning);
        solution.AddAssignment(1, 102, date, ShiftLabel.Evening);

        Assert.Equal(2, solution.GetUserAssignments(1, date).Count);

        DailyDuplicateAssignmentGuard.StripDuplicates(solution, constraints);

        Assert.Equal(1, solution.GetUserAssignments(1, date).Count);
    }

    [Fact]
    public void ShiftEligibilityGuard_WhenMaxShiftsPerDayIsOne_ReportsViolationForLongShift()
    {
        var date = new DateTime(2026, 8, 23);
        var constraints = new ShiftConstraints
        {
            StartDate = date,
            EndDate = date.AddDays(1),
            HardRules = new HardRuleSet
            {
                EnforceMaxShiftsPerDay = true,
                ForbidDuplicateDailyAssignments = true
            },
            GlobalConstraints = new GlobalConstraints { MaxShiftsPerDay = 1 },
            UserConstraints = new List<UserConstraint>
            {
                new()
                {
                    UserId = 1,
                    UserName = "کاربر تستی",
                    IsActive = true,
                    AllowedShiftPermissions = UserShiftPermission.Morning | UserShiftPermission.Evening
                }
            }
        };

        var solution = new ShiftSolution();
        solution.AddAssignment(1, 101, date, ShiftLabel.Morning);
        solution.AddAssignment(1, 102, date, ShiftLabel.Evening);

        var violations = ShiftEligibilityGuard.GetViolations(solution, constraints);

        Assert.NotEmpty(violations);
        Assert.Contains("سقف شیفت روزانه", violations[0]);
    }

    [Fact]
    public void ShiftCoverageGuard_WhenMaxShiftsPerDayIsOne_DoesNotAssignDoubleShiftEvenWithDeficit()
    {
        var date = new DateTime(2026, 8, 23);
        var user = new UserConstraint
        {
            UserId = 1,
            UserName = "کاربر تستی",
            IsActive = true,
            SpecialtyId = 10,
            ShiftType = ShiftTypes.RotatingShift,
            AllowedShiftPermissions = UserShiftPermission.Morning | UserShiftPermission.Evening | UserShiftPermission.MorningEveningSameDay
        };

        var shiftMorning = new ShiftRequirement
        {
            ShiftId = 1,
            ShiftLabel = ShiftLabel.Morning,
            SpecialtyRequirements = new List<SpecialtyRequirement>
            {
                new()
                {
                    SpecialtyId = 10,
                    RequiredTotalCount = 1
                }
            }
        };

        var shiftEvening = new ShiftRequirement
        {
            ShiftId = 2,
            ShiftLabel = ShiftLabel.Evening,
            SpecialtyRequirements = new List<SpecialtyRequirement>
            {
                new()
                {
                    SpecialtyId = 10,
                    RequiredTotalCount = 1
                }
            }
        };

        var constraints = new ShiftConstraints
        {
            StartDate = date,
            EndDate = date,
            UserConstraints = new List<UserConstraint> { user },
            ShiftRequirements = new List<ShiftRequirement> { shiftMorning, shiftEvening },
            HardRules = new HardRuleSet
            {
                EnforceMaxShiftsPerDay = true,
                ForbidDuplicateDailyAssignments = true,
                EnforceSpecialtyCapacity = true
            },
            GlobalConstraints = new GlobalConstraints { MaxShiftsPerDay = 1 }
        };

        var solution = new ShiftSolution();
        // انتساب شیفت صبح به کاربر
        solution.AddAssignment(user.UserId, shiftMorning.ShiftId, date, ShiftLabel.Morning);

        // تلاش برای پر کردن شیفت خالی عصر با شیفت دوبل
        ShiftCoverageGuard.ForceFillAllMissingCoverage(solution, constraints);

        // اطمینان از اینکه کاربر در همان روز تقویمی شمسی بیش از ۱ شیفت نگرفته است
        var userAssignmentsOnDate = solution.GetUserAssignments(user.UserId, date);
        Assert.Single(userAssignmentsOnDate);
        Assert.Equal(ShiftLabel.Morning, userAssignmentsOnDate[0].ShiftLabel);

        // وجود کسری باید گزارش شود نه اینکه سقف شکسته شود
        var underCapacity = ShiftCoverageGuard.GetUnderCapacityViolations(solution, constraints);
        Assert.NotEmpty(underCapacity);
    }

    [Fact]
    public void PersianCalendarGrouping_CorrectlyIdentifiesMultipleShiftsOnSameSolarDay()
    {
        // تاریخ شمسی ۱ شهریور ۱۴۰۵ معادل ۲۳ اوت ۲۰۲۶
        var date = DateConverter.ConvertToGregorianDate("1405/06/01");
        var persianDate = DateConverter.ConvertToPersianDate(date);

        Assert.Equal("1405/06/01", persianDate);

        var solution = new ShiftSolution();
        solution.AddAssignment(1, 101, date, ShiftLabel.Morning);
        solution.AddAssignment(1, 102, date, ShiftLabel.Evening);

        var grouped = solution.Assignments.Values
            .GroupBy(a => new
            {
                a.UserId,
                PersianDate = DateConverter.ConvertToPersianDate(a.Date)
            })
            .ToDictionary(g => g.Key.PersianDate, g => g.Count());

        Assert.True(grouped.ContainsKey("1405/06/01"));
        Assert.Equal(2, grouped["1405/06/01"]);
    }
}
