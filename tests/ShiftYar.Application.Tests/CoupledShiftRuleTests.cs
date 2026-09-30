#nullable enable
using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.DTOs.UserModel;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using ShiftYar.Application.Features.UserModel.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;
using static ShiftYar.Domain.Enums.UserModel.UserEnums;

namespace ShiftYar.Application.Tests;

public class CoupledShiftRuleTests
{
    [Theory]
    [InlineData("MorningRequiresEvening", ShiftLabel.Morning, ShiftLabel.Evening)]
    [InlineData("MorningRequiresNight", ShiftLabel.Morning, ShiftLabel.Night)]
    [InlineData("EveningRequiresMorning", ShiftLabel.Evening, ShiftLabel.Morning)]
    [InlineData("NightRequiresMorning", ShiftLabel.Night, ShiftLabel.Morning)]
    public void UserConstraint_GetRequiredCoupledShift_ReturnsCorrectPairedShift(string propertyName, ShiftLabel trigger, ShiftLabel expectedPair)
    {
        var constraint = new UserConstraint { UserId = 1, UserName = "User1" };
        typeof(UserConstraint).GetProperty(propertyName)?.SetValue(constraint, true);

        Assert.True(constraint.HasCoupledShiftRules);
        Assert.Equal(expectedPair, constraint.GetRequiredCoupledShift(trigger));

        // Other shifts should not trigger anything
        var otherLabels = new[] { ShiftLabel.Morning, ShiftLabel.Evening, ShiftLabel.Night }
            .Where(l => l != trigger);
        foreach (var other in otherLabels)
        {
            Assert.Null(constraint.GetRequiredCoupledShift(other));
        }
    }

    [Fact]
    public void ShiftEligibilityResolver_ApplyPermissionsToUserConstraint_ExpandsPermissionsForCoupledRules()
    {
        var eveningUser = new UserConstraint
        {
            UserId = 1,
            AllowedShiftPermissions = UserShiftPermission.Morning,
            MorningRequiresEvening = true
        };
        ShiftEligibilityResolver.ApplyPermissionsToUserConstraint(eveningUser, eveningUser.AllowedShiftPermissions);
        Assert.True(eveningUser.AllowedShiftPermissions.HasFlag(UserShiftPermission.Morning));
        Assert.True(eveningUser.AllowedShiftPermissions.HasFlag(UserShiftPermission.Evening));
        Assert.True(eveningUser.AllowedShiftPermissions.HasFlag(UserShiftPermission.MorningEveningSameDay));

        var nightUser = new UserConstraint
        {
            UserId = 2,
            AllowedShiftPermissions = UserShiftPermission.Night,
            NightRequiresMorning = true
        };
        ShiftEligibilityResolver.ApplyPermissionsToUserConstraint(nightUser, nightUser.AllowedShiftPermissions);
        Assert.True(nightUser.AllowedShiftPermissions.HasFlag(UserShiftPermission.Morning));
        Assert.True(nightUser.AllowedShiftPermissions.HasFlag(UserShiftPermission.Night));
        Assert.True(nightUser.AllowedShiftPermissions.HasFlag(UserShiftPermission.MorningNightSameDay));
    }

