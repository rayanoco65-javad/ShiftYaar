using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using ShiftYar.Domain.Entities.ProductivityModel;
using Xunit;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.Tests;

public class ProductivityWorkedHoursCalculatorTests
{
    private static readonly Dictionary<int, ProductivityWorkedHoursCalculator.ShiftWorkInfo> ShiftInfo =
        new()
        {
            [1] = new(1, ShiftLabel.Morning, 8, TimeSpan.FromHours(7), TimeSpan.FromHours(15)),
            [2] = new(2, ShiftLabel.Night, 8, TimeSpan.FromHours(23), TimeSpan.FromHours(7))
        };

    [Fact]
    public void CalculateEffectiveWorkedHours_AppliesNightMultiplier()
    {
        var hours = ProductivityWorkedHoursCalculator.CalculateEffectiveWorkedHours(
            new[]
            {
                new SaShiftAssignment { UserId = 1, ShiftId = 2, Date = new DateTime(2026, 1, 5), ShiftLabel = ShiftLabel.Night }
            },
            ShiftInfo,
            _ => false);

        Assert.Equal(12, hours, precision: 2);
    }

    [Fact]
    public void CalculateEffectiveWorkedHours_AddsHandoverBetweenShifts()
    {
        var hours = ProductivityWorkedHoursCalculator.CalculateEffectiveWorkedHours(
            new[]
            {
                new SaShiftAssignment { UserId = 1, ShiftId = 1, Date = new DateTime(2026, 1, 5), ShiftLabel = ShiftLabel.Morning },
                new SaShiftAssignment { UserId = 1, ShiftId = 1, Date = new DateTime(2026, 1, 7), ShiftLabel = ShiftLabel.Morning }
            },
            ShiftInfo,
            _ => false);

        Assert.Equal(17, hours, precision: 2);
    }

    [Fact]
    public void ExceedsMaxConsecutiveWorkHours_DetectsLongStretch()
    {
        var shiftInfo = new Dictionary<int, ProductivityWorkedHoursCalculator.ShiftWorkInfo>
        {
            [1] = new(1, ShiftLabel.Morning, 8, TimeSpan.FromHours(6), TimeSpan.FromHours(14)),
            [2] = new(2, ShiftLabel.Evening, 8, TimeSpan.FromHours(14), TimeSpan.FromHours(22))
        };

        var exceeds = ProductivityWorkedHoursCalculator.ExceedsMaxConsecutiveWorkHours(
            new[]
            {
                new SaShiftAssignment { UserId = 1, ShiftId = 1, Date = new DateTime(2026, 1, 5), ShiftLabel = ShiftLabel.Morning },
                new SaShiftAssignment { UserId = 1, ShiftId = 2, Date = new DateTime(2026, 1, 5), ShiftLabel = ShiftLabel.Evening }
            },
            shiftInfo,
            maxConsecutiveHours: 12);

        Assert.True(exceeds);
    }

    [Fact]
    public void GetMaxAllowedHours_RespectsOvertimeConsent()
    {
        Assert.Equal(160, ProductivityWorkedHoursCalculator.GetMaxAllowedHours(160m, overtimeConsent: false));
        Assert.Equal(240, ProductivityWorkedHoursCalculator.GetMaxAllowedHours(160m, overtimeConsent: true));
    }
}
