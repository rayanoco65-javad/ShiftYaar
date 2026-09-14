using System;
using System.Collections.Generic;
using System.Linq;
using ShiftYar.Application.DTOs.ProductivityModel;
using ShiftYar.Application.Interfaces.ProductivityModel;
using ShiftYar.Domain.Entities.ProductivityModel;

namespace ShiftYar.Application.Features.ProductivityModel.Services
{
    /// <summary>
    /// Implements the formulas mandated by the Regulation of Productivity Promotion of Clinical Employees.
    /// FinalMonthly = (BaseWeekly × Weeks) − (WeeklyReductions × Weeks) − NightHolidayCredit.
    /// NightHolidayCredit = (NightHolidayHours × 1.5) − NightHolidayHours.
    /// </summary>
    /// <summary>
    /// Implements the formulas mandated by the Regulation of Productivity Promotion of Clinical Employees in Iran (Group 1)
    /// and General/Ordinary Civil Service Labor Law (Group 2).
    /// BaseHours = WorkingDays * (22 / 3) = WorkingDays * 7.333333333333333
    /// Group 1 Deduction = (TotalDays / 7) * WeeklyDeductions
    /// Group 2 Deduction = 0
    /// </summary>
    public class WorkingHoursCalculator : IWorkingHoursCalculator
    {
        public WorkingHoursCalculationResultDto CalculateMonthlyHours(WorkingHoursCalculationRequestDto request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (request.Staff == null)
            {
                throw new ArgumentException("Staff employment info is required.", nameof(request));
            }

            if (request.TotalDays < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(request.TotalDays), "TotalDays cannot be negative.");
            }

            if (request.WorkingDays < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(request.WorkingDays), "WorkingDays cannot be negative.");
            }

            if (request.TotalDays > 0 && request.WorkingDays > request.TotalDays)
            {
                throw new ArgumentException("WorkingDays cannot exceed TotalDays.", nameof(request));
            }

