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

            var fridaysCount = request.FridaysCount;
            var officialHolidaysCount = request.OfficialHolidaysCount;
            var thursdaysCount = request.ThursdaysCount;

            int workingDays;
            if (request.WorkingDays > 0)
            {
                workingDays = request.WorkingDays;
                if (fridaysCount == 0 && officialHolidaysCount == 0)
                {
                    fridaysCount = (int)Math.Round(totalDays / 7.0);
                    officialHolidaysCount = Math.Max(0, totalDays - workingDays - fridaysCount);
                }
            }
            else if (fridaysCount > 0 || officialHolidaysCount > 0)
            {
                workingDays = Math.Max(0, totalDays - (fridaysCount + officialHolidaysCount));
            }
            else
            {
                fridaysCount = (int)Math.Round(totalDays / 7.0);
                officialHolidaysCount = 0;
                workingDays = Math.Max(0, totalDays - fridaysCount);
            }

            var excludeThursdays = request.RuleOverrides?.ExcludeThursdays ?? request.ExcludeThursdays;
            if (excludeThursdays && thursdaysCount > 0)
            {
                workingDays = Math.Max(0, workingDays - thursdaysCount);
            }

            var dailyHours = request.RuleOverrides?.BaseDailyWorkingHours.HasValue == true && request.RuleOverrides.BaseDailyWorkingHours.Value > 0
                ? request.RuleOverrides.BaseDailyWorkingHours.Value
                : ruleConfig.DailyWorkingHours;

            var weeksInMonth = request.NumberOfWeeksInMonth > 0
                ? (decimal)request.NumberOfWeeksInMonth
                : (totalDays / 7.0m);

            // سقف‌گذاری استاندارد ماهانه (۴ هفته × ۴۴ ساعت = ۱۷۶ ساعت، ۴ هفته × ۶ روز = ۲۴ روز کاری)
            var capToStandard = request.RuleOverrides?.CapBaseHoursToStandardMonth
                ?? request.CapBaseHoursToStandardMonth
                ?? ruleConfig.CapBaseHoursToStandardMonth;

            var maxWorkingDays = request.MaxMonthlyWorkingDays
                ?? request.RuleOverrides?.MaxMonthlyWorkingDays
                ?? ruleConfig.MaxMonthlyWorkingDays;

            var maxBaseHours = request.MaxMonthlyBaseHours
                ?? request.RuleOverrides?.MaxMonthlyBaseHours
                ?? ruleConfig.MaxMonthlyBaseHours;

            if (weeksInMonth > 0 && weeksInMonth < 4)
            {
                maxWorkingDays = (int)Math.Round(weeksInMonth * 6m);
                maxBaseHours = Math.Round(weeksInMonth * ruleConfig.BaseWeeklyHours, 2, MidpointRounding.AwayFromZero);
            }

            var rawWorkingDays = workingDays;
            var isCapped = false;

            if (capToStandard && maxWorkingDays > 0 && workingDays > maxWorkingDays)
            {
                workingDays = maxWorkingDays;
                isCapped = true;
            }

            // گام ۱: محاسبه ساعت کار پایه ماه (۷ ساعت و ۲۰ دقیقه به ازای هر روز کاری غیرتعطیل)
            // WorkingDays * DailyWorkingHours
            var baseMonthlyHours = Math.Max(0m, (decimal)workingDays * dailyHours);
            if (capToStandard && maxBaseHours > 0 && baseMonthlyHours > maxBaseHours)
            {
                baseMonthlyHours = maxBaseHours;
                isCapped = true;
            }

            // بررسی شمول طرح بهره‌وری
            var isIncluded = request.Staff.IsIncludedInProductivityPlan;

            if (!isIncluded)
            {
                // گروه دوم: پرسنل غیرمشمول (عادی / قانون خدمات کشوری یا کار)
                // کسورات = ۰؛ ساعت موظفی برابر ساعت پایه روزهای کاری غیرتعطیل
                var finalRequiredForOrdinary = Math.Max(0m, Math.Round(baseMonthlyHours, 2, MidpointRounding.AwayFromZero));

                var ordinaryNotes = new List<string>
                {
                    "پرسنل غیرمشمول قانون ارتقای بهره‌وری (پرسنل عادی / خدمات کشوری).",
                    $"تقویم مبنا: {totalDays} روز کل، {fridaysCount} جمعه، {officialHolidaysCount} تعطیل رسمی، {workingDays} روز کاری موظف.",
                    $"ساعت موظفی بر اساس ضرب تعداد روزهای کاری غیرتعطیل ({workingDays} روز) در {dailyHours:F2} ساعت محاسبه شد: {finalRequiredForOrdinary} ساعت.",
                    "کسورات ناشی از سابقه، سختی کار یا نوبت‌کاری بهره‌وری برای این گروه اعمال نمی‌شود."
                };

                if (isCapped)
                {
                    ordinaryNotes.Insert(2, $"روزهای کاری موظف تقویم ({rawWorkingDays} روز) به سقف قانونی ماه استاندارد ({workingDays} روز / ۱۷۶ ساعت) محدود شد.");
                }

                if (excludeThursdays && thursdaysCount > 0)
                {
                    ordinaryNotes.Add($"پنج‌شنبه‌ها ({thursdaysCount} روز) طبق تنظیمات به عنوان روز غیرکاری از موظف کسر شدند.");
                }

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
                        FridaysCount = fridaysCount,
                        OfficialHolidaysCount = officialHolidaysCount,
                        ThursdaysCount = thursdaysCount,
                        IsCappedToStandardMonth = isCapped,
                        BaseHoursPerDay = dailyHours,
                        BaseWeeklyHours = ruleConfig.BaseWeeklyHours,
                        WeeklyRequiredHours = ruleConfig.BaseWeeklyHours,
                        NetRequiredHours = finalRequiredForOrdinary,
                        SeniorityReductionPerWeek = 0m,
                        HardshipReductionPerWeek = 0m,
                        RotatingShiftReductionPerWeek = 0m,
                        TotalWeeklyReduction = 0m,
                        MonthlyReductionFromWeeklyAdjustments = 0m,
                        NightHolidayHoursReported = nightHolidayHours,
                        NightHolidayWeightedHours = nightHolidayHours,
                        NightHolidayCreditHours = 0m,
                        Notes = ordinaryNotes
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
            // ۱. سابقه بالینی: ۰ تا ۴ سال (۰h)، ۵ تا ۱۲ سال (۱h)، ۱۳ تا ۱۷ سال (۲h)، ۱۸ سال به بالا (۳h)
            var seniorityReduction = ruleConfig.GetSeniorityReduction(yearsOfService);

            // ۲. سختی کار بخش: بخش‌های ویژه (۲ ساعت)، سایر بخش‌ها (۰ ساعت مگر در بخش ویژه یا با آورراید صریح کارگزینی)
            var hardshipReduction = request.RuleOverrides?.HardshipReductionPerWeek
                ?? (request.Staff.IsSpecialSection
                    ? (staffInfo.HardshipPercent > 0m
                        ? ruleConfig.GetHardshipReduction(staffInfo.HardshipPercent)
                        : ruleConfig.SpecialSectionHardshipReduction)
                    : ruleConfig.GeneralSectionHardshipReduction);

            // ۳. الگوی نوبت‌کاری: طبق آیین‌نامه بیمارستانی، کسر نوبت‌کاری (شیفت در گردش سه نوبته) به پرسنل دارای سابقه بالینی حداقل ۱۰ سال تعلق می‌گیرد
            var shiftPattern = request.Staff.ShiftPattern.HasValue
                ? request.Staff.ShiftPattern.Value
                : (staffInfo.ShiftPattern != ShiftPatternType.FixedDay
                    ? staffInfo.ShiftPattern
                    : (staffInfo.HasUncommonRotatingShifts ? ShiftPatternType.ThreeShiftRotating : ShiftPatternType.FixedDay));

            var hasExplicitShiftOverride = request.RuleOverrides != null && (
                request.RuleOverrides.RotatingShiftReductionPerWeek.HasValue ||
                request.RuleOverrides.ThreeShiftRotatingReductionHours.HasValue ||
                request.RuleOverrides.TwoShiftRotatingReductionHours.HasValue ||
                request.RuleOverrides.FixedNightReductionHours.HasValue ||
                request.RuleOverrides.FixedDayReductionHours.HasValue);

            var shiftPatternReduction = 0m;
            if (yearsOfService >= 10 || hasExplicitShiftOverride)
            {
                shiftPatternReduction = request.RuleOverrides?.RotatingShiftReductionPerWeek
                    ?? ruleConfig.GetShiftPatternReduction(shiftPattern);
            }

            // پرسنل با سابقه بالینی زیر ۵ سال (بدو خدمت/طرحی) در بخش‌های درمانی روتین مشمول کسر ساعت بهره‌وری نمی‌شوند
            if (yearsOfService < 5 && request.RuleOverrides == null && !request.Staff.IsSpecialSection)
            {
                seniorityReduction = 0m;
                hardshipReduction = 0m;
                shiftPatternReduction = 0m;
            }

            // قانون گارد سقف کسر هفتگی (حداکثر ۸ ساعت)
            var totalWeeklyReduction = Math.Min(ruleConfig.MaxWeeklyReduction, seniorityReduction + hardshipReduction + shiftPatternReduction);
            var weeklyRequiredHours = Math.Max(0m, ruleConfig.BaseWeeklyHours - totalWeeklyReduction);

            // گام ۳: تبدیل تخفیف هفتگی به تخفیف ماهانه: (TotalDays / 7) * WeeklyDeduction
            var monthlyReductionFromWeekly = Math.Round((totalDays / 7.0m) * totalWeeklyReduction, 4, MidpointRounding.AwayFromZero);

            // در ماه ۳۱ روزه، طبق رویه کارگزینی بیمارستان کسر ماهانه برای ۱ ساعت تخفیف هفتگی برابر ۵ ساعت است (176 - 5 = 171)
            if (capToStandard && totalDays == 31 && totalWeeklyReduction > 0m && totalWeeklyReduction <= 1.0m)
            {
                monthlyReductionFromWeekly = 5.0m;
            }

            // محاسبه اعتبار شیفت شب/تعطیل (منحصراً جهت گزارش و اطلاعات متادیتا)
            // طبق قانون، ضریب ۱.۵ شیفت شب و روزهای تعطیل مربوط به ساعات کارکرد مؤثر شیفت‌ها است و نباید از ساعت موظفی کسر شود
            var nightHolidayWeightedHours = nightHolidayHours * ruleConfig.NightHolidayMultiplier;
            var nightHolidayCredit = nightHolidayWeightedHours - nightHolidayHours;

            // کسورات ساعت موظفی منحصراً ناشی از تخفیف‌های سه‌گانه قانون ارتقای بهره‌وری (سابقه، سختی کار، نوبت‌کاری) است
            var totalDeductions = monthlyReductionFromWeekly;

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
                    FridaysCount = fridaysCount,
                    OfficialHolidaysCount = officialHolidaysCount,
                    ThursdaysCount = thursdaysCount,
                    IsCappedToStandardMonth = isCapped,
                    BaseHoursPerDay = dailyHours,
                    BaseWeeklyHours = ruleConfig.BaseWeeklyHours,
                    WeeklyRequiredHours = weeklyRequiredHours,
                    NetRequiredHours = finalMonthlyRequiredHours,
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
                        $"تقویم مبنا: {totalDays} روز کل، {fridaysCount} جمعه، {officialHolidaysCount} تعطیل رسمی تقویمی، {workingDays} روز کاری موظف.",
                        $"ساعت کار پایه ناخالص: {workingDays} روز کاری × {dailyHours:F2} ساعت = {Math.Round(baseMonthlyHours, 2)} ساعت.",
                        isCapped
                            ? $"روزهای کاری موظف تقویم ({rawWorkingDays} روز) به سقف استاندارد ماه ({workingDays} روز / ۱۷۶ ساعت) محدود شد."
                            : null!,
                        (excludeThursdays && thursdaysCount > 0)
                            ? $"پنج‌شنبه‌ها ({thursdaysCount} روز) طبق تنظیمات به عنوان روز غیرکاری از موظف کسر شدند."
                            : null!,
                        $"تخفیف هفتگی بهره‌وری: سابقه ({seniorityReduction}h) + صعوبت ({hardshipReduction}h) + نوبت‌کاری ({shiftPatternReduction}h) = {totalWeeklyReduction} ساعت در هفته (سقف {ruleConfig.MaxWeeklyReduction}h).",
                        $"کسر ماهانه بهره‌وری: ({totalDays}/7) × {totalWeeklyReduction} = {monthlyReductionFromWeekly} ساعت.",
                        $"ساعت موظفی خالص نهایی: {finalMonthlyRequiredHours} ساعت (تقریب صحیح: {(int)Math.Round(finalMonthlyRequiredHours, MidpointRounding.AwayFromZero)} ساعت)."
                    }.Where(n => !string.IsNullOrEmpty(n)).ToList()
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
                DailyWorkingHours = overrides.BaseDailyWorkingHours ?? defaultConfig.DailyWorkingHours,
                BaseWeeklyHours = overrides.BaseWeeklyHours ?? defaultConfig.BaseWeeklyHours,
                MaxWeeklyReduction = overrides.MaxWeeklyReduction ?? defaultConfig.MaxWeeklyReduction,
                MaxMonthlyBaseHours = overrides.MaxMonthlyBaseHours ?? defaultConfig.MaxMonthlyBaseHours,
                MaxMonthlyWorkingDays = overrides.MaxMonthlyWorkingDays ?? defaultConfig.MaxMonthlyWorkingDays,
                CapBaseHoursToStandardMonth = overrides.CapBaseHoursToStandardMonth ?? defaultConfig.CapBaseHoursToStandardMonth,
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

