using ShiftYar.Application.DTOs.ProductivityModel;
using ShiftYar.Application.Features.ProductivityModel.Services;
using ShiftYar.Domain.Entities.ProductivityModel;
using Xunit;

namespace ShiftYar.Application.Tests;

public class WorkingHoursCalculatorTests
{
    private readonly WorkingHoursCalculator _calculator = new();

    [Theory]
    [InlineData(0, false, false, 0.0, 1.0, 0.0, 1.0, 43.0)]  // 0 yrs, General (1.0), Fixed (0) -> 1.0 deduction
    [InlineData(5, false, false, 0.5, 1.0, 0.0, 1.5, 42.5)]  // 5 yrs (0.5), General (1.0), Fixed (0) -> 1.5 deduction
    [InlineData(5, true, false, 0.5, 2.0, 0.0, 2.5, 41.5)]   // 5 yrs (0.5), ICU/Special (2.0), Fixed (0) -> 2.5 deduction
    [InlineData(5, true, true, 0.5, 2.0, 1.0, 3.5, 40.5)]    // 5 yrs (0.5), Special (2.0), ThreeShiftRotating (1.0) -> 3.5 deduction
    [InlineData(25, true, true, 2.0, 2.0, 1.0, 5.0, 39.0)]   // 25 yrs (2.0), Special (2.0), ThreeShiftRotating (1.0) -> 5.0 deduction
    public void CalculateMonthlyHours_Group1_AppliesWeeklyReductionBands(
        int yearsOfService,
        bool isSpecialSection,
        bool rotating,
        decimal expectedSeniority,
        decimal expectedHardship,
        decimal expectedRotating,
        decimal expectedTotalWeeklyReduction,
        decimal expectedWeeklyRequired)
    {
        var result = _calculator.CalculateMonthlyHours(new WorkingHoursCalculationRequestDto
        {
            Staff = new StaffEmploymentInfoDto
            {
                StaffId = 1,
                ClinicalExperienceYears = yearsOfService,
                IsSpecialSection = isSpecialSection,
                HasUncommonRotatingShifts = rotating,
                IsIncludedInProductivityPlan = true
            },
            TotalDays = 30,
            WorkingDays = 22
        });

        Assert.Equal(expectedSeniority, result.Breakdown.SeniorityReductionPerWeek);
        Assert.Equal(expectedHardship, result.Breakdown.HardshipReductionPerWeek);
        Assert.Equal(expectedRotating, result.Breakdown.RotatingShiftReductionPerWeek);
        Assert.Equal(expectedTotalWeeklyReduction, result.Breakdown.TotalWeeklyReduction);
        Assert.Equal(expectedWeeklyRequired, result.Breakdown.WeeklyRequiredHours);
    }

    [Fact]
    public void CalculateMonthlyHours_Group1_CalculatesExactBaseAndMonthlyDeductions()
    {
        // 22 working days in a 30-day month for a nurse with 10 yrs experience in ICU with 3-shift rotating
        // BaseHours = 22 * (22 / 3) = 161.33333...
        // Seniority = 1.0, Hardship = 2.0, Rotating = 1.0 => WeeklyDeduction = 4.0
        // MonthlyDeduction = (30 / 7) * 4.0 = 17.142857...
        // FinalRequired = 161.33333... - 17.142857... = 144.19
        var result = _calculator.CalculateMonthlyHours(new WorkingHoursCalculationRequestDto
        {
            Staff = new StaffEmploymentInfoDto
            {
                StaffId = 10,
                ClinicalExperienceYears = 10,
                IsSpecialSection = true,
                HasUncommonRotatingShifts = true,
                IsIncludedInProductivityPlan = true
            },
            TotalDays = 30,
            WorkingDays = 22
        });

        Assert.True(result.Breakdown.IsIncludedInProductivityPlan);
        Assert.Equal(161.33m, result.BaseMonthlyHours);
        Assert.Equal(144.19m, result.FinalMonthlyRequiredHours);
    }

    [Fact]
    public void CalculateMonthlyHours_Group2_OrdinaryStaff_NoDeduction()
    {
        // For non-included staff (ordinary staff / civil service law):
        // BaseHours = WorkingDays * 7.3333333 = 22 * (22 / 3) = 161.33
        // Deductions = 0
        var result = _calculator.CalculateMonthlyHours(new WorkingHoursCalculationRequestDto
        {
            Staff = new StaffEmploymentInfoDto
            {
                StaffId = 20,
                ClinicalExperienceYears = 15,
                IsSpecialSection = true,
                HasUncommonRotatingShifts = true,
                IsIncludedInProductivityPlan = false
            },
            TotalDays = 30,
            WorkingDays = 22
        });

        Assert.False(result.Breakdown.IsIncludedInProductivityPlan);
        Assert.Equal(161.33m, result.BaseMonthlyHours);
        Assert.Equal(0m, result.TotalDeductions);
        Assert.Equal(161.33m, result.FinalMonthlyRequiredHours);
    }