            var targetMonth = request.TargetMonth == default
                ? new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1)
                : new DateTime(request.TargetMonth.Year, request.TargetMonth.Month, 1);

            var staffInfo = BuildStaffInfo(request.Staff);
            var ruleConfig = ResolveRuleConfig(request.RuleOverrides);
            var nightHolidayHours = Math.Max(0m, request.NightHolidayHours);

            // محاسبه تعداد روزهای کل و غیرتعطیل ماه
            var totalDays = request.TotalDays > 0
                ? request.TotalDays
                : (request.NumberOfWeeksInMonth > 0 ? request.NumberOfWeeksInMonth * 7 : DateTime.DaysInMonth(targetMonth.Year, targetMonth.Month));

            var workingDays = request.WorkingDays > 0
                ? request.WorkingDays
                : (request.TotalDays > 0 ? (int)Math.Round((request.TotalDays / 7.0) * 6) : (request.NumberOfWeeksInMonth > 0 ? request.NumberOfWeeksInMonth * 6 : 24));

            var weeksInMonth = request.NumberOfWeeksInMonth > 0
                ? (decimal)request.NumberOfWeeksInMonth
                : (totalDays / 7.0m);

            // گام ۱: محاسبه ساعت کار پایه ماه (۷ ساعت و ۲۰ دقیقه به ازای هر روز کاری غیرتعطیل)
            // WorkingDays * (22 / 3)
            var baseMonthlyHours = Math.Max(0m, (decimal)workingDays * ProductivityRuleConfig.BaseDailyWorkingHours);

            // بررسی شمول طرح بهره‌وری
            var isIncluded = request.Staff.IsIncludedInProductivityPlan;

            if (!isIncluded)
            {
                // گروه دوم: پرسنل غیرمشمول (عادی / قانون خدمات کشوری یا کار)
                // کسورات = ۰؛ ساعت موظفی برابر ساعت پایه روزهای کاری غیرتعطیل
                var finalRequiredForOrdinary = Math.Max(0m, Math.Round(baseMonthlyHours, 2, MidpointRounding.AwayFromZero));

                return new WorkingHoursCalculationResultDto
                {
                    BaseMonthlyHours = Math.Round(baseMonthlyHours, 2, MidpointRounding.AwayFromZero),
                    TotalDeductions = 0m,
                    FinalMonthlyRequiredHours = finalRequiredForOrdinary,
                    Breakdown = new WorkingHoursCalculationBreakdownDto
                    {
                        IsIncludedInProductivityPlan = false,
                        TotalDays = totalDays,
                        WorkingDays = workingDays,
                        BaseHoursPerDay = ProductivityRuleConfig.BaseDailyWorkingHours,
                        BaseWeeklyHours = ruleConfig.BaseWeeklyHours,
                        WeeklyRequiredHours = ruleConfig.BaseWeeklyHours,
                        SeniorityReductionPerWeek = 0m,
                        HardshipReductionPerWeek = 0m,
                        RotatingShiftReductionPerWeek = 0m,
                        TotalWeeklyReduction = 0m,
                        MonthlyReductionFromWeeklyAdjustments = 0m,
                        NightHolidayHoursReported = nightHolidayHours,
                        NightHolidayWeightedHours = nightHolidayHours,
                        NightHolidayCreditHours = 0m,
                        Notes = new List<string>
                        {
                            "پرسنل غیرمشمول قانون ارتقای بهره‌وری (پرسنل عادی / خدمات کشوری).",
                            $"ساعت موظفی بر اساس ضرب تعداد روزهای کاری غیرتعطیل ({workingDays} روز) در ۷ ساعت و ۲۰ دقیقه محاسبه شد.",
                            "کسورات ناشی از سابقه، سختی کار یا نوبت‌کاری بهره‌وری اعمال نمی‌شود."
                        }
                    }
                };
            }

            // گروه اول: پرسنل مشمول طرح بهره‌وری
            var yearsOfService = request.Staff.ClinicalExperienceYears.HasValue && request.Staff.ClinicalExperienceYears.Value >= 0
                ? request.Staff.ClinicalExperienceYears.Value
                : staffInfo.ResolveYearsOfService(targetMonth);

            if (yearsOfService < 0)
            {
                yearsOfService = 0;
            }

            // گام ۲: محاسبه کسر ساعت هفتگی قانون ارتقای بهره‌وری
            // ۱. سابقه بالینی: ۰.۵ ساعت به ازای هر ۵ سال سابقه کار بالینی (سقف ۲ ساعت)
            var seniorityReduction = ruleConfig.GetSeniorityReduction(yearsOfService);

            // ۲. سختی کار بخش: بخش‌های ویژه (۲ ساعت)، سایر بخش‌ها (۱ تا ۱.۵ ساعت)
            var hardshipReduction = request.Staff.IsSpecialSection
                ? ruleConfig.SpecialSectionHardshipReduction
                : (staffInfo.HardshipPercent > 0m
                    ? ruleConfig.GetHardshipReduction(staffInfo.HardshipPercent)
                    : ruleConfig.GetSectionHardshipReduction(isSpecialSection: false));

            // ۳. الگوی نوبت‌کاری: ثابت روزکار (۰.۰)، دو نوبته (۰.۵)، سه نوبته کامل (۱.۰)، ثابت شب (۱.۰)
            var shiftPattern = request.Staff.ShiftPattern.HasValue
                ? request.Staff.ShiftPattern.Value
                : (staffInfo.ShiftPattern != ShiftPatternType.FixedDay
                    ? staffInfo.ShiftPattern
                    : (staffInfo.HasUncommonRotatingShifts ? ShiftPatternType.ThreeShiftRotating : ShiftPatternType.FixedDay));

            var shiftPatternReduction = ruleConfig.GetShiftPatternReduction(shiftPattern);

            // سازگاری با درخواست‌های قدیمی که فقط RotatingShiftReductionPerWeek را سفارشی کرده بودند
            if (!request.Staff.ShiftPattern.HasValue && staffInfo.HasUncommonRotatingShifts && request.RuleOverrides?.RotatingShiftReductionPerWeek.HasValue == true)
            {
                shiftPatternReduction = request.RuleOverrides.RotatingShiftReductionPerWeek.Value;
            }

            // قانون گارد سقف کسر هفتگی (حداکثر ۸ ساعت)
            var totalWeeklyReduction = Math.Min(ruleConfig.MaxWeeklyReduction, seniorityReduction + hardshipReduction + shiftPatternReduction);
            var weeklyRequiredHours = Math.Max(0m, ruleConfig.BaseWeeklyHours - totalWeeklyReduction);

            // گام ۳: تبدیل تخفیف هفتگی به تخفیف ماهانه: (TotalDays / 7) * WeeklyDeduction
            var monthlyReductionFromWeekly = Math.Round((totalDays / 7.0m) * totalWeeklyReduction, 4, MidpointRounding.AwayFromZero);

            // محاسبه اعتبار شیفت شب/تعطیل (در صورت گزارش مجزا)
            var nightHolidayWeightedHours = nightHolidayHours * ruleConfig.NightHolidayMultiplier;
            var nightHolidayCredit = nightHolidayWeightedHours - nightHolidayHours;

            var totalDeductions = monthlyReductionFromWeekly + nightHolidayCredit;

            // گام ۴: محاسبه ساعت موظفی خالص ماه
            var finalMonthlyRequiredHours = Math.Max(0m, Math.Round(baseMonthlyHours - totalDeductions, 2, MidpointRounding.AwayFromZero));

            return new WorkingHoursCalculationResultDto
            {
                BaseMonthlyHours = Math.Round(baseMonthlyHours, 2, MidpointRounding.AwayFromZero),
                TotalDeductions = Math.Round(totalDeductions, 2, MidpointRounding.AwayFromZero),
                FinalMonthlyRequiredHours = finalMonthlyRequiredHours,
                Breakdown = new WorkingHoursCalculationBreakdownDto
                {
                    IsIncludedInProductivityPlan = true,
                    TotalDays = totalDays,
                    WorkingDays = workingDays,
                    BaseHoursPerDay = ProductivityRuleConfig.BaseDailyWorkingHours,
                    BaseWeeklyHours = ruleConfig.BaseWeeklyHours,
                    WeeklyRequiredHours = weeklyRequiredHours,
                    SeniorityReductionPerWeek = seniorityReduction,
                    HardshipReductionPerWeek = hardshipReduction,
                    ShiftPattern = shiftPattern,
                    ShiftPatternReductionPerWeek = shiftPatternReduction,
                    RotatingShiftReductionPerWeek = shiftPatternReduction,
                    TotalWeeklyReduction = totalWeeklyReduction,
                    MonthlyReductionFromWeeklyAdjustments = monthlyReductionFromWeekly,
                    NightHolidayHoursReported = nightHolidayHours,
                    NightHolidayWeightedHours = nightHolidayWeightedHours,
                    NightHolidayCreditHours = nightHolidayCredit,
                    Notes = new List<string>
                    {
                        "ساعت موظفی پایه بر اساس تعداد روزهای کاری غیرتعطیل ماه ضرب در ۷ ساعت و ۲۰ دقیقه محاسبه شد.",
                        "تخفیف هفتگی ناشی از سابقه، سختی بخش و الگوی نوبت‌کاری اعمال گردید (حداکثر سقف ۸ ساعت در هفته).",
                        "تخفیف ماهانه متناسب با نسبت طول ماه (تعداد روزهای ماه تقسیم بر ۷) محاسبه گردید."
                    }
                }
            };
        }

        private static StaffEmploymentInfo BuildStaffInfo(StaffEmploymentInfoDto dto)
        {
            if (dto == null)
            {
                throw new ArgumentException("Staff employment info is required.");
            }

            var pattern = dto.ShiftPattern ?? (dto.HasUncommonRotatingShifts ? ShiftPatternType.ThreeShiftRotating : ShiftPatternType.FixedDay);

            return new StaffEmploymentInfo
            {
                StaffId = dto.StaffId,
                StaffFullName = dto.StaffFullName,
                DateOfEmployment = StaffEmploymentInfo.NormalizeEmploymentDate(dto.DateOfEmployment),
                YearsOfServiceOverride = dto.ClinicalExperienceYears ?? dto.YearsOfServiceOverride,
                HardshipPercent = dto.HardshipPercent,
                HasUncommonRotatingShifts = dto.HasUncommonRotatingShifts || pattern == ShiftPatternType.ThreeShiftRotating || pattern == ShiftPatternType.TwoShiftRotating,
                ShiftPattern = pattern
            };
        }

        private static ProductivityRuleConfig ResolveRuleConfig(ProductivityRuleOverrideDto? overrides)
        {
            if (overrides == null)
            {
                return ProductivityRuleConfig.CreateDefault();
            }

            var defaultConfig = ProductivityRuleConfig.CreateDefault();

            var seniorityBands = overrides.SeniorityReductionBands != null && overrides.SeniorityReductionBands.Count > 0
                ? overrides.SeniorityReductionBands
                    .Select(band => new SeniorityReductionBand(band.MinYearsInclusive, band.MaxYearsInclusive, band.ReductionHours))
                    .ToList()
                : defaultConfig.SeniorityReductionBands;

            return new ProductivityRuleConfig
            {
                BaseWeeklyHours = overrides.BaseWeeklyHours ?? defaultConfig.BaseWeeklyHours,
                MaxWeeklyReduction = overrides.MaxWeeklyReduction ?? defaultConfig.MaxWeeklyReduction,
                RotatingShiftReductionPerWeek = overrides.RotatingShiftReductionPerWeek ?? defaultConfig.RotatingShiftReductionPerWeek,
                ThreeShiftRotatingReductionHours = overrides.ThreeShiftRotatingReductionHours ?? defaultConfig.ThreeShiftRotatingReductionHours,
                TwoShiftRotatingReductionHours = overrides.TwoShiftRotatingReductionHours ?? defaultConfig.TwoShiftRotatingReductionHours,
                FixedNightReductionHours = overrides.FixedNightReductionHours ?? defaultConfig.FixedNightReductionHours,
                FixedDayReductionHours = overrides.FixedDayReductionHours ?? defaultConfig.FixedDayReductionHours,
                NightHolidayMultiplier = overrides.NightHolidayMultiplier ?? defaultConfig.NightHolidayMultiplier,
                SeniorityReductionBands = seniorityBands
            };
        }
    }
}

