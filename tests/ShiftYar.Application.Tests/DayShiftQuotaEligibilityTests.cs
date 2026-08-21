using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;
using Xunit;

namespace ShiftYar.Application.Tests;

public class DayShiftQuotaEligibilityTests
{
    [Fact]
    public void CanAssignInCoverageFill_AllowsNullQuotaAndNullFallback()
    {
        var solution = new ShiftSolution();
        var constraints = new ShiftConstraints();
        var user = new UserConstraint
        {
            UserId = 1,
            MorningFallbackParticipation = null,
            AllowedShiftPermissions = UserShiftPermission.Morning,
            AllowedShiftLabels = new List<ShiftLabel> { ShiftLabel.Morning }
        };

        Assert.True(DayShiftQuotaEligibility.CanAssignInCoverageFill(
            solution, constraints, user, ShiftLabel.Morning, new DateTime(2026, 8, 10)));
    }

    [Fact]
    public void CanAssignInCoverageFill_BlocksExplicitFalseFallbackWithoutQuota()
    {
        var solution = new ShiftSolution();
        var constraints = new ShiftConstraints();
        var user = new UserConstraint
        {
            UserId = 1,
            MorningFallbackParticipation = false,
            AllowedShiftPermissions = UserShiftPermission.Morning,
            AllowedShiftLabels = new List<ShiftLabel> { ShiftLabel.Morning }
        };

        Assert.False(DayShiftQuotaEligibility.CanAssignInCoverageFill(
            solution, constraints, user, ShiftLabel.Morning, new DateTime(2026, 8, 10)));
    }

    [Fact]
    public void CanAssignInCoverageFill_AllowsExactQuotaWithNullFallbackForSurplus()
    {
        var solution = new ShiftSolution();
        var constraints = new ShiftConstraints();
        var user = new UserConstraint
        {
            UserId = 1,
            ExactMorningShiftCount = 2,
            MorningFallbackParticipation = null,
            AllowedShiftPermissions = UserShiftPermission.Morning,
            AllowedShiftLabels = new List<ShiftLabel> { ShiftLabel.Morning }
        };

        solution.AddAssignment(1, 1, new DateTime(2026, 8, 1), ShiftLabel.Morning, isOnCall: false);
        solution.AddAssignment(1, 1, new DateTime(2026, 8, 2), ShiftLabel.Morning, isOnCall: false);

        Assert.True(DayShiftQuotaEligibility.CanAssignInCoverageFill(
            solution, constraints, user, ShiftLabel.Morning, new DateTime(2026, 8, 10)));
    }

    [Fact]
    public void GetMaxAllowedTotal_ReturnsExactOnlyWhenFallbackExplicitlyFalse()
    {
        var capped = new UserConstraint
        {
            ExactMorningShiftCount = 5,
            MorningFallbackParticipation = false
        };
        var open = new UserConstraint
        {
            ExactMorningShiftCount = 5,
            MorningFallbackParticipation = null
        };

        Assert.Equal(5, DayShiftQuotaEligibility.GetMaxAllowedTotal(capped, ShiftLabel.Morning));
        Assert.Equal(int.MaxValue, DayShiftQuotaEligibility.GetMaxAllowedTotal(open, ShiftLabel.Morning));
    }
}
