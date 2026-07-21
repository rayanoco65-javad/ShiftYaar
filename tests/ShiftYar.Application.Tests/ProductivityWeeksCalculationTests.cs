using ShiftYar.Application.DTOs.ProductivityModel;
using ShiftYar.Application.Features.ProductivityModel.Services;
using Xunit;

namespace ShiftYar.Application.Tests;

/// <summary>
/// بازه ۳۱ روزه نباید ۵ هفته موظفی بسازد (۴۴×۵=۲۲۰)؛ ماه کاری ۴ هفته است.
/// </summary>
public class ProductivityWeeksCalculationTests
{
    private readonly WorkingHoursCalculator _calculator = new();

    [Theory]
    [InlineData(31, 176)] // 44 × 4
    [InlineData(30, 176)]
    [InlineData(28, 176)]
    [InlineData(14, 88)]  // 44 × 2
    public void MonthlyProductivity_UsesFourWeeksForStandardMonth(int days, decimal expectedBase)
    {
        var weeks = days >= 28 ? 4 : (int)Math.Ceiling(days / 7.0);
        var result = _calculator.CalculateMonthlyHours(new WorkingHoursCalculationRequestDto
        {
            Staff = new StaffEmploymentInfoDto { StaffId = 1 },
            NumberOfWeeksInMonth = weeks,
            NightHolidayHours = 0
        });

        Assert.Equal(expectedBase, result.FinalMonthlyRequiredHours);
    }

    [Fact]
    public void MonthlyProductivity_WithRotatingReduction_StaysBelow176()
    {
        var result = _calculator.CalculateMonthlyHours(new WorkingHoursCalculationRequestDto
        {
            Staff = new StaffEmploymentInfoDto
            {
                StaffId = 1,
                YearsOfServiceOverride = 5,
                HardshipPercent = 30,
                HasUncommonRotatingShifts = true
            },
            NumberOfWeeksInMonth = 4,
            NightHolidayHours = 0
        });

        Assert.Equal(160m, result.FinalMonthlyRequiredHours); // 40 × 4
        Assert.True(result.FinalMonthlyRequiredHours <= 176m);
    }
}