    [Fact]
    public void CoupledShiftRuleGuard_Enforce_AddsPairedShiftWhenTriggerIsPresent()
    {
        var date = new DateTime(2026, 8, 23);
        var constraints = new ShiftConstraints
        {
            StartDate = date,
            EndDate = date.AddDays(1),
            ShiftRequirements = new List<ShiftRequirement>
            {
                new() { ShiftId = 101, ShiftLabel = ShiftLabel.Morning },
                new() { ShiftId = 102, ShiftLabel = ShiftLabel.Evening }
            },
            UserConstraints = new List<UserConstraint>
            {
                new()
                {
                    UserId = 1,
                    UserName = "User1",
                    MorningRequiresEvening = true,
                    AllowedShiftPermissions = UserShiftPermission.Morning | UserShiftPermission.Evening | UserShiftPermission.MorningEveningSameDay
                }
            }
        };

        var solution = new ShiftSolution();
        // Give morning shift to user 1
        solution.AddAssignment(1, 101, date, ShiftLabel.Morning);

        // Before guard: missing evening
        var violationsBefore = CoupledShiftRuleGuard.GetViolations(solution, constraints);
        Assert.Single(violationsBefore);
        Assert.Contains("نقض شیفت مقید", violationsBefore[0]);
        Assert.Contains("عصر", violationsBefore[0]);

        // Enforce guard
        CoupledShiftRuleGuard.Enforce(solution, constraints);

        // After guard: evening is automatically added
        var assignments = solution.GetUserAssignments(1, date);
        Assert.Equal(2, assignments.Count);
        Assert.Contains(assignments, a => a.ShiftLabel == ShiftLabel.Morning);
        Assert.Contains(assignments, a => a.ShiftLabel == ShiftLabel.Evening);

        var violationsAfter = CoupledShiftRuleGuard.GetViolations(solution, constraints);
        Assert.Empty(violationsAfter);
    }

