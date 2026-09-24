using System;
using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.DTOs.ProductivityModel;
using ShiftYar.Application.Features.ProductivityModel.Services;
using ShiftYar.Domain.Entities.ProductivityModel;
using Xunit;

namespace ShiftYar.Application.Tests;

public class WorkingHoursCalculatorTests
{
    private readonly WorkingHoursCalculator _calculator = new();

    #region ۱. تست مرزهای سنوات خدمت (Seniority Reduction Bands)

    [Theory]
    [InlineData(0, 1.0)]   // بدو خدمت / طرحی (۰ سال): ۱ ساعت
    [InlineData(1, 1.0)]   // ۱ سال: ۱ ساعت
    [InlineData(3, 1.0)]   // ۳ سال: ۱ ساعت
    [InlineData(4, 1.0)]   // ۴ سال تمام: ۱ ساعت
    [InlineData(5, 2.0)]   // ۵ سال (۴ سال و ۱ ماه تا ۸ سال): ۲ ساعت
    [InlineData(6, 2.0)]   // ۶ سال: ۲ ساعت
    [InlineData(8, 2.0)]   // ۸ سال تمام: ۲ ساعت
    [InlineData(9, 3.0)]   // ۹ سال (۸ سال و ۱ ماه تا ۱۲ سال): ۳ ساعت
    [InlineData(10, 3.0)]  // ۱۰ سال: ۳ ساعت
    [InlineData(12, 3.0)]  // ۱۲ سال تمام: ۳ ساعت
    [InlineData(13, 4.0)]  // ۱۳ سال (۱۲ سال و ۱ ماه تا ۱۶ سال): ۴ ساعت
    [InlineData(15, 4.0)]  // ۱۵ سال: ۴ ساعت
    [InlineData(16, 4.0)]  // ۱۶ سال تمام: ۴ ساعت
    [InlineData(17, 5.0)]  // ۱۷ سال (۱۶ سال و ۱ ماه به بالا): ۵ ساعت
    [InlineData(20, 5.0)]  // ۲۰ سال: ۵ ساعت
    [InlineData(25, 5.0)]  // ۲۵ سال: ۵ ساعت
    public void CalculateMonthlyHours_SeniorityBands_AppliesOfficialDirectiveReductions(
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
    public void CalculateMonthlyHours_SeniorityReduction_CalculatesExactMonthThresholds()
    {
        var referenceMonth = new DateTime(2026, 9, 1);

        // ۴ سال و ۱ ماه (۴۹ ماه قبل): باید وارد بازه ۲.۰ ساعت شود
        var employmentDate4y1m = referenceMonth.AddMonths(-49);
        var result4y1m = _calculator.CalculateMonthlyHours(new WorkingHoursCalculationRequestDto
        {
            Staff = new StaffEmploymentInfoDto
            {
                StaffId = 2,
                DateOfEmployment = employmentDate4y1m,
                IsIncludedInProductivityPlan = true,
                ShiftPattern = ShiftPatternType.FixedDay
            },
            TargetMonth = referenceMonth,
            TotalDays = 30,
            WorkingDays = 24
        });
        Assert.Equal(2.0m, result4y1m.Breakdown.SeniorityReductionPerWeek);

        // ۸ سال و ۱ ماه (۹۷ ماه قبل): باید وارد بازه ۳.۰ ساعت شود
        var employmentDate8y1m = referenceMonth.AddMonths(-97);
        var result8y1m = _calculator.CalculateMonthlyHours(new WorkingHoursCalculationRequestDto
        {
            Staff = new StaffEmploymentInfoDto
            {
                StaffId = 3,
                DateOfEmployment = employmentDate8y1m,
                IsIncludedInProductivityPlan = true,
                ShiftPattern = ShiftPatternType.FixedDay
            },
            TargetMonth = referenceMonth,
            TotalDays = 30,
            WorkingDays = 24
        });
        Assert.Equal(3.0m, result8y1m.Breakdown.SeniorityReductionPerWeek);

        // ۱۶ سال و ۱ ماه (۱۹۳ ماه قبل): باید وارد بازه ۵.۰ ساعت شود
        var employmentDate16y1m = referenceMonth.AddMonths(-193);
        var result16y1m = _calculator.CalculateMonthlyHours(new WorkingHoursCalculationRequestDto
        {
            Staff = new StaffEmploymentInfoDto
            {
                StaffId = 4,
                DateOfEmployment = employmentDate16y1m,
                IsIncludedInProductivityPlan = true,
                ShiftPattern = ShiftPatternType.FixedDay
            },
            TargetMonth = referenceMonth,
            TotalDays = 30,
            WorkingDays = 24
        });
        Assert.Equal(5.0m, result16y1m.Breakdown.SeniorityReductionPerWeek);
    }

    #endregion

    #region ۲. تست کاهش صعوبت کار با درصد (Hardship Percentage)

    [Theory]
    [InlineData(0, 0.0)]     // ۰ درصد -> ۰ ساعت
    [InlineData(5, 0.0)]     // زیر ۸ درصد -> ۰ ساعت
    [InlineData(7.5, 0.0)]   // زیر ۸ درصد -> ۰ ساعت
    [InlineData(8, 0.5)]     // ۸ تا ۲۵ درصد -> ۰.۵ ساعت
    [InlineData(15, 0.5)]    // ۸ تا ۲۵ درصد -> ۰.۵ ساعت
    [InlineData(25, 0.5)]    // ۸ تا ۲۵ درصد -> ۰.۵ ساعت
    [InlineData(26, 1.0)]    // ۲۶ تا ۵۰ درصد -> ۱.۰ ساعت
    [InlineData(40, 1.0)]    // ۲۶ تا ۵۰ درصد -> ۱.۰ ساعت
    [InlineData(50, 1.0)]    // ۲۶ تا ۵۰ درصد -> ۱.۰ ساعت
    [InlineData(51, 1.5)]    // ۵۱ تا ۷۵ درصد -> ۱.۵ ساعت
    [InlineData(60, 1.5)]    // ۵۱ تا ۷۵ درصد -> ۱.۵ ساعت
    [InlineData(75, 1.5)]    // ۵۱ تا ۷۵ درصد -> ۱.۵ ساعت
    [InlineData(76, 2.0)]    // ۷۶ تا ۱۰۰ درصد -> ۲.۰ ساعت
    [InlineData(90, 2.0)]    // ۷۶ تا ۱۰۰ درصد -> ۲.۰ ساعت
    [InlineData(100, 2.0)]   // ۷۶ تا ۱۰۰ درصد -> ۲.۰ ساعت
    public void CalculateMonthlyHours_HardshipPercentage_AppliesCorrectReductions(
        decimal percentage,
        decimal expectedHardshipReduction)
    {
        var result = _calculator.CalculateMonthlyHours(new WorkingHoursCalculationRequestDto
        {
            Staff = new StaffEmploymentInfoDto
            {
                StaffId = 10,
                ClinicalExperienceYears = 0,
                HardshipPercent = percentage,
                IsIncludedInProductivityPlan = true,
                ShiftPattern = ShiftPatternType.FixedDay
            },
            TotalDays = 30,
            WorkingDays = 24
        });

        Assert.Equal(expectedHardshipReduction, result.Breakdown.HardshipReductionPerWeek);
    }

    #endregion

    #region ۳. تست کاهش صعوبت کار با امتیاز (Hardship Score / Points)

    [Theory]
    [InlineData(0, 0.5)]      // ۰ تا ۳۷۵ امتیاز -> ۰.۵ ساعت
    [InlineData(150, 0.5)]    // ۰ تا ۳۷۵ امتیاز -> ۰.۵ ساعت
    [InlineData(375, 0.5)]    // ۰ تا ۳۷۵ امتیاز -> ۰.۵ ساعت
    [InlineData(376, 1.0)]    // ۳۷۶ تا ۷۵۰ امتیاز -> ۱.۰ ساعت
    [InlineData(500, 1.0)]    // ۳۷۶ تا ۷۵۰ امتیاز -> ۱.۰ ساعت
    [InlineData(750, 1.0)]    // ۳۷۶ تا ۷۵۰ امتیاز -> ۱.۰ ساعت
    [InlineData(751, 1.5)]    // ۷۵۱ تا ۱۰۰۰ امتیاز -> ۱.۵ ساعت
    [InlineData(900, 1.5)]    // ۷۵۱ تا ۱۰۰۰ امتیاز -> ۱.۵ ساعت
    [InlineData(1000, 1.5)]   // ۷۵۱ تا ۱۰۰۰ امتیاز -> ۱.۵ ساعت
    [InlineData(1001, 2.0)]   // ۱۰۰۰ امتیاز به بالا -> ۲.۰ ساعت
    [InlineData(1200, 2.0)]   // ۱۰۰۰ امتیاز به بالا -> ۲.۰ ساعت
    public void CalculateMonthlyHours_HardshipScore_AppliesCorrectReductions(
        decimal score,
        decimal expectedHardshipReduction)
    {
        var result = _calculator.CalculateMonthlyHours(new WorkingHoursCalculationRequestDto
        {
            Staff = new StaffEmploymentInfoDto
            {
                StaffId = 20,
                ClinicalExperienceYears = 0,
                HardshipScore = score,
                IsIncludedInProductivityPlan = true,
                ShiftPattern = ShiftPatternType.FixedDay
            },
            TotalDays = 30,
            WorkingDays = 24
        });

        Assert.Equal(expectedHardshipReduction, result.Breakdown.HardshipReductionPerWeek);
        Assert.Equal(score, result.Breakdown.HardshipScore);
    }

    [Fact]
    public void CalculateMonthlyHours_HardshipScore_NullScore_GivesZeroHardshipReduction()
    {
        var result = _calculator.CalculateMonthlyHours(new WorkingHoursCalculationRequestDto
        {
            Staff = new StaffEmploymentInfoDto
            {
                StaffId = 21,
                ClinicalExperienceYears = 0,
                HardshipScore = null,
                HardshipPercent = 0m,
                IsIncludedInProductivityPlan = true,
                ShiftPattern = ShiftPatternType.FixedDay
            },
            TotalDays = 30,
            WorkingDays = 24
        });

        Assert.Equal(0.0m, result.Breakdown.HardshipReductionPerWeek);
    }

    #endregion

    #region ۴. تست ولیدیشن انحصاری متقابل (Mutually Exclusive / XOR)

    [Fact]
    public void CalculateMonthlyHours_BothPercentAndScoreProvided_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => _calculator.CalculateMonthlyHours(new WorkingHoursCalculationRequestDto
        {
            Staff = new StaffEmploymentInfoDto
            {
                StaffId = 30,
                HardshipPercent = 50m,
                HardshipScore = 600m,
                IsIncludedInProductivityPlan = true
            },
            TotalDays = 30,
            WorkingDays = 24
        }));

