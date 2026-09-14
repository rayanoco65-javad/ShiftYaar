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
    public void CalculateEffectiveWorkedHours_AppliesNightMultiplierForPlanIncludedStaff()
    {
        var hours = ProductivityWorkedHoursCalculator.CalculateEffectiveWorkedHours(
            new[]
            {
                new SaShiftAssignment { UserId = 1, ShiftId = 2, Date = new DateTime(2026, 1, 5), ShiftLabel = ShiftLabel.Night }
            },
            ShiftInfo,
            isHoliday: _ => false,
            isIncludedInProductivityPlan: _ => true);

        Assert.Equal(12, hours, precision: 2);
    }

    [Fact]
    public void CalculateEffectiveWorkedHours_ReturnsOneToOneHoursForNonIncludedStaff()
    {
        var hours = ProductivityWorkedHoursCalculator.CalculateEffectiveWorkedHours(
            new[]
            {
                new SaShiftAssignment { UserId = 1, ShiftId = 2, Date = new DateTime(2026, 1, 5), ShiftLabel = ShiftLabel.Night }
            },
            ShiftInfo,
            isHoliday: _ => true,
            isIncludedInProductivityPlan: _ => false);

        Assert.Equal(8, hours, precision: 2);
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
    public void ResolveCreditedHours_UsesSupervisorConfiguredValues()
    {
        var night = new ProductivityWorkedHoursCalculator.ShiftWorkInfo(
            2,
            ShiftLabel.Night,
            12,
            TimeSpan.FromHours(19),
            TimeSpan.FromHours(7),
            WeekdayNonProductivityHours: 12,
            HolidayNonProductivityHours: 18,
            WeekdayProductivityPlanHours: 19,
            HolidayProductivityPlanHours: 19);

        Assert.Equal(12, ProductivityWorkedHoursCalculator.ResolveCreditedHours(night, isHoliday: false, includedInProductivityPlan: false));
        Assert.Equal(19, ProductivityWorkedHoursCalculator.ResolveCreditedHours(night, isHoliday: false, includedInProductivityPlan: true));
        Assert.Equal(18, ProductivityWorkedHoursCalculator.ResolveCreditedHours(night, isHoliday: true, includedInProductivityPlan: false));
        Assert.Equal(19, ProductivityWorkedHoursCalculator.ResolveCreditedHours(night, isHoliday: true, includedInProductivityPlan: true));
    }

    [Fact]
    public void CalculateEffectiveWorkedHours_UsesPlanFlagAndConfiguredHours()
    {
        var lookup = new Dictionary<int, ProductivityWorkedHoursCalculator.ShiftWorkInfo>
        {
            [10] = new(
                10,
                ShiftLabel.Morning,
                7,
                TimeSpan.FromHours(7),
                TimeSpan.FromHours(14),
                WeekdayNonProductivityHours: 7,
                HolidayNonProductivityHours: 10,
                WeekdayProductivityPlanHours: 7,
                HolidayProductivityPlanHours: 10)
        };

        var holidayMorning = new[]
        {
            new SaShiftAssignment { UserId = 5, ShiftId = 10, Date = new DateTime(2026, 1, 5), ShiftLabel = ShiftLabel.Morning }
        };

        var nonPlan = ProductivityWorkedHoursCalculator.CalculateEffectiveWorkedHours(
            holidayMorning, lookup, _ => true, _ => false);
        var inPlan = ProductivityWorkedHoursCalculator.CalculateEffectiveWorkedHours(
            holidayMorning, lookup, _ => true, _ => true);

        Assert.Equal(10, nonPlan, precision: 2);
        Assert.Equal(10, inPlan, precision: 2);
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