    [Fact]
    public void CalculateMonthlyHours_GuardClauses_ThrowsOnInvalidInputs()
    {
        Assert.Throws<ArgumentNullException>(() => _calculator.CalculateMonthlyHours(null!));

        Assert.Throws<ArgumentException>(() => _calculator.CalculateMonthlyHours(new WorkingHoursCalculationRequestDto
        {
            Staff = null!
        }));

        Assert.Throws<ArgumentOutOfRangeException>(() => _calculator.CalculateMonthlyHours(new WorkingHoursCalculationRequestDto
        {
            Staff = new StaffEmploymentInfoDto(),
            TotalDays = -1
        }));

        Assert.Throws<ArgumentOutOfRangeException>(() => _calculator.CalculateMonthlyHours(new WorkingHoursCalculationRequestDto
        {
            Staff = new StaffEmploymentInfoDto(),
            WorkingDays = -5
        }));

        Assert.Throws<ArgumentException>(() => _calculator.CalculateMonthlyHours(new WorkingHoursCalculationRequestDto
        {
            Staff = new StaffEmploymentInfoDto(),
            TotalDays = 28,
            WorkingDays = 30
        }));
    }

    [Fact]
    public void CalculateMonthlyHours_AppliesNightHolidayCredit()
    {
        var result = _calculator.CalculateMonthlyHours(new WorkingHoursCalculationRequestDto
        {
            Staff = new StaffEmploymentInfoDto { StaffId = 1, IsIncludedInProductivityPlan = true },
            TotalDays = 28,
            WorkingDays = 24,
            NightHolidayHours = 16
        });

        Assert.Equal(8m, result.Breakdown.NightHolidayCreditHours);
    }

    [Theory]
    [InlineData(ShiftPatternType.FixedDay, 0.0)]
    [InlineData(ShiftPatternType.TwoShiftRotating, 0.5)]
    [InlineData(ShiftPatternType.ThreeShiftRotating, 1.0)]
    [InlineData(ShiftPatternType.FixedNight, 1.0)]
    public void CalculateMonthlyHours_Group1_ShiftPatternType_AppliesCorrectReductions(
        ShiftPatternType pattern,
        decimal expectedPatternReduction)
    {
        var result = _calculator.CalculateMonthlyHours(new WorkingHoursCalculationRequestDto
        {
            Staff = new StaffEmploymentInfoDto
            {
                StaffId = 1,
                ClinicalExperienceYears = 0,
                IsSpecialSection = false,
                ShiftPattern = pattern,
                IsIncludedInProductivityPlan = true
            },
            TotalDays = 28,
            WorkingDays = 24
        });

        // 0 yrs seniority (0.0) + General section hardship (1.0) + pattern reduction
        Assert.Equal(pattern, result.Breakdown.ShiftPattern);
        Assert.Equal(expectedPatternReduction, result.Breakdown.ShiftPatternReductionPerWeek);
        Assert.Equal(1.0m + expectedPatternReduction, result.Breakdown.TotalWeeklyReduction);
    }

    [Fact]
    public void CalculateMonthlyHours_Group1_ShiftPatternType_SupportsOverrides()
    {
        var result = _calculator.CalculateMonthlyHours(new WorkingHoursCalculationRequestDto
        {
            Staff = new StaffEmploymentInfoDto
            {
                StaffId = 1,
                ClinicalExperienceYears = 5, // 0.5
                IsSpecialSection = true,     // 2.0
                ShiftPattern = ShiftPatternType.TwoShiftRotating,
                IsIncludedInProductivityPlan = true
            },
            RuleOverrides = new ProductivityRuleOverrideDto
            {
                TwoShiftRotatingReductionHours = 0.75m
            },
            TotalDays = 28,
            WorkingDays = 24
        });

        // Seniority (0.5) + Special Hardship (2.0) + Override TwoShiftRotating (0.75) = 3.25
        Assert.Equal(ShiftPatternType.TwoShiftRotating, result.Breakdown.ShiftPattern);
        Assert.Equal(0.75m, result.Breakdown.ShiftPatternReductionPerWeek);
        Assert.Equal(3.25m, result.Breakdown.TotalWeeklyReduction);
    }

