using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using Xunit;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.Tests;

public class ShiftEligibilityResolverTests
{
    [Theory]
    [InlineData(ShiftTypes.FixedShift, ShiftSubTypes.FixedMorning, null, new[] { ShiftLabel.Morning })]
    [InlineData(ShiftTypes.FixedShift, ShiftSubTypes.FixedEvening, null, new[] { ShiftLabel.Evening })]
    [InlineData(ShiftTypes.RotatingShift, ShiftSubTypes.ThreeShifts, null,
        new[] { ShiftLabel.Morning, ShiftLabel.Evening, ShiftLabel.Night })]
    [InlineData(ShiftTypes.RotatingShift, ShiftSubTypes.TwoShifts, TwoShiftRotationPattern.MorningEvening,
        new[] { ShiftLabel.Morning, ShiftLabel.Evening })]
    [InlineData(ShiftTypes.RotatingShift, ShiftSubTypes.TwoShifts, TwoShiftRotationPattern.MorningNight,
        new[] { ShiftLabel.Morning, ShiftLabel.Night })]
    [InlineData(ShiftTypes.RotatingShift, ShiftSubTypes.TwoShifts, TwoShiftRotationPattern.EveningNight,
        new[] { ShiftLabel.Evening, ShiftLabel.Night })]
    public void GetAllowedLabels_MatchesShiftProfile(
        ShiftTypes type,
        ShiftSubTypes subType,
        TwoShiftRotationPattern? pattern,
        ShiftLabel[] expected)
    {
        var allowed = ShiftEligibilityResolver.GetAllowedLabels(type, subType, pattern);
        Assert.Equal(expected, allowed);
    }

    [Fact]
    public void IsLabelAllowed_EmptyList_AllowsAll()
    {
        Assert.True(ShiftEligibilityResolver.IsLabelAllowed(Array.Empty<ShiftLabel>(), ShiftLabel.Night));
    }

    [Fact]
    public void MayTakeLabelOnDate_ApprovedRequestOnlyBypassesPermissionsOnRequestedDate()
    {
        var user = new UserConstraint
        {
            UserId = 13,
            ShiftType = ShiftTypes.RotatingShift,
            ShiftSubType = ShiftSubTypes.TwoShifts,
            TwoShiftRotationPattern = TwoShiftRotationPattern.EveningNight
        };
        ShiftEligibilityResolver.ApplyPermissionsToUserConstraint(user, UserShiftPermission.Evening | UserShiftPermission.Night);

        var approvedDate = new DateTime(2026, 8, 29);
        var unrequestedDate = new DateTime(2026, 9, 2);

        user.RequiredShiftSlots.Add(new ShiftSlotConstraint
        {
            Date = approvedDate,
            ShiftLabel = ShiftLabel.Morning
        });

        // Approved date allows Morning
        Assert.True(ShiftEligibilityResolver.MayTakeLabelOnDate(user, ShiftLabel.Morning, approvedDate));

        // Unrequested date DOES NOT allow Morning
        Assert.False(ShiftEligibilityResolver.MayTakeLabelOnDate(user, ShiftLabel.Morning, unrequestedDate));

        // Evening and Night allowed inherently on all dates
        Assert.True(ShiftEligibilityResolver.MayTakeLabelOnDate(user, ShiftLabel.Evening, unrequestedDate));
        Assert.True(ShiftEligibilityResolver.MayTakeLabelOnDate(user, ShiftLabel.Night, unrequestedDate));
    }

    [Fact]
    public void IsAssignmentAllowed_WithDate_RejectsUnrequestedShiftOutsidePermissions()
    {
        var user = new UserConstraint
        {
            UserId = 13,
            ShiftType = ShiftTypes.RotatingShift,
            ShiftSubType = ShiftSubTypes.TwoShifts,
            TwoShiftRotationPattern = TwoShiftRotationPattern.EveningNight
        };
        ShiftEligibilityResolver.ApplyPermissionsToUserConstraint(user, UserShiftPermission.Evening);

        var approvedDate = new DateTime(2026, 8, 29);
        var shahrivar6 = new DateTime(2026, 8, 28); // 6 Shahrivar

        user.RequiredShiftSlots.Add(new ShiftSlotConstraint
        {
            Date = approvedDate,
            ShiftLabel = ShiftLabel.Morning
        });

        user.UnavailableShiftSlots.Add(new ShiftSlotConstraint
        {
            Date = shahrivar6,
            ShiftLabel = ShiftLabel.Evening
        });

        // Morning allowed on requested date (2026-08-29)
        Assert.True(ShiftEligibilityResolver.IsAssignmentAllowed(user, Array.Empty<ShiftLabel>(), ShiftLabel.Morning, 1, true, approvedDate));

        // Morning NOT allowed on 6 Shahrivar (2026-08-28)
        Assert.False(ShiftEligibilityResolver.IsAssignmentAllowed(user, Array.Empty<ShiftLabel>(), ShiftLabel.Morning, 1, true, shahrivar6));

        // DayShiftQuotaEligibility also rejects Morning on 6 Shahrivar
        var constraints = new ShiftConstraints();
        var solution = new ShiftSolution();
        Assert.False(DayShiftQuotaEligibility.CanAssignInCoverageFill(solution, constraints, user, ShiftLabel.Morning, shahrivar6));
    }
}
