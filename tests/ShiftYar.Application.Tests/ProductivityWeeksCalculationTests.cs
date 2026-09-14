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
    [InlineData(31, 176)] // 24 working days * (22/3) = 176 for Group 2
    [InlineData(30, 176)]
    [InlineData(28, 176)]
    [InlineData(14, 88)]  // 12 working days * (22/3) = 88 for Group 2
    public void MonthlyProductivity_UsesFourWeeksForStandardMonth(int days, decimal expectedBase)
    {
        var weeks = days >= 28 ? 4 : (int)Math.Ceiling(days / 7.0);
        var workingDays = weeks * 6; // 6 working days per week
        var result = _calculator.CalculateMonthlyHours(new WorkingHoursCalculationRequestDto
        {
            Staff = new StaffEmploymentInfoDto { StaffId = 1, IsIncludedInProductivityPlan = false },
            TotalDays = days,
            WorkingDays = workingDays,
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
                ClinicalExperienceYears = 5,
                HasUncommonRotatingShifts = true,
                IsIncludedInProductivityPlan = true
            },
            TotalDays = 28,
            WorkingDays = 24,
            NightHolidayHours = 0
        });

        Assert.True(result.FinalMonthlyRequiredHours < 176m);
    }
}