    [Fact]
    public void DailyDuplicateAssignmentGuard_WhenMaxShiftsPerDayIsOne_DoesNotStripCoupledShifts()
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
                    UserName = "User1",
                    IsActive = true,
                    MorningRequiresEvening = true,
                    AllowedShiftPermissions = UserShiftPermission.Morning | UserShiftPermission.Evening | UserShiftPermission.MorningEveningSameDay
                }
            }
        };

        var solution = new ShiftSolution();
        solution.AddAssignment(1, 101, date, ShiftLabel.Morning);
        solution.AddAssignment(1, 102, date, ShiftLabel.Evening);

        // Violations should be empty because coupled shift rule overrides MaxShiftsPerDay=1
        var violations = DailyDuplicateAssignmentGuard.GetViolations(solution, constraints);
        Assert.Empty(violations);

        // Strip duplicates should keep both shifts intact
        DailyDuplicateAssignmentGuard.StripDuplicates(solution, constraints);
        var assignments = solution.GetUserAssignments(1, date);
        Assert.Equal(2, assignments.Count);
    }

    [Fact]
    public void DailyDuplicateAssignmentGuard_WhenMaxShiftsPerDayIsOne_AllowsNightRequiresMorning()
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
                    UserName = "User1",
                    IsActive = true,
                    NightRequiresMorning = true,
                    AllowedShiftPermissions = UserShiftPermission.Morning | UserShiftPermission.Night | UserShiftPermission.MorningNightSameDay
                }
            }
        };

        var solution = new ShiftSolution();
        solution.AddAssignment(1, 101, date, ShiftLabel.Morning);
        solution.AddAssignment(1, 103, date, ShiftLabel.Night);

        var violations = DailyDuplicateAssignmentGuard.GetViolations(solution, constraints);
        Assert.Empty(violations);

        DailyDuplicateAssignmentGuard.StripDuplicates(solution, constraints);
        var assignments = solution.GetUserAssignments(1, date);
        Assert.Equal(2, assignments.Count);
    }

    [Fact]
    public void ShiftEligibilityGuard_WhenMaxShiftsPerDayIsOne_AllowsCoupledShifts()
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
                    UserName = "User1",
                    IsActive = true,
                    EveningRequiresMorning = true,
                    AllowedShiftPermissions = UserShiftPermission.Morning | UserShiftPermission.Evening | UserShiftPermission.MorningEveningSameDay
                }
            }
        };

        var solution = new ShiftSolution();
        solution.AddAssignment(1, 101, date, ShiftLabel.Morning);
        solution.AddAssignment(1, 102, date, ShiftLabel.Evening);

        var violations = ShiftEligibilityGuard.GetViolations(solution, constraints);
        Assert.Empty(violations);

        ShiftEligibilityGuard.StripIneligibleAssignments(solution, constraints);
        var assignments = solution.GetUserAssignments(1, date);
        Assert.Equal(2, assignments.Count);
    }

    [Fact]
    public void UserService_ValidateCoupledShiftRules_RejectsSimultaneousEveningAndNightRules()
    {
        var method = typeof(UserService).GetMethod("ValidateCoupledShiftRules", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        // Conflicting evening + night rule: MorningRequiresEvening + MorningRequiresNight
        var dto1 = new UserDtoAdd
        {
            MorningRequiresEvening = true,
            MorningRequiresNight = true
        };
        var result1 = (string?)method.Invoke(null, new object[] { dto1 });
        Assert.NotNull(result1);
        Assert.Contains("امکان فعال‌سازی هم‌زمان قوانین شیفت مقید عصر و شب", result1);

        // Conflicting: EveningRequiresMorning + NightRequiresMorning
        var dto2 = new UserDtoAdd
        {
            EveningRequiresMorning = true,
            NightRequiresMorning = true
        };
        var result2 = (string?)method.Invoke(null, new object[] { dto2 });
        Assert.NotNull(result2);
        Assert.Contains("امکان فعال‌سازی هم‌زمان قوانین شیفت مقید عصر و شب", result2);

        // Symmetric Morning + Evening is valid
        var dtoValid1 = new UserDtoAdd
        {
            MorningRequiresEvening = true,
            EveningRequiresMorning = true
        };
        var resultValid1 = (string?)method.Invoke(null, new object[] { dtoValid1 });
        Assert.Null(resultValid1);

        // Symmetric Morning + Night is valid
        var dtoValid2 = new UserDtoAdd
        {
            MorningRequiresNight = true,
            NightRequiresMorning = true
        };
        var resultValid2 = (string?)method.Invoke(null, new object[] { dtoValid2 });
        Assert.Null(resultValid2);
    }

    [Fact]
    public void SimulatedAnnealingScheduler_ApplyMandatoryConstraints_EnforcesCoupledShiftsWithMaxShiftsPerDayOne()
    {
        var date = new DateTime(2026, 8, 23);
        var user = new UserConstraint
        {
            UserId = 1,
            UserName = "User1",
            IsActive = true,
            MorningRequiresEvening = true,
            AllowedShiftPermissions = UserShiftPermission.Morning | UserShiftPermission.Evening | UserShiftPermission.MorningEveningSameDay
        };
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
            ShiftRequirements = new List<ShiftRequirement>
            {
                new() { ShiftId = 101, ShiftLabel = ShiftLabel.Morning, DurationHours = 6 },
                new() { ShiftId = 102, ShiftLabel = ShiftLabel.Evening, DurationHours = 6 }
            },
            UserConstraints = new List<UserConstraint> { user }
        };

        var solution = new ShiftSolution();
        solution.AddAssignment(1, 101, date, ShiftLabel.Morning);

        var scheduler = new SimulatedAnnealingScheduler(constraints, new SimulatedAnnealingParameters());
        scheduler.ApplyMandatoryConstraints(solution);

        var assignments = solution.GetUserAssignments(1, date);
        Assert.Equal(2, assignments.Count);
        Assert.Contains(assignments, a => a.ShiftLabel == ShiftLabel.Morning);
        Assert.Contains(assignments, a => a.ShiftLabel == ShiftLabel.Evening);
        Assert.Empty(DailyDuplicateAssignmentGuard.GetViolations(solution, constraints));
        Assert.Empty(CoupledShiftRuleGuard.GetViolations(solution, constraints));
    }

    [Fact]
    public void DailyDuplicateAssignmentGuard_WhenNightRequiresMorning_StripsUncoupledMorningEvening_WhenMaxShiftsPerDayIsOne()
    {
        var date = new DateTime(2026, 10, 11); // یکشنبه 19 مهر 1405
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
                    UserName = "آفرین نورکرمی",
                    IsActive = true,
                    NightRequiresMorning = true,
                    // حتی اگر کاربر از قبل مجوزها را داشته باشد، چون قانون مقید فقط شب-صبح است
                    AllowedShiftPermissions = UserShiftPermission.Morning | UserShiftPermission.Evening | UserShiftPermission.Night | UserShiftPermission.MorningNightSameDay
                }
            }
        };

        var solution = new ShiftSolution();
        solution.AddAssignment(1, 101, date, ShiftLabel.Morning);
        solution.AddAssignment(1, 102, date, ShiftLabel.Evening);

        // ترکیب صبح+عصر برای کاربری که فقط شب-صبح دارد در بخش با سقف ۱ شیفت، تخلف محسوب می‌شود
        var violations = DailyDuplicateAssignmentGuard.GetViolations(solution, constraints);
        Assert.NotEmpty(violations);

        // پس از StripDuplicates، یکی از شیفت‌ها باید حذف شود و کاربر فقط ۱ شیفت داشته باشد
        DailyDuplicateAssignmentGuard.StripDuplicates(solution, constraints);
        var assignments = solution.GetUserAssignments(1, date);
        Assert.Single(assignments);
    }

    [Fact]
    public void ShiftEligibilityResolver_WhenNightRequiresMorning_RejectsAddingEveningToMorning_WhenMaxShiftsPerDayIsOne()
    {
        var date = new DateTime(2026, 10, 11);
        var user = new UserConstraint
        {
            UserId = 1,
            UserName = "آفرین نورکرمی",
            IsActive = true,
            NightRequiresMorning = true,
            AllowedShiftPermissions = UserShiftPermission.Morning | UserShiftPermission.Evening | UserShiftPermission.Night | UserShiftPermission.MorningNightSameDay
        };

        // کاربر روی این تاریخ شیفت صبح دارد
        var existing = new List<ShiftLabel> { ShiftLabel.Morning };

        // افزودن عصر باید رد شود چون سقف دپارتمان ۱ است و کاربر فقط مجوز جفت شب+صبح دارد
        var canAddEvening = ShiftEligibilityResolver.IsAssignmentAllowed(
            user,
            existing,
            ShiftLabel.Evening,
            maxShiftsPerDay: 1,
            forbidDuplicateLabels: true,
            date: date);

        Assert.False(canAddEvening);

        // اما افزودن شب باید مجاز باشد چون جفت شب+صبح مجاز است
        var canAddNight = ShiftEligibilityResolver.IsAssignmentAllowed(
            user,
            existing,
            ShiftLabel.Night,
            maxShiftsPerDay: 1,
            forbidDuplicateLabels: true,
            date: date);

        Assert.True(canAddNight);
    }

    [Fact]
    public void ShiftEligibilityResolver_ApplyPermissionsToUserConstraint_ClearsOpposingSameDayCombination()
    {
        var nightUser = new UserConstraint
        {
            UserId = 1,
            ShiftType = ShiftTypes.RotatingShift,
            ShiftSubType = ShiftSubTypes.ThreeShifts,
            NightRequiresMorning = true
        };

        ShiftEligibilityResolver.ApplyPermissionsToUserConstraint(nightUser, null);
        Assert.True(nightUser.AllowedShiftPermissions.HasFlag(UserShiftPermission.MorningNightSameDay));
        Assert.False(nightUser.AllowedShiftPermissions.HasFlag(UserShiftPermission.MorningEveningSameDay));

        var eveningUser = new UserConstraint
        {
            UserId = 2,
            ShiftType = ShiftTypes.RotatingShift,
            ShiftSubType = ShiftSubTypes.ThreeShifts,
            MorningRequiresEvening = true
        };

        ShiftEligibilityResolver.ApplyPermissionsToUserConstraint(eveningUser, null);
        Assert.True(eveningUser.AllowedShiftPermissions.HasFlag(UserShiftPermission.MorningEveningSameDay));
        Assert.False(eveningUser.AllowedShiftPermissions.HasFlag(UserShiftPermission.MorningNightSameDay));
    }
}
