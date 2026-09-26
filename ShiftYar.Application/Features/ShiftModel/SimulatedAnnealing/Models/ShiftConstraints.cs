using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ShiftYar.Application.DTOs.ProductivityModel;
using ShiftYar.Application.Common.Utilities;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;
using static ShiftYar.Domain.Enums.UserModel.UserEnums;

namespace ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models
{
    /// <summary>
    /// محدودیت‌های شیفت‌بندی
    /// </summary>
    public class ShiftConstraints
    {
        public int DepartmentId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<UserConstraint> UserConstraints { get; set; } = new List<UserConstraint>();
        public List<ShiftRequirement> ShiftRequirements { get; set; } = new List<ShiftRequirement>();
        /// <summary>روزهای تعطیل بازه (پرسنل فیکس در این روزها شیفت نمی‌گیرند؛ ظرفیت تخصص می‌تواند متفاوت باشد)</summary>
        public HashSet<DateTime> HolidayDates { get; set; } = new HashSet<DateTime>();
        /// <summary>سقف‌گذاری ساعت کار پایه بر مبنای ماه استاندارد (۱۷۶ ساعت / ۲۴ روز کاری). پیش‌فرض null (معادل false برای محاسبات تقویمی دقیق)</summary>
        public bool? CapBaseHoursToStandardMonth { get; set; }

        public bool IsHoliday(DateTime date) => HolidayDates.Contains(date.Date);

        /// <summary>
        /// شب آخر هفته/تعطیل: خود روز تعطیل، یا شب روز قبل از تعطیل.
        /// </summary>
        public bool IsHolidayWeekendNight(DateTime date) =>
            HolidayWeekendNightRules.IsHolidayWeekendNight(date, IsHoliday);

        /// <summary>فعال‌سازی توزیع نرم شیفت صبح بر اساس سابقه (بعد از سهمیه دقیق).</summary>
        public bool EnableMorningShiftDistributionBySeniority { get; set; }

        /// <summary>۰=سابقه بیشتر، ۱=سابقه کمتر، ۲=خنثی.</summary>
        public int MorningShiftDistributionType { get; set; } = 2;

        /// <summary>فعال‌سازی توزیع نرم شیفت عصر بر اساس سابقه.</summary>
        public bool EnableEveningShiftDistributionBySeniority { get; set; }

        /// <summary>۰=سابقه بیشتر، ۱=سابقه کمتر، ۲=خنثی.</summary>
        public int EveningShiftDistributionType { get; set; } = 2;

        /// <summary>فعال‌سازی توزیع نرم شیفت شب بر اساس سابقه (بعد از سهمیه دقیق).</summary>
        public bool EnableNightShiftDistributionBySeniority { get; set; }

        /// <summary>۰=سابقه بیشتر، ۱=سابقه کمتر، ۲=خنثی.</summary>
        public int NightShiftDistributionType { get; set; } = 2;

        /// <summary>شیب مشترک وزن‌دهی سابقه برای صبح/عصر/شب.</summary>
        public double SeniorityDistributionSlope { get; set; } = 1.0;

        /// <summary>فعال‌سازی توزیع نرم اضافه کار بر اساس سابقه و تمایل بخش.</summary>
        public bool EnableOvertimeDistributionBySeniority { get; set; }

        /// <summary>نوع ترجیح اضافه کار: ۰=علاقه‌مند (سابقه بیشتر)، ۱=گریزان (سابقه کمتر)، ۲=خنثی.</summary>
        public int OvertimePreferenceType { get; set; } = 2;

        /// <summary>شیب اختصاصی وزن‌دهی سابقه برای توزیع اضافه کار (پیش‌فرض: 1.0).</summary>
        public double OvertimeSeniorityDistributionSlope { get; set; } = 1.0;

        public GlobalConstraints GlobalConstraints { get; set; } = new GlobalConstraints();
        // قوانین قطعی (سراسری برای همه دپارتمان‌ها)
        public HardRuleSet HardRules { get; set; } = HardRuleSet.CreateDefault();
        // وزن قوانین اختیاری (قابل تنظیم per-department)
        public SoftRuleWeights SoftWeights { get; set; } = SoftRuleWeights.CreateDefault();
    }