    [Theory]
    [InlineData(0, 0.0)]
    [InlineData(3, 0.0)]
    [InlineData(4, 0.5)]
    [InlineData(7, 0.5)]
    [InlineData(8, 1.0)]
    [InlineData(11, 1.0)]
    [InlineData(12, 1.5)]
    [InlineData(15, 1.5)]
    [InlineData(16, 2.0)]
    [InlineData(22, 2.0)]
    public void CalculateMonthlyHours_HospitalStandardSeniorityBands_AppliesCorrectReductions(
        int yearsOfService,
        decimal expectedSeniorityReduction)
    {
        var result = _calculator.CalculateMonthlyHours(new WorkingHoursCalculationRequestDto
        {
            Staff = new StaffEmploymentInfoDto
            {
                StaffId = 1,
                ClinicalExperienceYears = yearsOfService,
                IsSpecialSection = false,
                ShiftPattern = ShiftPatternType.FixedDay,
                IsIncludedInProductivityPlan = true
            },
            TotalDays = 30,
            WorkingDays = 24
        });

        Assert.Equal(expectedSeniorityReduction, result.Breakdown.SeniorityReductionPerWeek);
    }

    [Fact]
    public void CalculateMonthlyHours_StandardHospitalCalendar_CalculatesExactNetHours()
    {
        // ماه ۳۰ روزه فرضی با ۴ جمعه و ۲ تعطیل رسمی:
        // WorkingDays = 30 - (4 + 2) = 24 روز
        // BaseHours = 24 * (22 / 3) = 176.00
        // پرستار با ۱۰ سال سابقه در بخش جنرال با شیفت گردشی:
        // Seniority = 1.0, Hardship = 1.0, ShiftPattern = 1.0 => WeeklyReduction = 3.0
        // MonthlyReduction = (30 / 7) * 3.0 = 12.8571 -> 12.86
        // NetRequiredHours = 176.00 - 12.86 = 163.14
        var result = _calculator.CalculateMonthlyHours(new WorkingHoursCalculationRequestDto
        {
            Staff = new StaffEmploymentInfoDto
            {
                StaffId = 50,
                ClinicalExperienceYears = 10,
                IsSpecialSection = false,
                ShiftPattern = ShiftPatternType.ThreeShiftRotating,
                IsIncludedInProductivityPlan = true
            },
            TotalDays = 30,
            FridaysCount = 4,
            OfficialHolidaysCount = 2
        });

        Assert.Equal(30, result.Breakdown.TotalDays);
        Assert.Equal(4, result.Breakdown.FridaysCount);
        Assert.Equal(2, result.Breakdown.OfficialHolidaysCount);
        Assert.Equal(24, result.Breakdown.WorkingDays);
        Assert.Equal(176.00m, result.BaseMonthlyHours);
        Assert.Equal(12.86m, result.TotalDeductions);
        Assert.Equal(163.14m, result.FinalMonthlyRequiredHours);
        Assert.Equal(163.14m, result.NetRequiredHours);
        Assert.Equal(163.14m, result.Breakdown.NetRequiredHours);
        Assert.NotEmpty(result.Breakdown.Notes);
    }

