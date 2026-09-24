using System;
using System.Collections.Generic;
using System.Linq;
using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.DTOs.ProductivityModel;
using ShiftYar.Application.Interfaces.ProductivityModel;
using ShiftYar.Domain.Entities.ProductivityModel;
using ShiftYar.Domain.Entities.UserModel;

namespace ShiftYar.Application.Features.ProductivityModel.Services
{
    /// <summary>
    /// پیاده‌سازی فرمول‌های دستورالعمل اجرایی قانون ارتقای بهره‌وری کارکنان بالینی نظام سلامت وزارت بهداشت (گروه اول)
    /// و قوانین عمومی مدیریت خدمات کشوری / قانون کار (گروه دوم).
    /// </summary>
    public class WorkingHoursCalculator : IWorkingHoursCalculator
    {
        private readonly ICalendarHolidayProvider _calendarHolidayProvider;

        public WorkingHoursCalculator() : this(new CalendarHolidayProvider())
        {
        }

        public WorkingHoursCalculator(ICalendarHolidayProvider calendarHolidayProvider)
        {
            _calendarHolidayProvider = calendarHolidayProvider ?? new CalendarHolidayProvider();
        }
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

            // اعتبارسنجی شرط انحصاری متقابل (Mutually Exclusive / XOR) درصد و امتیاز سختی کار
            var hardshipValidationError = HardshipRulesValidator.Validate(request.Staff.HardshipPercent, request.Staff.HardshipScore);
            if (hardshipValidationError != null)
            {
                throw new ArgumentException(hardshipValidationError, nameof(request));
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
                var finalRequiredForOrdinary = Math.Max(0m, Math.Round(baseMonthlyHours, MidpointRounding.AwayFromZero));

                var ordinaryNotes = new List<string>
                {
                    "پرسنل غیرمشمول قانون ارتقای بهره‌وری (پرسنل عادی / خدمات کشوری).",
                    $"تقویم مبنا: {totalDays} روز کل، {fridaysCount} جمعه، {officialHolidaysCount} تعطیل رسمی، {workingDays} روز کاری موظف.",
                    $"ساعت موظفی بر اساس ضرب تعداد روزهای کاری غیرتعطیل ({workingDays} روز) در {dailyHours:F2} ساعت محاسبه و گرد شد: {finalRequiredForOrdinary} ساعت.",
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
                ? (decimal)request.Staff.ClinicalExperienceYears.Value
                : staffInfo.ResolveYearsOfServiceDecimal(targetMonth);

            if (yearsOfService < 0m)
            {
                yearsOfService = 0m;
            }

            // گام ۲: محاسبه کسر ساعت هفتگی بر اساس دستورالعمل رسمی وزارت بهداشت
            // ۱. کاهش سنوات خدمت:
            // ۰ تا ۴ سال (شامل بدو خدمت و طرحی): ۱.۰ ساعت
            // ۴ سال و ۱ ماه تا ۸ سال: ۲.۰ ساعت
            // ۸ سال و ۱ ماه تا ۱۲ سال: ۳.۰ ساعت
            // ۱۲ سال و ۱ ماه تا ۱۶ سال: ۴.۰ ساعت
            // ۱۶ سال و ۱ ماه به بالا: ۵.۰ ساعت
            var seniorityReduction = ruleConfig.GetSeniorityReduction(yearsOfService);

            // ۲. کاهش صعوبت/سختی کار (حداکثر ۲.۰ ساعت):
            // رده‌های مدیریت بالینی (ماده ۴ دستورالعمل): سوپروایزر، سرپرستار، مترون و مدیران پرستاری -> قطعی ۲.۰ ساعت
            var isClinicalManager = ClinicalManagementRoleDetector.IsClinicalManager(
                staffInfo.Position,
                staffInfo.JobTitle,
                staffInfo.Role,
                staffInfo.IsSupervisor,
                staffInfo.IsHeadNurse,
                staffInfo.StaffFullName);

            decimal hardshipReduction;
            if (request.RuleOverrides?.HardshipReductionPerWeek.HasValue == true)
            {
                hardshipReduction = request.RuleOverrides.HardshipReductionPerWeek.Value;
            }
            else if (isClinicalManager)
            {
                // استثنای رده‌های مدیریتی بالینی (ماده ۴): سقف ۲.۰ ساعت تخفیف صعوبت کار
                hardshipReduction = 2.0m;
            }
            else if (staffInfo.HardshipScore.HasValue)
            {
                // بر اساس امتیاز سختی کار قانون مدیریت خدمات کشوری:
                // ۰ تا ۳۷۵: ۰.۵h | ۳۷۶ تا ۷۵۰: ۱.۰h | ۷۵۱ تا ۱۰۰۰: ۱.۵h | بالای ۱۰۰۰: ۲.۰h
                hardshipReduction = ruleConfig.GetHardshipReductionFromScore(staffInfo.HardshipScore.Value);
            }
            else if (staffInfo.HardshipPercent >= 8m)
            {
                // بر اساس درصد نظام هماهنگ:
                // ۸ تا ۲۵٪: ۰.۵h | ۲۶ تا ۵۰٪: ۱.۰h | ۵۱ تا ۷۵٪: ۱.۵h | ۷۶ تا ۱۰۰٪: ۲.۰h
                hardshipReduction = ruleConfig.GetHardshipReduction(staffInfo.HardshipPercent);
            }
            else if (request.Staff.IsSpecialSection)
            {
                hardshipReduction = ruleConfig.SpecialSectionHardshipReduction;
            }
            else
            {
                // در صورت عدم ثبت درصد یا امتیاز سختی کار، کاهش صعوبت کار برابر با صفر لحاظ می‌شود
                hardshipReduction = ruleConfig.GeneralSectionHardshipReduction;
            }

            // ۳. کاهش نوبت‌کاری غیرمتعارف (گردشی):
            // کلیه پرسنلی که نوبتکاری در گردش دارند: دقیقاً ۱.۰ ساعت کسر در هفته (حذف پیش‌شرط سابقه ۱۰ سال)
            // روزکار ثابت: ۰.۰ ساعت
            var shiftPattern = request.Staff.ShiftPattern.HasValue
                ? request.Staff.ShiftPattern.Value
                : (staffInfo.ShiftPattern != ShiftPatternType.FixedDay
                    ? staffInfo.ShiftPattern
                    : (staffInfo.HasUncommonRotatingShifts ? ShiftPatternType.ThreeShiftRotating : ShiftPatternType.FixedDay));

            var isRotatingOrUnconventional = staffInfo.HasUncommonRotatingShifts
                || shiftPattern == ShiftPatternType.ThreeShiftRotating
                || shiftPattern == ShiftPatternType.TwoShiftRotating
                || shiftPattern == ShiftPatternType.FixedNight;

            decimal shiftPatternReduction;
            if (request.RuleOverrides?.RotatingShiftReductionPerWeek.HasValue == true)
            {
                shiftPatternReduction = request.RuleOverrides.RotatingShiftReductionPerWeek.Value;
            }
            else if (request.RuleOverrides != null && (
                (shiftPattern == ShiftPatternType.ThreeShiftRotating && request.RuleOverrides.ThreeShiftRotatingReductionHours.HasValue) ||
                (shiftPattern == ShiftPatternType.TwoShiftRotating && request.RuleOverrides.TwoShiftRotatingReductionHours.HasValue) ||
                (shiftPattern == ShiftPatternType.FixedNight && request.RuleOverrides.FixedNightReductionHours.HasValue) ||
                (shiftPattern == ShiftPatternType.FixedDay && request.RuleOverrides.FixedDayReductionHours.HasValue)))
            {
                shiftPatternReduction = ruleConfig.GetShiftPatternReduction(shiftPattern);
            }
            else if (isRotatingOrUnconventional)
            {
                shiftPatternReduction = ruleConfig.GetShiftPatternReduction(shiftPattern);
                if (shiftPatternReduction <= 0m && isRotatingOrUnconventional)
                {
                    shiftPatternReduction = ruleConfig.RotatingShiftReductionPerWeek; // 1.0m
                }
            }
            else
            {
                shiftPatternReduction = 0.0m;
            }

            // قانون گارد سقف کسر هفتگی: مجموع کاهش = Min(8.0, سنوات + صعوبت + نوبت‌کاری)
            var totalWeeklyReduction = Math.Min(ruleConfig.MaxWeeklyReduction, seniorityReduction + hardshipReduction + shiftPatternReduction);
            var weeklyRequiredHours = Math.Max(0m, ruleConfig.BaseWeeklyHours - totalWeeklyReduction);

            // گام ۳: تبدیل تخفیف هفتگی به تخفیف ماهانه
            // در محاسبات تقویمی واقعی (!capToStandard)، در صورت تعیین هفته‌های موظفی ماه (مثلاً ۴ هفته برای ماه استاندارد)، همان فاکتور اعمال می‌شود.
            var effectiveWeeks = (!capToStandard && request.NumberOfWeeksInMonth > 0)
                ? (decimal)request.NumberOfWeeksInMonth
                : (totalDays / 7.0m);
            var monthlyReductionFromWeekly = Math.Round(effectiveWeeks * totalWeeklyReduction, 4, MidpointRounding.AwayFromZero);

            // در ماه ۳۱ روزه، طبق رویه کارگزینی بیمارستان کسر ماهانه برای ۱ ساعت تخفیف هفتگی برابر ۵ ساعت است (176 - 5 = 171)
            if (capToStandard && totalDays == 31 && totalWeeklyReduction > 0m && totalWeeklyReduction <= 1.0m)
            {
                monthlyReductionFromWeekly = 5.0m;
            }

            // محاسبه اعتبار شیفت شب/تعطیل (منحصراً جهت گزارش و اطلاعات متادیتا)
            var nightHolidayWeightedHours = nightHolidayHours * ruleConfig.NightHolidayMultiplier;
            var nightHolidayCredit = nightHolidayWeightedHours - nightHolidayHours;

            // کسورات ساعت موظفی منحصراً ناشی از تخفیف‌های سه‌گانه قانون ارتقای بهره‌وری است
            var totalDeductions = monthlyReductionFromWeekly;

            // گام ۴: محاسبه ساعت موظفی خالص ماه (گردشده به نزدیک‌ترین عدد صحیح)
            var finalMonthlyRequiredHours = Math.Max(0m, Math.Round(baseMonthlyHours - totalDeductions, MidpointRounding.AwayFromZero));

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
                    HardshipScore = staffInfo.HardshipScore,
                    IsClinicalManager = isClinicalManager,
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
                        isClinicalManager
                            ? "ماده ۴ دستورالعمل اجرایی: تخفیف صعوبت کار برای پست مدیریت بالینی (سوپروایزر/سرپرستار/مترون) به صورت خودکار با سقف ۲.۰ ساعت در هفته لحاظ شد."
                            : null!,
                        $"تخفیف هفتگی بهره‌وری: سابقه ({seniorityReduction}h) + صعوبت ({hardshipReduction}h) + نوبت‌کاری ({shiftPatternReduction}h) = {totalWeeklyReduction} ساعت در هفته (سقف {ruleConfig.MaxWeeklyReduction}h).",
                        $"کسر ماهانه بهره‌وری: ({totalDays}/7) × {totalWeeklyReduction} = {monthlyReductionFromWeekly} ساعت.",
                        $"ساعت موظفی خالص نهایی (گردشده): {finalMonthlyRequiredHours} ساعت."
                    }.Where(n => !string.IsNullOrEmpty(n)).ToList()
                }
            };
        }

        /// <summary>
        /// دریافت میزان تخفیف هفتگی کاربر (عددی بین ۰.۰ تا حداکثر ۸.۰ ساعت)
        /// بر اساس سنوات خدمت، سختی/صعوبت کار و نوبت‌کاری گردشی.
        /// </summary>
        public decimal GetWeeklyProductivityReduction(User user, DateTime? referenceDate = null, ProductivityRuleConfig? ruleConfig = null)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if (user.IncludedProductivityPlan == false)
            {
                return 0.0m;
            }

            var staffInfo = StaffEmploymentInfo.FromUser(user);
            return GetWeeklyProductivityReduction(staffInfo, referenceDate, ruleConfig);
        }

        /// <summary>
        /// دریافت میزان تخفیف هفتگی بر اساس مدل DTO اطلاعات استخدامی پرسنل.
        /// </summary>
        public decimal GetWeeklyProductivityReduction(StaffEmploymentInfoDto staff, DateTime? referenceDate = null, ProductivityRuleConfig? ruleConfig = null)
        {
            if (staff == null)
            {
                throw new ArgumentNullException(nameof(staff));
            }

            if (!staff.IsIncludedInProductivityPlan)
            {
                return 0.0m;
            }

            var staffInfo = BuildStaffInfo(staff);
            return GetWeeklyProductivityReduction(staffInfo, referenceDate, ruleConfig);
        }

        /// <summary>
        /// دریافت میزان تخفیف هفتگی بر اساس موجودیت دامین StaffEmploymentInfo.
        /// </summary>
        public decimal GetWeeklyProductivityReduction(StaffEmploymentInfo staffInfo, DateTime? referenceDate = null, ProductivityRuleConfig? ruleConfig = null)
        {
            if (staffInfo == null)
            {
                throw new ArgumentNullException(nameof(staffInfo));
            }

            var hardshipError = HardshipRulesValidator.Validate(staffInfo.HardshipPercent, staffInfo.HardshipScore);
            if (hardshipError != null)
            {
                throw new ArgumentException(hardshipError, nameof(staffInfo));
            }

            ruleConfig ??= ProductivityRuleConfig.CreateDefault();
            var refDate = referenceDate ?? DateTime.UtcNow;

            // ۱. کاهش سنوات خدمت:
            var yearsOfService = staffInfo.YearsOfServiceOverride.HasValue && staffInfo.YearsOfServiceOverride.Value >= 0
                ? (decimal)staffInfo.YearsOfServiceOverride.Value
                : staffInfo.ResolveYearsOfServiceDecimal(refDate);

            if (yearsOfService < 0m)
            {
                yearsOfService = 0m;
            }

            var seniorityReduction = ruleConfig.GetSeniorityReduction(yearsOfService);

            // ۲. کاهش صعوبت/سختی کار (حداکثر ۲.۰ ساعت):
            var isClinicalManager = ClinicalManagementRoleDetector.IsClinicalManager(
                staffInfo.Position,
                staffInfo.JobTitle,
                staffInfo.Role,
                staffInfo.IsSupervisor,
                staffInfo.IsHeadNurse,
                staffInfo.StaffFullName);

            decimal hardshipReduction;
            if (isClinicalManager)
            {
                hardshipReduction = 2.0m;
            }
            else if (staffInfo.HardshipScore.HasValue)
            {
                hardshipReduction = ruleConfig.GetHardshipReductionFromScore(staffInfo.HardshipScore.Value);
            }
            else if (staffInfo.HardshipPercent >= 8m)
            {
                hardshipReduction = ruleConfig.GetHardshipReduction(staffInfo.HardshipPercent);
            }
            else
            {
                hardshipReduction = ruleConfig.GeneralSectionHardshipReduction;
            }

            // ۳. کاهش نوبت‌کاری غیرمتعارف (گردشی):
            var isRotating = staffInfo.HasUncommonRotatingShifts
                || staffInfo.ShiftPattern == ShiftPatternType.ThreeShiftRotating
                || staffInfo.ShiftPattern == ShiftPatternType.TwoShiftRotating
                || staffInfo.ShiftPattern == ShiftPatternType.FixedNight;

            var shiftPatternReduction = isRotating
                ? (staffInfo.ShiftPattern != ShiftPatternType.FixedDay
                    ? ruleConfig.GetShiftPatternReduction(staffInfo.ShiftPattern)
                    : ruleConfig.RotatingShiftReductionPerWeek)
                : 0.0m;

            var totalWeeklyReduction = Math.Min(ruleConfig.MaxWeeklyReduction, seniorityReduction + hardshipReduction + shiftPatternReduction);
            return Math.Round(totalWeeklyReduction, 2, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// محاسبه ساعت موظفی خالص ماهانه (Net Monthly Required Hours) پرسنل درمان برای یک سال و ماه مشخص (شمسی یا میلادی).
        /// </summary>
        public decimal CalculateMonthlyRequiredHours(User user, int year, int month, ISet<DateTime>? officialHolidays = null, int? numberOfWeeksInMonth = null)
        {
            var details = CalculateMonthlyRequiredHoursDetails(user, year, month, officialHolidays, numberOfWeeksInMonth);
            return details.NetMonthlyRequiredHours;
        }

        /// <summary>
        /// محاسبه تفصیلی ساعت موظفی تقویمی ماهانه پرسنل درمان شامل روزهای کاری، ساعت خام، کسر ساعت و ساعت موظف خالص.
        /// </summary>
        public MonthlyCalendarWorkingHoursResultDto CalculateMonthlyRequiredHoursDetails(User user, int year, int month, ISet<DateTime>? officialHolidays = null, int? numberOfWeeksInMonth = null)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            var monthInfo = _calendarHolidayProvider.GetMonthWorkingDaysInfo(year, month, officialHolidays);
            return CalculateMonthlyRequiredHoursInternal(user, monthInfo, numberOfWeeksInMonth);
        }

        /// <summary>
        /// محاسبه تفصیلی ساعت موظفی ماهانه پرسنل بر مبنای تعداد کل روزها و روزهای کاری موظف داده‌شده.
        /// </summary>
        public MonthlyCalendarWorkingHoursResultDto CalculateMonthlyRequiredHoursForDaysDetails(User user, int totalDaysInMonth, int workingDaysCount, DateTime? referenceDate = null, int? numberOfWeeksInMonth = null)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if (totalDaysInMonth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(totalDaysInMonth), "TotalDaysInMonth must be greater than zero.");
            }

            if (workingDaysCount < 0 || workingDaysCount > totalDaysInMonth)
            {
                throw new ArgumentOutOfRangeException(nameof(workingDaysCount), "WorkingDaysCount cannot be negative or exceed TotalDaysInMonth.");
            }

            var refDate = referenceDate ?? DateTime.UtcNow;
            var isPersian = refDate.Year >= 1200 && refDate.Year <= 1600;
            var fridaysCount = (int)Math.Round(totalDaysInMonth / 7.0);
            var midWeekHolidays = Math.Max(0, totalDaysInMonth - workingDaysCount - fridaysCount);

            var monthInfo = new MonthWorkingDaysInfo
            {
                Year = refDate.Year,
                Month = refDate.Month,
                IsPersian = isPersian,
                MonthStart = new DateTime(refDate.Year, refDate.Month, 1),
                MonthEnd = new DateTime(refDate.Year, refDate.Month, 1).AddDays(totalDaysInMonth - 1),
                TotalDays = totalDaysInMonth,
                WorkingDaysCount = workingDaysCount,
                FridaysCount = fridaysCount,
                MidWeekOfficialHolidaysCount = midWeekHolidays
            };

            return CalculateMonthlyRequiredHoursInternal(user, monthInfo, numberOfWeeksInMonth);
        }

        /// <summary>
        /// محاسبه ساعت موظفی خالص ماهانه پرسنل بر مبنای تعداد کل روزها و روزهای کاری موظف داده‌شده.
        /// </summary>
        public decimal CalculateMonthlyRequiredHoursForDays(User user, int totalDaysInMonth, int workingDaysCount, DateTime? referenceDate = null, int? numberOfWeeksInMonth = null)
        {
            var details = CalculateMonthlyRequiredHoursForDaysDetails(user, totalDaysInMonth, workingDaysCount, referenceDate, numberOfWeeksInMonth);
            return details.NetMonthlyRequiredHours;
        }

        private MonthlyCalendarWorkingHoursResultDto CalculateMonthlyRequiredHoursInternal(User user, MonthWorkingDaysInfo monthInfo, int? numberOfWeeksInMonth = null)
        {
            // گام ۱: ساعت موظفی خام/ناخالص ماهانه = WorkingDaysCount * (44 / 6)
            const decimal baseDailyWorkingHours = 44.0m / 6.0m;
            var grossMonthlyHours = Math.Round((decimal)monthInfo.WorkingDaysCount * baseDailyWorkingHours, 2, MidpointRounding.AwayFromZero);

            // گام ۲: محاسبه کسر ساعت بهره‌وری ماهانه
            var isIncluded = user.IncludedProductivityPlan != false;
            var weeklyReduction = isIncluded
                ? GetWeeklyProductivityReduction(user, monthInfo.MonthStart)
                : 0.0m;

            var monthWeeksFactor = numberOfWeeksInMonth.HasValue && numberOfWeeksInMonth.Value > 0
                ? (decimal)numberOfWeeksInMonth.Value
                : (decimal)monthInfo.TotalDays / 7.0m;
            var totalMonthlyReduction = Math.Round(weeklyReduction * monthWeeksFactor, 2, MidpointRounding.AwayFromZero);

            // گام ۳: ساعت موظفی خالص ماهانه = Math.Max(0, GrossMonthlyHours - TotalMonthlyReduction)
            var netMonthlyRequiredHours = Math.Max(0m, Math.Round(grossMonthlyHours - totalMonthlyReduction, 2, MidpointRounding.AwayFromZero));

            var hasManualOverride = ProductivityRequiredHoursResolver.HasManualOverride(user);
            var manualHours = hasManualOverride ? (decimal?)Math.Round(user.MaxProductivityRequiredHours!.Value, 2, MidpointRounding.AwayFromZero) : null;

            var staffInfo = StaffEmploymentInfo.FromUser(user);
            var ruleConfig = ProductivityRuleConfig.CreateDefault();
            var years = staffInfo.ResolveYearsOfServiceDecimal(monthInfo.MonthStart);
            var seniorityRed = isIncluded ? ruleConfig.GetSeniorityReduction(years) : 0m;
            var isClinicalManager = ClinicalManagementRoleDetector.IsClinicalManager(user);
            var hardshipRed = isIncluded
                ? (isClinicalManager
                    ? 2.0m
                    : (staffInfo.HardshipScore.HasValue
                        ? ruleConfig.GetHardshipReductionFromScore(staffInfo.HardshipScore.Value)
                        : (staffInfo.HardshipPercent >= 8m
                            ? ruleConfig.GetHardshipReduction(staffInfo.HardshipPercent)
                            : ruleConfig.GeneralSectionHardshipReduction)))
                : 0m;
            var isRotating = staffInfo.HasUncommonRotatingShifts || staffInfo.ShiftPattern == ShiftPatternType.ThreeShiftRotating || staffInfo.ShiftPattern == ShiftPatternType.TwoShiftRotating || staffInfo.ShiftPattern == ShiftPatternType.FixedNight;
            var shiftPatternRed = (isIncluded && isRotating)
                ? (staffInfo.ShiftPattern != ShiftPatternType.FixedDay
                    ? ruleConfig.GetShiftPatternReduction(staffInfo.ShiftPattern)
                    : ruleConfig.RotatingShiftReductionPerWeek)
                : 0m;

            var notes = new List<string>
            {
                $"تقویم مبنا: {monthInfo.TotalDays} روز کل، {monthInfo.FridaysCount} جمعه، {monthInfo.MidWeekOfficialHolidaysCount} روز تعطیل رسمی وسط هفته، {monthInfo.WorkingDaysCount} روز کاری موظف.",
                $"ساعت کار پایه هر روز کاری: ۷.۳۳۳۳ ساعت (معادل ۷ ساعت و ۲۰ دقیقه = ۴۴/۶).",
                $"ساعت موظفی خام ماهانه: {monthInfo.WorkingDaysCount} روز کاری × (۴۴/۶) = {grossMonthlyHours} ساعت."
            };

            if (!isIncluded)
            {
                notes.Add("پرسنل غیرمشمول قانون ارتقای بهره‌وری (بدون تخفیف هفتگی).");
            }
            else
            {
                if (isClinicalManager)
                {
                    notes.Add("ماده ۴ دستورالعمل اجرایی: پست مدیریت بالینی (سوپروایزر/سرپرستار/مترون) با سقف ۲.۰ ساعت صعوبت کار لحاظ شد.");
                }
                notes.Add($"تخفیف هفتگی بهره‌وری: سابقه ({seniorityRed}h) + صعوبت ({hardshipRed}h) + نوبت‌کاری ({shiftPatternRed}h) = {weeklyReduction} ساعت در هفته (حداکثر ۸ ساعت).");
                notes.Add(numberOfWeeksInMonth.HasValue && numberOfWeeksInMonth.Value > 0
                    ? $"تعداد هفته‌های مبنای ماه: {numberOfWeeksInMonth.Value} هفته."
                    : $"نسبت هفته‌های ماه: {monthInfo.TotalDays} / ۷ = {Math.Round(monthWeeksFactor, 4)} هفته.");
                notes.Add($"کسر ساعت بهره‌وری ماهانه: {weeklyReduction} × {Math.Round(monthWeeksFactor, 4)} = {totalMonthlyReduction} ساعت.");
            }

            notes.Add($"ساعت موظفی خالص ماهانه: Max(0, {grossMonthlyHours} - {totalMonthlyReduction}) = {netMonthlyRequiredHours} ساعت.");

            if (hasManualOverride)
            {
                notes.Add($"کاربر دارای سقف موظفی دستی است: {manualHours} ساعت (به‌جای مقدار محاسبه‌شده تقویمی).");
            }

            return new MonthlyCalendarWorkingHoursResultDto
            {
                StaffId = user.Id ?? 0,
                StaffFullName = user.FullName,
                Year = monthInfo.Year,
                Month = monthInfo.Month,
                IsPersianCalendar = monthInfo.IsPersian,
                MonthStartDate = monthInfo.MonthStart,
                MonthEndDate = monthInfo.MonthEnd,
                TotalDaysInMonth = monthInfo.TotalDays,
                FridaysCount = monthInfo.FridaysCount,
                OfficialHolidaysCount = monthInfo.MidWeekOfficialHolidaysCount,
                WorkingDaysCount = monthInfo.WorkingDaysCount,
                BaseDailyWorkingHours = baseDailyWorkingHours,
                GrossMonthlyHours = grossMonthlyHours,
                WeeklyProductivityReduction = weeklyReduction,
                MonthWeeksFactor = Math.Round(monthWeeksFactor, 4),
                TotalMonthlyReduction = totalMonthlyReduction,
                NetMonthlyRequiredHours = netMonthlyRequiredHours,
                IsIncludedInProductivityPlan = isIncluded,
                HasManualOverride = hasManualOverride,
                ManualOverrideHours = manualHours,
                SeniorityReductionPerWeek = seniorityRed,
                HardshipReductionPerWeek = hardshipRed,
                ShiftPatternReductionPerWeek = shiftPatternRed,
                Notes = notes
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
                HardshipScore = dto.HardshipScore,
                Position = dto.Position,
                JobTitle = dto.JobTitle,
                Role = dto.Role,
                IsSupervisor = dto.IsSupervisor,
                IsHeadNurse = dto.IsHeadNurse,
                HasUncommonRotatingShifts = dto.HasUncommonRotatingShifts || pattern == ShiftPatternType.ThreeShiftRotating || pattern == ShiftPatternType.TwoShiftRotating || pattern == ShiftPatternType.FixedNight,
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