    /// <summary>
    /// محدودیت‌های کاربر
    /// </summary>
    public class UserConstraint
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public UserGender Gender { get; set; }
        public int SpecialtyId { get; set; }
        public string SpecialtyName { get; set; } = string.Empty;
        public List<DateTime> UnavailableDates { get; set; } = new List<DateTime>();      //تاریخ‌های غیرقابل دسترس (کل روز)
        public List<ShiftSlotConstraint> UnavailableShiftSlots { get; set; } = new List<ShiftSlotConstraint>(); // عدم حضور در شیفت مشخص
        public List<ShiftSlotConstraint> RequiredShiftSlots { get; set; } = new List<ShiftSlotConstraint>(); // حضور قطعی در شیفت مشخص
        public List<DateTime> RequiredPresenceDates { get; set; } = new List<DateTime>(); // حضور قطعی حداقل در یک شیفت آن روز
        public List<ShiftLabel> PreferredShifts { get; set; } = new List<ShiftLabel>();   //شیفت‌های ترجیحی
        public List<ShiftLabel> UnwantedShifts { get; set; } = new List<ShiftLabel>();    //شیفت‌های ناخواسته
        public int MaxConsecutiveShifts { get; set; } = 3;    //حداکثر شیفت‌های متوالی
        public int MinRestDaysBetweenShifts { get; set; } = 1;  //حداقل استراحت بین شیفت‌ها
        public int MaxShiftsPerWeek { get; set; } = 5;   //حداکثر شیفت در هر هفته
        public int MaxNightShiftsPerMonth { get; set; } = 8;    //حداکثر شیفت شب در ماه (حالت بدون سهمیه دقیق)
        /// <summary>حداقل تعداد شیفت شب برای کاربر در بازه برنامه‌ریزی.</summary>
        public int? ExactNightShiftCount { get; set; }
        /// <summary>حداقل تعداد شیفت شب روی روزهای تعطیل/آخرهفته.</summary>
        public int? ExactHolidayWeekendNightShiftCount { get; set; }
        /// <summary>null = مشارکت در مازاد شب (پیش‌فرض)؛ false = بدون مازاد.</summary>
        public bool? NightFallbackParticipation { get; set; }
        /// <summary>null = مشارکت در مازاد شب تعطیل/آخرهفته (پیش‌فرض)؛ false = بدون مازاد.</summary>
        public bool? HolidayWeekendNightFallbackParticipation { get; set; }
        /// <summary>حداقل فاصله روزهای تقویمی بین دو شیفت شب (۱ = بدون شب متوالی).</summary>
        public int MinDaysBetweenNightShifts { get; set; } = 1;
        public bool HasExactNightQuota => ExactNightShiftCount.HasValue;
        /// <summary>حداقل تعداد شیفت صبح در بازه.</summary>
        public int? ExactMorningShiftCount { get; set; }
        /// <summary>حداقل تعداد شیفت صبح در روزهای تعطیل.</summary>
        public int? ExactHolidayMorningShiftCount { get; set; }
        /// <summary>null = مشارکت در مازاد (پیش‌فرض)؛ false = بدون مازاد؛ true = مشارکت صریح.</summary>
        public bool? MorningFallbackParticipation { get; set; }
        /// <summary>مشارکت در توزیع مازاد شیفت صبح در روزهای تعطیل.</summary>
        public bool? MorningHolidayFallbackParticipation { get; set; }
        public bool HasExactMorningQuota => ExactMorningShiftCount.HasValue;
        /// <summary>حداقل تعداد شیفت عصر در بازه.</summary>
        public int? ExactEveningShiftCount { get; set; }
        /// <summary>حداقل تعداد شیفت عصر در روزهای تعطیل.</summary>
        public int? ExactHolidayEveningShiftCount { get; set; }
        /// <summary>null = مشارکت در مازاد (پیش‌فرض)؛ false = بدون مازاد؛ true = مشارکت صریح.</summary>
        public bool? EveningFallbackParticipation { get; set; }
        /// <summary>مشارکت در توزیع مازاد شیفت عصر در روزهای تعطیل.</summary>
        public bool? EveningHolidayFallbackParticipation { get; set; }
        public bool HasExactEveningQuota => ExactEveningShiftCount.HasValue;