    [Theory]
    [InlineData(4, 158.29, 158)] // بهار بهاری: تخفیف ۴ ساعت -> ۱۵۸.۲۹ (تقریب ۱۵۸)
    [InlineData(3, 162.71, 163)] // فرشته ساکی، درخشانی، حاتمی: تخفیف ۳ ساعت -> ۱۶۲.۷۱ (تقریب ۱۶۳)
    [InlineData(2, 167.14, 167)] // فاطمه رضایی، متقی، رحیمی: تخفیف ۲ ساعت -> ۱۶۷.۱۴ (تقریب ۱۶۷)
    [InlineData(1, 171.57, 172)] // فاطمه مدهنی: تخفیف ۱ ساعت -> ۱۷۱.۵۷ (رند به ۱۷۲ یا قطع اعشار به ۱۷۱)
    [InlineData(0, 176.00, 176)] // مریم کرمی، مریم امیدی: تخفیف ۰ ساعت (پایه) -> ۱۷۶.۰۰ (۱۷۶)
    public void CalculateMonthlyHours_Shahrivar1405_HospitalStaffExactMatches(
        decimal weeklyReduction,
        decimal expectedDecimal,
        int expectedRounded)
    {
        // در شهریور ۱۴۰۵ (۳۱ روزه، ۴ جمعه و ۱ تعطیل رسمی = ۲۶ روز تقویمی):
        // سیستم روزهای کاری را به سقف استاندارد ۲۴ روز و ساعت پایه را به ۱۷۶ ساعت محدود می‌کند
        // تخفیف ماهانه: (31 / 7) * weeklyReduction
        var result = _calculator.CalculateMonthlyHours(new WorkingHoursCalculationRequestDto
        {
            Staff = new StaffEmploymentInfoDto
            {
                StaffId = 100,
                IsIncludedInProductivityPlan = true,
                ClinicalExperienceYears = 0,
                IsSpecialSection = false,
                ShiftPattern = ShiftPatternType.FixedDay
            },
            RuleOverrides = new ProductivityRuleOverrideDto
            {
                HardshipReductionPerWeek = weeklyReduction // اعمال مستقیم تخفیف هفتگی موردنظر
            },
            CapBaseHoursToStandardMonth = true,
            TotalDays = 31,
            FridaysCount = 4,
            OfficialHolidaysCount = 1
        });

        Assert.True(result.Breakdown.IsCappedToStandardMonth);
        Assert.Equal(24, result.Breakdown.WorkingDays);
        Assert.Equal(176.00m, result.BaseMonthlyHours);
        Assert.Equal(expectedDecimal, result.FinalMonthlyRequiredHours);
        Assert.Equal(expectedRounded, result.FinalMonthlyRequiredHoursRounded);
        Assert.Equal(expectedRounded, result.Breakdown.NetRequiredHoursRounded);
    }

    [Theory]
    [InlineData(0, 183.33)] // سناریو ب: ۳۰ روزه با ۲۵ روز کاری: پایه = 25 * (22/3) = 183.33
    [InlineData(1, 179.05)] // تخفیف ۱ ساعت: 183.33 - (30/7 * 1) = 183.33 - 4.2857 = 179.05
    [InlineData(2, 174.76)] // تخفیف ۲ ساعت: 183.33 - (30/7 * 2) = 183.33 - 8.5714 = 174.76
    [InlineData(3, 170.48)] // تخفیف ۳ ساعت: 183.33 - (30/7 * 3) = 183.33 - 12.8571 = 170.48
    public void CalculateMonthlyHours_ScenarioB_30Days25WorkingDays(decimal weeklyReduction, decimal expectedFinal)
    {
        // سناریو ب: ماه ۳۰ روزه با ۴ جمعه و ۱ تعطیل رسمی وسط هفته (۲۵ روز کاری بدون سقف دستی)
        var result = _calculator.CalculateMonthlyHours(new WorkingHoursCalculationRequestDto
        {
            Staff = new StaffEmploymentInfoDto
            {
                StaffId = 101,
                IsIncludedInProductivityPlan = true,
                ClinicalExperienceYears = 0,
                IsSpecialSection = false,
                ShiftPattern = ShiftPatternType.FixedDay
            },
            RuleOverrides = new ProductivityRuleOverrideDto
            {
                HardshipReductionPerWeek = weeklyReduction
            },
            TotalDays = 30,
            FridaysCount = 4,
            OfficialHolidaysCount = 1
        });

        Assert.Equal(25, result.Breakdown.WorkingDays);
        Assert.Equal(183.33m, result.BaseMonthlyHours);
        Assert.Equal(expectedFinal, result.FinalMonthlyRequiredHours);
    }

    [Fact]
    public void CalculateMonthlyHours_ScenarioC_29DaysEsfand_CalculatesCalendarDeduction()
    {
        // سناریو ج: ماه ۲۹ روزه (اسفند): ضریب کسر هفتگی = 29 / 7 = 4.1429
        var result = _calculator.CalculateMonthlyHours(new WorkingHoursCalculationRequestDto
        {
            Staff = new StaffEmploymentInfoDto
            {
                StaffId = 102,
                IsIncludedInProductivityPlan = true,
                ClinicalExperienceYears = 0,
                IsSpecialSection = false,
                ShiftPattern = ShiftPatternType.FixedDay
            },
            RuleOverrides = new ProductivityRuleOverrideDto
            {
                HardshipReductionPerWeek = 1.0m
            },
            TotalDays = 29,
            WorkingDays = 24
        });

        // 24 * (22/3) = 176.00; MonthlyReduction = (29/7) * 1.0 = 4.1429
        // Final = 176.00 - 4.1429 = 171.86
        Assert.Equal(24, result.Breakdown.WorkingDays);
        Assert.Equal(176.00m, result.BaseMonthlyHours);
        Assert.Equal(4.1429m, result.Breakdown.MonthlyReductionFromWeeklyAdjustments);
        Assert.Equal(171.86m, result.FinalMonthlyRequiredHours);
    }

