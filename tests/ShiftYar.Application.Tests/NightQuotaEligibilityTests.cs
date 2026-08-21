using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;
using Xunit;

namespace ShiftYar.Application.Tests;

public class NightQuotaEligibilityTests
{
    [Fact]
    public void CanAssignInCoverageFill_AllowsNullQuotaAndNullFallback()
    {
        var solution = new ShiftSolution();
        var constraints = new ShiftConstraints();
        var user = new UserConstraint
        {
            UserId = 1,
            NightFallbackParticipation = null,
            AllowedShiftPermissions = UserShiftPermission.Night,
            AllowedShiftLabels = new List<ShiftLabel> { ShiftLabel.Night }
        };

        Assert.True(NightQuotaEligibility.CanAssignInCoverageFill(
            solution, constraints, user, new DateTime(2026, 8, 10)));
    }

    [Fact]
    public void CanAssignInCoverageFill_BlocksExplicitFalseFallbackWithoutQuota()
    {
        var solution = new ShiftSolution();
        var constraints = new ShiftConstraints();
        var user = new UserConstraint
        {
            UserId = 1,
            NightFallbackParticipation = false,
            AllowedShiftPermissions = UserShiftPermission.Night,
            AllowedShiftLabels = new List<ShiftLabel> { ShiftLabel.Night }
        };

        Assert.False(NightQuotaEligibility.CanAssignInCoverageFill(
            solution, constraints, user, new DateTime(2026, 8, 10)));
    }

    [Fact]
    public void CanAssignInCoverageFill_AllowsExactQuotaWithNullFallbackForSurplus()
    {
        var solution = new ShiftSolution();
        var constraints = new ShiftConstraints();
        var user = new UserConstraint
        {
            UserId = 1,
            ExactNightShiftCount = 2,
            NightFallbackParticipation = null,
            AllowedShiftPermissions = UserShiftPermission.Night,
            AllowedShiftLabels = new List<ShiftLabel> { ShiftLabel.Night }
        };

        solution.AddAssignment(1, 1, new DateTime(2026, 8, 1), ShiftLabel.Night, isOnCall: false);
        solution.AddAssignment(1, 1, new DateTime(2026, 8, 2), ShiftLabel.Night, isOnCall: false);

        Assert.True(NightQuotaEligibility.CanAssignInCoverageFill(
            solution, constraints, user, new DateTime(2026, 8, 10)));
    }

    [Fact]
    public void GetMaxAllowedTotal_ReturnsExactOnlyWhenFallbackExplicitlyFalse()
    {
        var capped = new UserConstraint
        {
            ExactNightShiftCount = 5,
            NightFallbackParticipation = false
        };
        var open = new UserConstraint
        {
            ExactNightShiftCount = 5,
            NightFallbackParticipation = null
        };

        Assert.Equal(5, NightQuotaEligibility.GetMaxAllowedTotal(capped));
        Assert.Equal(int.MaxValue, NightQuotaEligibility.GetMaxAllowedTotal(open));
    }
}
