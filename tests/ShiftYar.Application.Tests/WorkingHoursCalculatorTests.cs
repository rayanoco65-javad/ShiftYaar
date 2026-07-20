using ShiftYar.Application.DTOs.ProductivityModel;
using ShiftYar.Application.Features.ProductivityModel.Services;
using ShiftYar.Domain.Entities.ProductivityModel;
using Xunit;

namespace ShiftYar.Application.Tests;

public class WorkingHoursCalculatorTests
{
    private readonly WorkingHoursCalculator _calculator = new();

    [Theory]
    [InlineData(0, 0, false, 44)]
    [InlineData(5, 0, false, 42)]
    [InlineData(5, 30, false, 41)]
    [InlineData(5, 30, true, 40)]
    [InlineData(20, 80, true, 36)]
    public void CalculateMonthlyHours_AppliesWeeklyReductionBands(
        int yearsOfService,
        decimal hardshipPercent,
        bool rotating,
        decimal expectedWeeklyRequired)
    {
        var result = _calculator.CalculateMonthlyHours(new WorkingHoursCalculationRequestDto
        {
            Staff = new StaffEmploymentInfoDto
            {
                StaffId = 1,
                YearsOfServiceOverride = yearsOfService,
                HardshipPercent = hardshipPercent,
                HasUncommonRotatingShifts = rotating
            },
            NumberOfWeeksInMonth = 4,
            NightHolidayHours = 0
        });

        Assert.Equal(expectedWeeklyRequired, result.Breakdown.WeeklyRequiredHours);
        Assert.Equal(expectedWeeklyRequired * 4, result.FinalMonthlyRequiredHours);
    }

    [Fact]
    public void ProductivityRuleConfig_GetHardshipReduction_UsesPercentBands()
    {
        var config = ProductivityRuleConfig.CreateDefault();

        Assert.Equal(0m, config.GetHardshipReduction(7m));
        Assert.Equal(0.5m, config.GetHardshipReduction(10m));
        Assert.Equal(1m, config.GetHardshipReduction(40m));
        Assert.Equal(1.5m, config.GetHardshipReduction(60m));
        Assert.Equal(2m, config.GetHardshipReduction(90m));
    }

    [Fact]
    public void CalculateMonthlyHours_AppliesNightHolidayCredit()
    {
        var result = _calculator.CalculateMonthlyHours(new WorkingHoursCalculationRequestDto
        {
            Staff = new StaffEmploymentInfoDto { StaffId = 1 },
            NumberOfWeeksInMonth = 4,
            NightHolidayHours = 16
        });

        Assert.Equal(8m, result.Breakdown.NightHolidayCreditHours);
        Assert.Equal(168m, result.FinalMonthlyRequiredHours);
    }
}