    [Fact]
    public void CalculateMonthlyHours_NightHolidayCredit_DoesNotDeductFromRequiredHours()
    {
        // ضریب ۱.۵ شیفت شب/تعطیل نباید از ساعت موظفی کسر شود، بلکه در ساعات کارکرد پرسنل اثر می‌گذارد
        var resultWithoutNight = _calculator.CalculateMonthlyHours(new WorkingHoursCalculationRequestDto
        {
            Staff = new StaffEmploymentInfoDto { StaffId = 103, IsIncludedInProductivityPlan = true },
            TotalDays = 30,
            WorkingDays = 24,
            NightHolidayHours = 0
        });

        var resultWithNight = _calculator.CalculateMonthlyHours(new WorkingHoursCalculationRequestDto
        {
            Staff = new StaffEmploymentInfoDto { StaffId = 103, IsIncludedInProductivityPlan = true },
            TotalDays = 30,
            WorkingDays = 24,
            NightHolidayHours = 32 // ۳۲ ساعت شب -> ۱۶ ساعت اعتبار
        });

        // ساعت موظفی نهایی نباید هیچ تغییری کند
        Assert.Equal(resultWithoutNight.FinalMonthlyRequiredHours, resultWithNight.FinalMonthlyRequiredHours);
        Assert.Equal(resultWithoutNight.TotalDeductions, resultWithNight.TotalDeductions);
        // اما در Breakdown اعتبار شب برای گزارش ثبت می‌شود
        Assert.Equal(16m, resultWithNight.Breakdown.NightHolidayCreditHours);
    }

    [Fact]
    public void CalculateMonthlyHours_Shahrivar1405_OrdinaryStaff_CappedAt176()
    {
        var result = _calculator.CalculateMonthlyHours(new WorkingHoursCalculationRequestDto
        {
            Staff = new StaffEmploymentInfoDto
            {
                StaffId = 200,
                IsIncludedInProductivityPlan = false
            },
            CapBaseHoursToStandardMonth = true,
            TotalDays = 31,
            FridaysCount = 4,
            OfficialHolidaysCount = 1
        });

        Assert.True(result.Breakdown.IsCappedToStandardMonth);
        Assert.Equal(24, result.Breakdown.WorkingDays);
        Assert.Equal(176.00m, result.BaseMonthlyHours);
        Assert.Equal(176.00m, result.FinalMonthlyRequiredHours);
        Assert.Equal(176, result.FinalMonthlyRequiredHoursRounded);
    }

    [Fact]
    public void CalculateMonthlyHours_OptOutCap_AllowsRawCalendarHours()
    {
        var result = _calculator.CalculateMonthlyHours(new WorkingHoursCalculationRequestDto
        {
            Staff = new StaffEmploymentInfoDto
            {
                StaffId = 300,
                IsIncludedInProductivityPlan = false
            },
            CapBaseHoursToStandardMonth = false,
            TotalDays = 31,
            FridaysCount = 4,
            OfficialHolidaysCount = 1
        });

        Assert.False(result.Breakdown.IsCappedToStandardMonth);
        Assert.Equal(26, result.Breakdown.WorkingDays);
        Assert.Equal(190.67m, result.BaseMonthlyHours);
        Assert.Equal(190.67m, result.FinalMonthlyRequiredHours);
    }

    [Fact]
    public void CalculateMonthlyHours_ExcludeThursdays_ReducesWorkingDays()
    {
        var result = _calculator.CalculateMonthlyHours(new WorkingHoursCalculationRequestDto
        {
            Staff = new StaffEmploymentInfoDto
            {
                StaffId = 400,
                IsIncludedInProductivityPlan = false
            },
            CapBaseHoursToStandardMonth = false,
            ExcludeThursdays = true,
            ThursdaysCount = 4,
            TotalDays = 31,
            FridaysCount = 4,
            OfficialHolidaysCount = 1
        });

        // 31 - 5 = 26 - 4 thursdays = 22 working days
        Assert.Equal(22, result.Breakdown.WorkingDays);
        Assert.Equal(161.33m, result.BaseMonthlyHours);
    }
}