        Assert.Contains(HardshipRulesValidator.MutuallyExclusiveErrorMessage, ex.Message);
    }

    [Fact]
    public void HardshipRulesValidator_Validate_CorrectlyValidatesAllScenarios()
    {
        // ورود همزمان درصد مثبت و امتیاز -> خطا
        var errorBoth = HardshipRulesValidator.Validate(50m, 500m);
        Assert.NotNull(errorBoth);
        Assert.Equal(HardshipRulesValidator.MutuallyExclusiveErrorMessage, errorBoth);

        // درصد منفی -> خطا
        var errorNegativePercent = HardshipRulesValidator.Validate(-5m, null);
        Assert.Equal(HardshipRulesValidator.NegativePercentageErrorMessage, errorNegativePercent);

        // درصد بالای ۱۰۰ -> خطا
        var errorOver100Percent = HardshipRulesValidator.Validate(105m, null);
        Assert.Equal(HardshipRulesValidator.ExceededPercentageErrorMessage, errorOver100Percent);

        // امتیاز منفی -> خطا
        var errorNegativeScore = HardshipRulesValidator.Validate(null, -10m);
        Assert.Equal(HardshipRulesValidator.NegativeScoreErrorMessage, errorNegativeScore);

        // ورود صرفاً درصد -> معتبر
        Assert.Null(HardshipRulesValidator.Validate(50m, null));

        // ورود صرفاً امتیاز -> معتبر
        Assert.Null(HardshipRulesValidator.Validate(null, 500m));

        // درصد صفر و امتیاز وارد شده -> معتبر (درصد پر نشده)
        Assert.Null(HardshipRulesValidator.Validate(0m, 500m));

        // هیچ مقداری وارد نشده -> معتبر
        Assert.Null(HardshipRulesValidator.Validate(null, null));
    }

    #endregion

    #region ۵. تست رده‌های مدیریتی بالینی (ماده ۴ دستورالعمل اجرایی: سوپروایزر، سرپرستار، مترون)

    [Theory]
    [InlineData("سوپروایزر")]
    [InlineData("سوپروایزر بالینی")]
    [InlineData("سوپروایزر آموزشی")]
    [InlineData("سرپرستار")]
    [InlineData("سرپرستار بخش ICU")]
    [InlineData("مترون")]
    [InlineData("مدیر پرستاری")]
    [InlineData("Supervisor")]
    [InlineData("Head Nurse")]
    [InlineData("HeadNurse")]
    [InlineData("Metron")]
    public void CalculateMonthlyHours_ClinicalManagementTitles_AutomaticallyReceives2HoursHardship(string title)
    {
        // با درصد ۰ و امتیاز null، باز هم رده مدیریتی باید قطعی ۲ ساعت کسر سختی کار دریافت کند
        var resultByPosition = _calculator.CalculateMonthlyHours(new WorkingHoursCalculationRequestDto
        {
            Staff = new StaffEmploymentInfoDto
            {
                StaffId = 40,
                ClinicalExperienceYears = 0,
                Position = title,
                HardshipPercent = 0m,
                HardshipScore = null,
                IsIncludedInProductivityPlan = true,
                ShiftPattern = ShiftPatternType.FixedDay
            },
            TotalDays = 30,
            WorkingDays = 24
        });

        Assert.True(resultByPosition.Breakdown.IsClinicalManager);
        Assert.Equal(2.0m, resultByPosition.Breakdown.HardshipReductionPerWeek);

        var resultByJobTitle = _calculator.CalculateMonthlyHours(new WorkingHoursCalculationRequestDto
        {
            Staff = new StaffEmploymentInfoDto
            {
                StaffId = 41,
                ClinicalExperienceYears = 0,
                JobTitle = title,
                HardshipPercent = 0m,
                HardshipScore = null,
                IsIncludedInProductivityPlan = true,
                ShiftPattern = ShiftPatternType.FixedDay
            },
            TotalDays = 30,
            WorkingDays = 24
        });

        Assert.True(resultByJobTitle.Breakdown.IsClinicalManager);
        Assert.Equal(2.0m, resultByJobTitle.Breakdown.HardshipReductionPerWeek);
    }

    [Fact]
    public void CalculateMonthlyHours_IsSupervisorFlag_AutomaticallyReceives2HoursHardship()
    {
        var result = _calculator.CalculateMonthlyHours(new WorkingHoursCalculationRequestDto
        {
            Staff = new StaffEmploymentInfoDto
            {
                StaffId = 42,
                ClinicalExperienceYears = 2,
                IsSupervisor = true,
                HardshipPercent = 0m,
                HardshipScore = null,
                IsIncludedInProductivityPlan = true,
                ShiftPattern = ShiftPatternType.FixedDay
            },
            TotalDays = 30,
            WorkingDays = 24
        });

        Assert.True(result.Breakdown.IsClinicalManager);
        Assert.Equal(2.0m, result.Breakdown.HardshipReductionPerWeek);
    }

    [Fact]
    public void CalculateMonthlyHours_IsHeadNurseFlag_AutomaticallyReceives2HoursHardship()
    {
        var result = _calculator.CalculateMonthlyHours(new WorkingHoursCalculationRequestDto
        {
            Staff = new StaffEmploymentInfoDto
            {
                StaffId = 43,
                ClinicalExperienceYears = 4,
                IsHeadNurse = true,
                HardshipPercent = 10m, // درصد پایین ثبت شده اما اولویت با تخفیف قطعی ماده ۴ است
                IsIncludedInProductivityPlan = true,
                ShiftPattern = ShiftPatternType.FixedDay
            },
            TotalDays = 30,
            WorkingDays = 24
        });

        Assert.True(result.Breakdown.IsClinicalManager);
        Assert.Equal(2.0m, result.Breakdown.HardshipReductionPerWeek);
    }

    #endregion

    #region ۶. تست کاهش نوبت‌کاری غیرمتعارف و حذف شرط سابقه ۱۰ سال

    [Fact]
    public void CalculateMonthlyHours_RotatingShift_AppliesToJuniorStaffUnder10Years()
    {
        // پرسنل با ۱ سال سابقه بالینی با شیفت گردشی ۳ نوبته:
        // سابقه (۱.۰ ساعت) + نوبت‌کاری (۱.۰ ساعت) = ۲.۰ ساعت در هفته
        var result = _calculator.CalculateMonthlyHours(new WorkingHoursCalculationRequestDto
        {
            Staff = new StaffEmploymentInfoDto
            {
                StaffId = 50,
                ClinicalExperienceYears = 1,
                HasUncommonRotatingShifts = true,
                ShiftPattern = ShiftPatternType.ThreeShiftRotating,
                IsIncludedInProductivityPlan = true
            },
            TotalDays = 30,
            WorkingDays = 24
        });

        Assert.Equal(1.0m, result.Breakdown.SeniorityReductionPerWeek);
        Assert.Equal(1.0m, result.Breakdown.RotatingShiftReductionPerWeek);
        Assert.Equal(2.0m, result.Breakdown.TotalWeeklyReduction);
    }

    [Fact]
    public void CalculateMonthlyHours_FixedDayShift_ReceivesZeroShiftReduction()
    {
        var result = _calculator.CalculateMonthlyHours(new WorkingHoursCalculationRequestDto
        {
            Staff = new StaffEmploymentInfoDto
            {
                StaffId = 51,
                ClinicalExperienceYears = 10,
                HasUncommonRotatingShifts = false,
                ShiftPattern = ShiftPatternType.FixedDay,
                IsIncludedInProductivityPlan = true
            },
            TotalDays = 30,
            WorkingDays = 24
        });

        Assert.Equal(0.0m, result.Breakdown.RotatingShiftReductionPerWeek);
    }

    #endregion

    #region ۷. تست تجمیع و سقف نهایی ۸ ساعت در هفته

    [Fact]
    public void CalculateMonthlyHours_MaxCap_CapsTotalWeeklyReductionAt8Hours()
    {
        // پرسنل با سابقه ۲۰ سال (۵.۰ ساعت) + سوپروایزر (۲.۰ ساعت) + نوبت‌کاری (۱.۰ ساعت) = ۸.۰ ساعت
        var result = _calculator.CalculateMonthlyHours(new WorkingHoursCalculationRequestDto
        {
            Staff = new StaffEmploymentInfoDto
            {
                StaffId = 60,
                ClinicalExperienceYears = 20,
                IsSupervisor = true,
                HasUncommonRotatingShifts = true,
                ShiftPattern = ShiftPatternType.ThreeShiftRotating,
                IsIncludedInProductivityPlan = true
            },
            TotalDays = 30,
            WorkingDays = 24
        });

        Assert.Equal(5.0m, result.Breakdown.SeniorityReductionPerWeek);
        Assert.Equal(2.0m, result.Breakdown.HardshipReductionPerWeek);
        Assert.Equal(1.0m, result.Breakdown.RotatingShiftReductionPerWeek);
        Assert.Equal(8.0m, result.Breakdown.TotalWeeklyReduction);
        Assert.Equal(36.0m, result.Breakdown.WeeklyRequiredHours); // 44 - 8 = 36
    }

    [Fact]
    public void CalculateMonthlyHours_SumExceeding8Hours_StrictlyCappedAt8Hours()
    {
        // فرضاً با اعمال یک آورراید تخفیف هفتگی مجموع محاسباتی به ۹ ساعت برسد
        var result = _calculator.CalculateMonthlyHours(new WorkingHoursCalculationRequestDto
        {
            Staff = new StaffEmploymentInfoDto
            {
                StaffId = 61,
                ClinicalExperienceYears = 20, // 5.0h
                HasUncommonRotatingShifts = true,
                ShiftPattern = ShiftPatternType.ThreeShiftRotating,
                IsIncludedInProductivityPlan = true
            },
            RuleOverrides = new ProductivityRuleOverrideDto
            {
                HardshipReductionPerWeek = 4.0m // 5.0 + 4.0 + 1.0 = 10.0h
            },
            TotalDays = 30,
            WorkingDays = 24
        });

        Assert.Equal(8.0m, result.Breakdown.TotalWeeklyReduction);
        Assert.Equal(36.0m, result.Breakdown.WeeklyRequiredHours);
    }

    #endregion

    #region ۸. تست محاسبات تقویمی و پرسنل غیرمشمول

    [Fact]
    public void CalculateMonthlyHours_Group2_OrdinaryStaff_NoDeduction()
    {
        // پرسنل غیرمشمول (عادی / قانون خدمات کشوری):
        // BaseHours = WorkingDays * (22 / 3) = 22 * 7.3333333 = 161.33
        // کسورات = ۰
        // ساعت موظفی نهایی = ۱۶۱ (گردشده)
        var result = _calculator.CalculateMonthlyHours(new WorkingHoursCalculationRequestDto
        {
            Staff = new StaffEmploymentInfoDto
            {
                StaffId = 70,
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
        Assert.Equal(161m, result.FinalMonthlyRequiredHours);
    }

    [Fact]
    public void CalculateMonthlyHours_GuardClauses_ThrowsOnInvalidInputs()
    {
        Assert.Throws<ArgumentNullException>(() => _calculator.CalculateMonthlyHours(null!));

        Assert.Throws<ArgumentException>(() => _calculator.CalculateMonthlyHours(new WorkingHoursCalculationRequestDto
        {
            Staff = null!,
            TotalDays = 30
        }));

        Assert.Throws<ArgumentOutOfRangeException>(() => _calculator.CalculateMonthlyHours(new WorkingHoursCalculationRequestDto
        {
            Staff = new StaffEmploymentInfoDto { StaffId = 1 },
            TotalDays = -1
        }));

        Assert.Throws<ArgumentOutOfRangeException>(() => _calculator.CalculateMonthlyHours(new WorkingHoursCalculationRequestDto
        {
            Staff = new StaffEmploymentInfoDto { StaffId = 1 },
            TotalDays = 30,
            WorkingDays = -1
        }));

        Assert.Throws<ArgumentException>(() => _calculator.CalculateMonthlyHours(new WorkingHoursCalculationRequestDto
        {
            Staff = new StaffEmploymentInfoDto { StaffId = 1 },
            TotalDays = 30,
            WorkingDays = 31
        }));
    }

    [Fact]
    public void CalculateMonthlyHours_NightHolidayCredit_DoesNotDeductFromRequiredHours()
    {
        // ضریب ۱.۵ شیفت شب/تعطیل نباید از ساعت موظفی کسر شود بلکه در ساعات کارکرد مؤثر لحاظ می‌شود
        var resultWithoutNight = _calculator.CalculateMonthlyHours(new WorkingHoursCalculationRequestDto
        {
            Staff = new StaffEmploymentInfoDto { StaffId = 80, IsIncludedInProductivityPlan = true },
            TotalDays = 30,
            WorkingDays = 24,
            NightHolidayHours = 0
        });

        var resultWithNight = _calculator.CalculateMonthlyHours(new WorkingHoursCalculationRequestDto
        {
            Staff = new StaffEmploymentInfoDto { StaffId = 80, IsIncludedInProductivityPlan = true },
            TotalDays = 30,
            WorkingDays = 24,
            NightHolidayHours = 32
        });

        Assert.Equal(resultWithoutNight.FinalMonthlyRequiredHours, resultWithNight.FinalMonthlyRequiredHours);
        Assert.Equal(resultWithoutNight.TotalDeductions, resultWithNight.TotalDeductions);
        Assert.Equal(16m, resultWithNight.Breakdown.NightHolidayCreditHours);
    }

    [Fact]
    public void CalculateMonthlyHours_OptOutCap_AllowsRawCalendarHours()
    {
        var result = _calculator.CalculateMonthlyHours(new WorkingHoursCalculationRequestDto
        {
            Staff = new StaffEmploymentInfoDto
            {
                StaffId = 90,
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
        Assert.Equal(191m, result.FinalMonthlyRequiredHours);
    }

    [Fact]
    public void CalculateMonthlyHours_ExcludeThursdays_ReducesWorkingDays()
    {
        var result = _calculator.CalculateMonthlyHours(new WorkingHoursCalculationRequestDto
        {
            Staff = new StaffEmploymentInfoDto
            {
                StaffId = 91,
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

    #endregion
}