        /// <summary>حداقل/هدف تعداد کل انتساب صبح+عصر (سهمیه ترکیبی).</summary>
        public int? MorningEveningShiftCount { get; set; }
        public bool? MorningEveningFallbackParticipation { get; set; }
        public int? MorningEveningHolidayCount { get; set; }
        public bool? MorningEveningHolidayFallback { get; set; }
        /// <summary>حداقل/هدف تعداد کل انتساب صبح+شب (سهمیه ترکیبی).</summary>
        public int? MorningNightShiftCount { get; set; }
        public bool? MorningNightFallbackParticipation { get; set; }
        public int? MorningNightHolidayCount { get; set; }
        public bool? MorningNightHolidayFallback { get; set; }

        /// <summary>وضعیت فعال بودن تخصیص شیفت‌های متناوب هفتگی (صبح و عصر)</summary>
        public bool IsWeeklyAlternatingActive { get; set; }
        /// <summary>شیفت انتخابی هفته اول ماه: صبح (Morning) یا عصر (Evening)</summary>
        public ShiftLabel? FirstWeekShiftLabel { get; set; }

        public bool CanBeShiftManager { get; set; }
        /// <summary>null = مسئول نیست؛ 1 = سطح ۱؛ 2 = سطح ۲</summary>
        public byte? ShiftManagerLevel { get; set; }
        public bool IsActive { get; set; } = true; // وضعیت فعال بودن کاربر
        public ShiftTypes ShiftType { get; set; }
        public ShiftSubTypes ShiftSubType { get; set; }
        public TwoShiftRotationPattern? TwoShiftRotationPattern { get; set; }
        /// <summary>
        /// شیفت‌های مجاز تکی (مشتق از AllowedShiftPermissions). خالی = همه مجاز (سازگاری عقب‌رو).
        /// </summary>
        public List<ShiftLabel> AllowedShiftLabels { get; set; } = new List<ShiftLabel>();
        /// <summary>مجوزهای صریح نوع شیفت (تکی + ترکیب روزانه).</summary>
        public UserShiftPermission AllowedShiftPermissions { get; set; } = UserShiftPermission.None;
        public bool IncludedInProductivityPlan { get; set; }
        public decimal? ProductivityRequiredHours { get; set; }
        public WorkingHoursCalculationResultDto? ProductivitySnapshot { get; set; }
        public MonthlyCalendarWorkingHoursResultDto? MonthlyCalendarSnapshot { get; set; }
        public decimal HardshipPercent { get; set; }
        public decimal? HardshipPercentage
        {
            get => HardshipPercent;
            set => HardshipPercent = value ?? 0m;
        }
        public decimal? HardshipScore { get; set; }
        public decimal? HardshipPoints
        {
            get => HardshipScore;
            set => HardshipScore = value;
        }
        public string? Position { get; set; }
        public string? JobTitle { get; set; }
        public bool? IsSupervisor { get; set; }
        public bool? IsHeadNurse { get; set; }
        public bool OvertimeConsent { get; set; }
        /// <summary>پرسنل طرحی؛ در پر کردن موظفی بعد از غیرطرحی اولویت دارد.</summary>
        public bool? IsProjectPersonnel { get; set; }
        public double MaxMonthlyOvertimeHours { get; set; } = ProductivityWorkedHoursCalculator.DefaultMaxMonthlyOvertimeHours;
        public double MaxConsecutiveWorkHours { get; set; } = ProductivityWorkedHoursCalculator.DefaultMaxConsecutiveWorkHours;

        // Fairness history (computed from previous months)
        public int RecentTotalShifts { get; set; } = 0;
        public Dictionary<ShiftLabel, int> RecentLabelCounts { get; set; } = new Dictionary<ShiftLabel, int>();

        // حداقل شیفت‌های مورد نیاز برای هر نوع شیفت (از تنظیمات دپارتمان)
        public Dictionary<ShiftLabel, int> MinimumShiftsRequired { get; set; } = new Dictionary<ShiftLabel, int>();

        // حداکثر شیفت‌های مجاز برای هر نوع شیفت (از تنظیمات دپارتمان)
        public Dictionary<ShiftLabel, int> MaxShiftsPerMonth { get; set; } = new Dictionary<ShiftLabel, int>();

        // سال‌های تجربه کاربر (برای محاسبه وزن‌های مربوط به سابقه)
        public int ExperienceYears { get; set; } = 0;

