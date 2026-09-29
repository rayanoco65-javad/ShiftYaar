#nullable enable
using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.DTOs.UserModel;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using System;
using System.Collections.Generic;
using Xunit;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.Tests;

public class UserPostNightShiftOverrideTests
{
    [Theory]
    // User overrides Department to True (allows evening after night even if dept forbids)
    [InlineData(true, false, true)]
    // User overrides Department to False (forbids evening after night even if dept allows)
    [InlineData(false, true, false)]
    // Null user field inherits directly from Department
    [InlineData(null, true, true)]
    [InlineData(null, false, false)]
    public void ResolveAllowEveningAfterNightShift_ResolvesWithPriorityOverDepartment(
        bool? userOverride, bool deptDefault, bool expectedResult)
    {
        var user = new UserConstraint
        {
            UserId = 1,
            AllowEveningAfterNightShift = userOverride
        };

        var resolved = user.ResolveAllowEveningAfterNightShift(deptDefault);
        Assert.Equal(expectedResult, resolved);
    }

    [Theory]
    // User overrides Department to True (allows consecutive nights even if dept forbids)
    [InlineData(true, false, true)]
    // User overrides Department to False (forbids consecutive nights even if dept allows)
    [InlineData(false, true, false)]
    // Null user field inherits directly from Department
    [InlineData(null, true, true)]
    [InlineData(null, false, false)]
    public void ResolveAllowNightShiftAfterNightShift_ResolvesWithPriorityOverDepartment(
        bool? userOverride, bool deptDefault, bool expectedResult)
    {
        var user = new UserConstraint
        {
            UserId = 1,
            AllowNightShiftAfterNightShift = userOverride
        };

        var resolved = user.ResolveAllowNightShiftAfterNightShift(deptDefault);
        Assert.Equal(expectedResult, resolved);
    }

    [Fact]
    public void AdjacentShiftRestRules_WouldConflict_RespectsUserOverrideOverDepartment()
    {
        var day1 = new DateTime(2026, 9, 1);
        var day2 = day1.AddDays(1);

        // User 1: Department forbids evening after night, but User 1 explicitly ALLOWS it (true)
        var user1 = new UserConstraint
        {
            UserId = 1,
            AllowEveningAfterNightShift = true
        };

        // User 2: Department allows evening after night, but User 2 explicitly FORBIDS it (false)
        var user2 = new UserConstraint
        {
            UserId = 2,
            AllowEveningAfterNightShift = false
        };

        var constraints = new ShiftConstraints
        {
            UserConstraints = new List<UserConstraint> { user1, user2 }
        };
        // Department default forbids evening after night
        constraints.HardRules.AllowEveningAfterNightShift = false;

        // User 1 existing assignment: Night on day 1
        var user1Assignments = new List<SaShiftAssignment>
        {
            new SaShiftAssignment { UserId = 1, Date = day1, ShiftLabel = ShiftLabel.Night, ShiftId = 10 }
        };

        // For User 1, Evening on day 2 should NOT conflict (because User 1 has AllowEveningAfterNightShift = true)
        var conflictUser1 = AdjacentShiftRestRules.WouldConflict(
            user1Assignments, day2, ShiftLabel.Evening, constraints, userId: 1);
        Assert.False(conflictUser1);

        // Now set department default to true:
        constraints.HardRules.AllowEveningAfterNightShift = true;

        // User 2 existing assignment: Night on day 1
        var user2Assignments = new List<SaShiftAssignment>
        {
            new SaShiftAssignment { UserId = 2, Date = day1, ShiftLabel = ShiftLabel.Night, ShiftId = 10 }
        };

        // For User 2, Evening on day 2 SHOULD conflict (because User 2 has AllowEveningAfterNightShift = false)
        var conflictUser2 = AdjacentShiftRestRules.WouldConflict(
            user2Assignments, day2, ShiftLabel.Evening, constraints, userId: 2);
        Assert.True(conflictUser2);
    }

    [Fact]
    public void AdjacentShiftRestRules_ConsecutiveNight_RespectsUserOverrideOverDepartment()
    {
        var day1 = new DateTime(2026, 9, 1);
        var day2 = day1.AddDays(1);

        var userAllowNight = new UserConstraint
        {
            UserId = 10,
            AllowNightShiftAfterNightShift = true
        };
        var userForbidNight = new UserConstraint
        {
            UserId = 20,
            AllowNightShiftAfterNightShift = false
        };

        var constraints = new ShiftConstraints
        {
            UserConstraints = new List<UserConstraint> { userAllowNight, userForbidNight }
        };

        // 1. Department default forbids consecutive nights
        constraints.HardRules.AllowNightShiftAfterNightShift = false;

        var user10Assignments = new List<SaShiftAssignment>
        {
            new SaShiftAssignment { UserId = 10, Date = day1, ShiftLabel = ShiftLabel.Night, ShiftId = 10 }
        };

        // User 10 allows consecutive nights -> No conflict
        Assert.False(AdjacentShiftRestRules.WouldConflict(
            user10Assignments, day2, ShiftLabel.Night, constraints, userId: 10));

        // 2. Department default allows consecutive nights
        constraints.HardRules.AllowNightShiftAfterNightShift = true;

        var user20Assignments = new List<SaShiftAssignment>
        {
            new SaShiftAssignment { UserId = 20, Date = day1, ShiftLabel = ShiftLabel.Night, ShiftId = 10 }
        };

        // User 20 forbids consecutive nights -> Should conflict
        Assert.True(AdjacentShiftRestRules.WouldConflict(
            user20Assignments, day2, ShiftLabel.Night, constraints, userId: 20));
    }

    [Fact]
    public void AdjacentShiftRestGuard_StripForbiddenAdjacencies_RespectsUserOverride()
    {
        var day1 = new DateTime(2026, 9, 1);
        var day2 = day1.AddDays(1);

        // User with user-level override allowing evening after night (true)
        var user = new UserConstraint
        {
            UserId = 1,
            UserName = "Ali",
            AllowEveningAfterNightShift = true
        };

        var constraints = new ShiftConstraints
        {
            UserConstraints = new List<UserConstraint> { user }
        };
        // Department forbids evening after night
        constraints.HardRules.AllowEveningAfterNightShift = false;

        var solution = new ShiftSolution();
        solution.AddAssignment(1, 10, day1, ShiftLabel.Night, isOnCall: false);
        solution.AddAssignment(1, 20, day2, ShiftLabel.Evening, isOnCall: false);

        // StripForbiddenAdjacencies should NOT remove the evening assignment because user allows it!
        AdjacentShiftRestGuard.StripForbiddenAdjacencies(solution, constraints);

        Assert.True(solution.HasAssignment(1, 10, day1));
        Assert.True(solution.HasAssignment(1, 20, day2));

        // Violations check should be empty for this user
        var violations = AdjacentShiftRestGuard.GetViolations(solution, constraints);
        Assert.Empty(violations);
    }
}
