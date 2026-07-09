using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ShiftYar.Application.DTOs.ProductivityModel;
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
        public List<DateTime> UnavailableDates { get; set; } = new List<DateTime>();      //تاریخ‌های غیرقابل دسترس
        public List<ShiftLabel> PreferredShifts { get; set; } = new List<ShiftLabel>();   //شیفت‌های ترجیحی
        public List<ShiftLabel> UnwantedShifts { get; set; } = new List<ShiftLabel>();    //شیفت‌های ناخواسته
        public int MaxConsecutiveShifts { get; set; } = 3;    //حداکثر شیفت‌های متوالی
        public int MinRestDaysBetweenShifts { get; set; } = 1;  //حداقل استراحت بین شیفت‌ها
        public int MaxShiftsPerWeek { get; set; } = 5;   //حداکثر شیفت در هر هفته
        public int MaxNightShiftsPerMonth { get; set; } = 8;    //حداکثر شیفت شب در ماه
        public bool CanBeShiftManager { get; set; }
        public bool IsActive { get; set; } = true; // وضعیت فعال بودن کاربر
        public ShiftTypes ShiftType { get; set; }
        public ShiftSubTypes ShiftSubType { get; set; }
        public TwoShiftRotationPattern? TwoShiftRotationPattern { get; set; }
        public bool IncludedInProductivityPlan { get; set; }
        public decimal? ProductivityRequiredHours { get; set; }
        public WorkingHoursCalculationResultDto? ProductivitySnapshot { get; set; }

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
        public List<SpecialtyRequirement> SpecialtyRequirements { get; set; } = new List<SpecialtyRequirement>();
    }

    /// <summary>
    /// نیازمندی تخصص در شیفت
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
    }

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
        public int MaxShiftsPerDay { get; set; } = 1;   //حداکثر شیفت در روز
        public bool AllowWeekendShifts { get; set; } = true;    //مجاز کردن شیفت‌های آخر هفته
        public bool RequireShiftManager { get; set; } = true;   //نیاز به مدیر شیفت
    }


    /// <summary>
	/// قوانین قطعی که باید همیشه رعایت شوند
	/// </summary>
	public class HardRuleSet
    {
        public bool ForbidDuplicateDailyAssignments { get; set; } = true; // هر کاربر حداکثر یک شیفت در روز
        public bool EnforceMaxShiftsPerDay { get; set; } = true; // از GlobalConstraints.MaxShiftsPerDay
        public bool EnforceMinRestDays { get; set; } = true; // حداقل فاصله استراحت
        public bool EnforceMaxConsecutiveShifts { get; set; } = true; // حداکثر شیفت‌های متوالی
        public bool EnforceWeeklyMaxShifts { get; set; } = false; // می‌تواند نرم نیز باشد
        public bool EnforceNightShiftMonthlyCap { get; set; } = false; // می‌تواند نرم نیز باشد
        public bool EnforceSpecialtyCapacity { get; set; } = true; // عدم تجاوز از ظرفیت موردنیاز هر تخصص/شیفت/روز
        public bool EnforceProductivityHours { get; set; } = true; // رعایت سقف ساعات موظفی بهره‌وری

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
        public double FairShiftCountBalanceWeight { get; set; } = 0.0; // تعادل تعداد شیفت بین افراد در این ماه
        public double ExtraShiftRotationWeight { get; set; } = 0.0;     // جلوگیری از دادن شیفت اضافه به کسانی که اخیراً زیاد گرفته‌اند
        public double ShiftLabelBalanceWeight { get; set; } = 0.0;      // تعادل Morning/Evening/Night برای کاربران گردشی
        public int FairnessLookbackMonths { get; set; } = 1;            // بازه سابقه برای محاسبات عدالت

        // Night shift distribution weights
        public double NightShiftDistributionBySeniorityWeight { get; set; } = 0.0; // وزن توزیع شیفت‌های شب بر اساس سابقه
        public double ProductivityOvertimeWeight { get; set; } = 2.0; // وزن جریمه مازاد ساعات موظفی

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
        public double CoolingRate { get; set; } = 0.95;
        public int MaxIterations { get; set; } = 10000;
        public int MaxIterationsWithoutImprovement { get; set; } = 1000;
        public int MaxNeighborsPerIteration { get; set; } = 10;
        public double PenaltyWeight { get; set; } = 1000.0; // وزن جریمه برای نقض محدودیت‌ها
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