        // تاریخ استخدام کاربر (برای محاسبه تجربه)
        public DateTime? DateOfEmployment { get; set; }
    }

    /// <summary>
    /// نیازمندی‌های شیفت
    /// </summary>
    public class ShiftRequirement
    {
        public int ShiftId { get; set; }
        public ShiftLabel ShiftLabel { get; set; }
        public int DepartmentId { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public double DurationHours { get; set; }
        public int DurationMinutes { get; set; }

        /// <summary>ساعات عملکرد روز عادی (خارج از طرح) — null یعنی fallback به Start/End و ضریب شب/تعطیل.</summary>
        public double? WeekdayNonProductivityHours { get; set; }
        public double? HolidayNonProductivityHours { get; set; }
        public double? WeekdayProductivityPlanHours { get; set; }
        public double? HolidayProductivityPlanHours { get; set; }

        /// <summary>حداقل تعداد مسئول شیفت در این نوبت (۰ = بدون الزام).</summary>
        public int ManagerRequiredCount { get; set; }
        /// <summary>حداقل تعداد مسئول سطح ۱ در این نوبت.</summary>
        public int ManagerMinLevel1Count { get; set; }

        public List<SpecialtyRequirement> SpecialtyRequirements { get; set; } = new List<SpecialtyRequirement>();
    }

    /// <summary>
    /// نیازمندی تخصص در شیفت — با پشتیبانی از ظرفیت متفاوت روز تعطیل.
    /// فیلدهای بدون پیشوند Holiday مربوط به روز غیرتعطیل‌اند.
    /// اگر Holiday* مقدار نداشته باشد، همان غیرتعطیل اعمال می‌شود.
    /// </summary>
    public class SpecialtyRequirement
    {
        public int SpecialtyId { get; set; }
        public string SpecialtyName { get; set; } = string.Empty;

        public int RequiredMaleCount { get; set; }
        public int RequiredFemaleCount { get; set; }
        public int RequiredTotalCount { get; set; }
        public int OnCallMaleCount { get; set; }
        public int OnCallFemaleCount { get; set; }
        public int OnCallTotalCount { get; set; }

        public int? HolidayRequiredMaleCount { get; set; }
        public int? HolidayRequiredFemaleCount { get; set; }
        public int? HolidayRequiredTotalCount { get; set; }
        public int? HolidayOnCallMaleCount { get; set; }
        public int? HolidayOnCallFemaleCount { get; set; }
        public int? HolidayOnCallTotalCount { get; set; }

        public SpecialtyDayCounts ForDay(bool isHoliday)
        {
            if (!isHoliday)
            {
                return new SpecialtyDayCounts(
                    RequiredMaleCount,
                    RequiredFemaleCount,
                    RequiredTotalCount,
                    OnCallMaleCount,
                    OnCallFemaleCount,
                    OnCallTotalCount);
            }

            return new SpecialtyDayCounts(
                HolidayRequiredMaleCount ?? RequiredMaleCount,
                HolidayRequiredFemaleCount ?? RequiredFemaleCount,
                HolidayRequiredTotalCount ?? RequiredTotalCount,
                HolidayOnCallMaleCount ?? OnCallMaleCount,
                HolidayOnCallFemaleCount ?? OnCallFemaleCount,
                HolidayOnCallTotalCount ?? OnCallTotalCount);
        }
    }

    /// <summary>نیازمندی مؤثر تخصص برای یک روز مشخص (تعطیل یا غیرتعطیل)</summary>
    public readonly record struct SpecialtyDayCounts(
        int RequiredMaleCount,
        int RequiredFemaleCount,
        int RequiredTotalCount,
        int OnCallMaleCount,
        int OnCallFemaleCount,
        int OnCallTotalCount);

    /// <summary>
    /// محدودیت‌های سراسری
    /// </summary>
    public class GlobalConstraints
    {
        public bool AllowConsecutiveNightShifts { get; set; } = false;  //اجازه شیفت‌های شبانه متوالی
        public int MaxConsecutiveNightShifts { get; set; } = 2; //حداکثر شیفت‌های شب متوالی
        public bool RequireGenderBalance { get; set; } = true;  //نیازمند تعادل جنسیتی
        public double MinGenderBalanceRatio { get; set; } = 0.3; // حداقل 30% از هر جنسیت
        public bool PreferSpecialtyMatch { get; set; } = true;  //مطابقت با تخصص
        public int MaxShiftsPerDay { get; set; } = 2;   // حداکثر شیفت در روز (صبح+عصر مجاز)
        public bool AllowWeekendShifts { get; set; } = true;    //مجاز کردن شیفت‌های آخر هفته
        public bool RequireShiftManager { get; set; } = true;   //نیاز به مدیر شیفت (legacy؛ الزام واقعی از ShiftRequirement)
    }

    /// <summary>
    /// قید حضور/عدم‌حضور در یک شیفت مشخص در تاریخ معین
    /// </summary>
    public class ShiftSlotConstraint
    {
        public DateTime Date { get; set; }
        public ShiftLabel ShiftLabel { get; set; }
        /// <summary>
        /// اگر مشخص باشد، اجبار دقیقاً روی همین ShiftId انجام می‌شود
        /// (برای وقتی فرانت اشتباهاً Shift.Id را به‌جای ShiftLabel می‌فرستد).
        /// </summary>
        public int? ShiftId { get; set; }
    }


    /// <summary>
	/// قوانین قطعی که باید همیشه رعایت شوند
	/// </summary>
	public class HardRuleSet
    {
        public bool ForbidDuplicateDailyAssignments { get; set; } = true; // ممنوعیت تکرار همان نوع شیفت در یک روز
        public bool EnforceMaxShiftsPerDay { get; set; } = true; // از GlobalConstraints.MaxShiftsPerDay (پیش‌فرض ۲ = صبح+عصر)
        public bool EnforceMinRestDays { get; set; } = true; // حداقل فاصله استراحت
        public bool EnforceMaxConsecutiveShifts { get; set; } = true; // حداکثر شیفت‌های متوالی
        public bool EnforceWeeklyMaxShifts { get; set; } = false; // می‌تواند نرم نیز باشد
        public bool EnforceNightShiftMonthlyCap { get; set; } = false; // می‌تواند نرم نیز باشد
        public bool EnforceSpecialtyCapacity { get; set; } = true; // عدم تجاوز از ظرفیت موردنیاز هر تخصص/شیفت/روز
        public bool EnforceProductivityHours { get; set; } = true; // رعایت سقف ساعات موظفی بهره‌وری
        public bool EnforceMaxConsecutiveWorkHours { get; set; } = true; // حداکثر ۱۲ ساعت کار متوالی
        public bool EnforceOvertimeConsent { get; set; } = true; // اضافه‌کاری فقط با رضایت پرسنل

        /// <summary>
        /// true: روز بعد از شب می‌تواند در صورت نیاز عصر بگیرد (صبح همچنان ممنوع).
        /// false: عصر روز بعد از شب ممنوع است (پیش‌فرض).
        /// </summary>
        public bool AllowEveningAfterNightShift { get; set; } = false;

        /// <summary>
        /// true: روز بعد از شب می‌تواند در صورت نیاز شب بگیرد (شب متوالی؛ صبح همچنان ممنوع).
        /// false: شب روز بعد از شب ممنوع است (پیش‌فرض).
        /// </summary>
        public bool AllowNightShiftAfterNightShift { get; set; } = false;

        /// <summary>آیا شیفت مشخص در روز بعد از شب ممنوع است؟</summary>
        public bool IsForbiddenOnDayAfterNight(ShiftLabel label) =>
            label == ShiftLabel.Morning ||
            (label == ShiftLabel.Evening && !AllowEveningAfterNightShift) ||
            (label == ShiftLabel.Night && !AllowNightShiftAfterNightShift);

        public static HardRuleSet CreateDefault()
        {
            return new HardRuleSet();
        }
    }

    /// <summary>
    /// وزن قوانین نرم (اختیاری) که per-department قابل تنظیم هستند
    /// </summary>
    public class SoftRuleWeights
    {
        public double GenderBalanceWeight { get; set; } = 1.0;
        public double SpecialtyPreferenceWeight { get; set; } = 1.0;
        public double UserUnwantedShiftWeight { get; set; } = 1.0;
        public double UserPreferredShiftWeight { get; set; } = 1.0; // به عنوان پاداش منفی استفاده می‌شود
        public double WeeklyMaxWeight { get; set; } = 1.0;
        public double MonthlyNightCapWeight { get; set; } = 1.0;

        // Fairness weights
        public double FairShiftCountBalanceWeight { get; set; } = 1.0; // تعادل تعداد شیفت بین افراد در این ماه
        public double FairWorkedHoursBalanceWeight { get; set; } = 8.0; // تعادل ساعات مؤثر کار (با ضریب شب/تعطیل)
        public double FairNightShiftBalanceWeight { get; set; } = 2.5; // تعادل تعداد شیفت شب بین افراد واجد شرایط
        public double MorningEveningBalanceWeight { get; set; } = 8.0; // تناسب تعداد شیفت صبح و عصر درون هر کاربر
        /// <summary>تعادل تعداد صبح/عصر بین کاربران گردشی (نه فقط درون یک نفر).</summary>
        public double FairMorningEveningPeerWeight { get; set; } = 4.0;
        /// <summary>تعادل صبح/عصر روزهای تعطیل بین کاربران گردشی (جدا از تعادل ماهانه).</summary>
        public double FairHolidayMorningEveningPeerWeight { get; set; } = 8.0;
        /// <summary>جریمه تراکم روزهای کاری (چند روز متوالی یا پر کردن کل هفته).</summary>
        public double WorkdaySpreadWeight { get; set; } = 1.5;
        /// <summary>جریمه OFFهای طولانی بدون درخواست OFF تمام‌روز تأییدشده.</summary>
        public double OffSpreadWeight { get; set; } = 1.0;
        public double ExactNightQuotaWeight { get; set; } = 200.0; // جریمه کسری از حداقل سهمیه شب
        public double ExtraShiftRotationWeight { get; set; } = 1.0;     // جلوگیری از دادن شیفت اضافه به کسانی که اخیراً زیاد گرفته‌اند
        public double ShiftLabelBalanceWeight { get; set; } = 1.0;      // تعادل Morning/Evening/Night برای کاربران گردشی
        public int FairnessLookbackMonths { get; set; } = 1;            // بازه سابقه برای محاسبات عدالت

        // Seniority distribution by shift label
        public double MorningShiftDistributionBySeniorityWeight { get; set; } = 1.0;
        public double EveningShiftDistributionBySeniorityWeight { get; set; } = 1.0;
        public double NightShiftDistributionBySeniorityWeight { get; set; } = 1.0; // وزن توزیع شیفت‌های شب بر اساس سابقه
        public double ProductivityOvertimeWeight { get; set; } = 6.0; // وزن جریمه مازاد ساعات موظفی
        public double ProductivityShortfallWeight { get; set; } = 8.0; // وزن جریمه کمبود ساعات موظفی
        public double ShiftManagerRequirementWeight { get; set; } = 2.0; // وزن الزام ترکیب مسئول شیفت
        public double OvertimeDistributionWeight { get; set; } = 1.0; // وزن توزیع اضافه کار بر اساس سابقه

        public static SoftRuleWeights CreateDefault()
        {
            return new SoftRuleWeights();
        }
    }


    /// <summary>
    /// پارامترهای الگوریتم Simulated Annealing
    /// </summary>
    public class SimulatedAnnealingParameters
    {
        public double InitialTemperature { get; set; } = 1000.0;
        public double FinalTemperature { get; set; } = 0.1;
        /// <summary>
        /// نرخ سردسازی. مقدار ۰.۹۵ فقط حدود ۱۸۰ تکرار می‌دهد؛ ۰.۹۹۷ حدود ۳۰۰۰ تکرار مفید می‌سازد.
        /// </summary>
        public double CoolingRate { get; set; } = 0.997;
        public int MaxIterations { get; set; } = 10000;
        public int MaxIterationsWithoutImprovement { get; set; } = 1000;
        public int MaxNeighborsPerIteration { get; set; } = 10;
        public double PenaltyWeight { get; set; } = 1000.0; // وزن جریمه برای نقض محدودیت‌ها
        public int? RandomSeed { get; set; }
    }

    /// <summary>
    /// آمارهای الگوریتم
    /// </summary>
    public class AlgorithmStatistics
    {
        public int TotalIterations { get; set; }
        public int AcceptedMoves { get; set; }
        public int RejectedMoves { get; set; }
        public double BestScore { get; set; } = double.MaxValue;
        public double CurrentScore { get; set; } = double.MaxValue;
        public double CurrentTemperature { get; set; }
        public TimeSpan ExecutionTime { get; set; }
        public List<double> ScoreHistory { get; set; } = new List<double>();
        public List<double> TemperatureHistory { get; set; } = new List<double>();
    }
}
