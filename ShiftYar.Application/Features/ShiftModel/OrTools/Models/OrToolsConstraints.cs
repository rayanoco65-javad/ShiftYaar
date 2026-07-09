using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;
using static ShiftYar.Domain.Enums.UserModel.UserEnums;

namespace ShiftYar.Application.Features.ShiftModel.OrTools.Models
{
    /// <summary>
    /// محدودیت‌های شیفت‌بندی برای OR-Tools CP-SAT
    /// </summary>
    public class OrToolsConstraints // مدل محدودیت‌ها برای OR-Tools
    {
        public int DepartmentId { get; set; } // شناسه دپارتمان هدف
        public DateTime StartDate { get; set; } // تاریخ شروع بازه
        public DateTime EndDate { get; set; } // تاریخ پایان بازه
        public List<OrToolsUserConstraint> UserConstraints { get; set; } = new List<OrToolsUserConstraint>(); // محدودیت‌های کاربران
        public List<OrToolsShiftRequirement> ShiftRequirements { get; set; } = new List<OrToolsShiftRequirement>(); // نیازمندی‌های شیفت‌ها
        public OrToolsGlobalConstraints GlobalConstraints { get; set; } = new OrToolsGlobalConstraints(); // قیود سراسری
        public OrToolsHardRules HardRules { get; set; } = OrToolsHardRules.CreateDefault(); // قوانین قطعی
        public OrToolsSoftWeights SoftWeights { get; set; } = OrToolsSoftWeights.CreateDefault(); // وزن قیود نرم

        // متغیرهای کمکی برای OR-Tools
        public Dictionary<string, int> UserIndexMap { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> ShiftIndexMap { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> DateIndexMap { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> SpecialtyIndexMap { get; set; } = new Dictionary<string, int>();

        public int NumUsers => UserConstraints.Count; // تعداد کاربران
        public int NumShifts => ShiftRequirements.Count; // تعداد شیفت‌ها
        public int NumDays => (EndDate - StartDate).Days + 1; // تعداد روزهای بازه
        public int NumSpecialties => UserConstraints.Select(u => u.SpecialtyId).Distinct().Count(); // تعداد تخصص‌ها
    }

    /// <summary>
    /// محدودیت‌های کاربر برای OR-Tools
    /// </summary>
    public class OrToolsUserConstraint // محدودیت‌های هر کاربر
    {
        public int UserId { get; set; } // شناسه کاربر
        public int UserIndex { get; set; } // ایندکس در آرایه OR-Tools
        public string UserName { get; set; } = string.Empty; // نام کاربر
        public UserGender Gender { get; set; } // جنسیت
        public int GenderIndex { get; set; } // 0: Male, 1: Female
        public int SpecialtyId { get; set; } // شناسه تخصص
        public int SpecialtyIndex { get; set; } // ایندکس تخصص
        public string SpecialtyName { get; set; } = string.Empty; // نام تخصص

        // محدودیت‌های سخت
        public List<int> UnavailableDateIndices { get; set; } = new List<int>(); // اندیس روزهای عدم حضور
        public List<ShiftLabel> PreferredShifts { get; set; } = new List<ShiftLabel>(); // شیفت‌های ترجیحی
        public List<ShiftLabel> UnwantedShifts { get; set; } = new List<ShiftLabel>(); // شیفت‌های ناخواسته
        public int MaxConsecutiveShifts { get; set; } = 3; // سقف شیفت متوالی
        public int MinRestDaysBetweenShifts { get; set; } = 1; // حداقل روز استراحت
        public int MaxShiftsPerWeek { get; set; } = 5; // سقف شیفت هفتگی
        public int MaxNightShiftsPerMonth { get; set; } = 8; // سقف شیفت شب ماهانه
        public bool CanBeShiftManager { get; set; } // توانایی مدیر شیفت بودن
        public ShiftTypes ShiftType { get; set; } // نوع شیفت
        public ShiftSubTypes ShiftSubType { get; set; } // زیرنوع شیفت
        public TwoShiftRotationPattern? TwoShiftRotationPattern { get; set; } // الگوی چرخش دوشیفته
        public double? ProductivityRequiredHours { get; set; } // سقف ساعات موظفی بهره‌وری

        // متغیرهای OR-Tools
        public Dictionary<string, int> AssignmentVariables { get; set; } = new Dictionary<string, int>(); // نقشه متغیرهای انتساب
        public Dictionary<string, int> OnCallVariables { get; set; } = new Dictionary<string, int>(); // نقشه متغیرهای آماده‌باش
    }

    /// <summary>
    /// نیازمندی‌های شیفت برای OR-Tools
    /// </summary>
    public class OrToolsShiftRequirement // نیازمندی‌های هر شیفت
    {
        public int ShiftId { get; set; } // شناسه شیفت
        public int ShiftIndex { get; set; } // ایندکس شیفت
        public ShiftLabel ShiftLabel { get; set; } // نوع شیفت (صبح/عصر/شب)
        public int DepartmentId { get; set; } // دپارتمان مالک شیفت
        public TimeSpan StartTime { get; set; } // ساعت شروع
        public TimeSpan EndTime { get; set; } // ساعت پایان
        public int DurationMinutes { get; set; } // مدت شیفت به دقیقه
        public List<OrToolsSpecialtyRequirement> SpecialtyRequirements { get; set; } = new List<OrToolsSpecialtyRequirement>(); // نیازمندی‌های تخصصی

        // متغیرهای OR-Tools
        public Dictionary<string, int> AssignmentVariables { get; set; } = new Dictionary<string, int>(); // متغیرهای انتساب شیفت
        public Dictionary<string, int> OnCallVariables { get; set; } = new Dictionary<string, int>(); // متغیرهای آماده‌باش شیفت
    }

    /// <summary>
    /// نیازمندی تخصص در شیفت برای OR-Tools
    /// </summary>
    public class OrToolsSpecialtyRequirement // نیازمندی یک تخصص در شیفت
    {
        public int SpecialtyId { get; set; } // شناسه تخصص
        public int SpecialtyIndex { get; set; } // ایندکس تخصص
        public string SpecialtyName { get; set; } = string.Empty; // نام تخصص
        public int RequiredMaleCount { get; set; } // تعداد موردنیاز مرد
        public int RequiredFemaleCount { get; set; } // تعداد موردنیاز زن
        public int RequiredTotalCount { get; set; } // تعداد کل موردنیاز
        public int OnCallMaleCount { get; set; } // تعداد آماده‌باش مرد
        public int OnCallFemaleCount { get; set; } // تعداد آماده‌باش زن
        public int OnCallTotalCount { get; set; } // تعداد آماده‌باش کل
    }

    /// <summary>
    /// محدودیت‌های سراسری برای OR-Tools
    /// </summary>
    public class OrToolsGlobalConstraints // قیود سراسری مسأله
    {
        public bool AllowConsecutiveNightShifts { get; set; } = false; // اجازه شب‌های متوالی
        public int MaxConsecutiveNightShifts { get; set; } = 2; // سقف شب‌های متوالی
        public bool RequireGenderBalance { get; set; } = true; // الزام تعادل جنسیتی
        public double MinGenderBalanceRatio { get; set; } = 0.3; // حداقل نسبت هر جنسیت
        public bool PreferSpecialtyMatch { get; set; } = true; // ترجیح تطابق تخصص
        public int MaxShiftsPerDay { get; set; } = 1; // حداکثر شیفت روزانه هر نفر
        public bool AllowWeekendShifts { get; set; } = true; // مجاز بودن آخر هفته
        public bool RequireShiftManager { get; set; } = true; // نیاز به مدیر شیفت
    }

    /// <summary>
    /// قوانین سخت برای OR-Tools
    /// </summary>
    public class OrToolsHardRules // قوانین قطعی
    {
        public bool ForbidDuplicateDailyAssignments { get; set; } = true; // یک شیفت در روز
        public bool EnforceMaxShiftsPerDay { get; set; } = true; // اعمال سقف روزانه
        public bool EnforceMinRestDays { get; set; } = true; // اعمال استراحت
        public bool EnforceMaxConsecutiveShifts { get; set; } = true; // اعمال سقف متوالی
        public bool EnforceWeeklyMaxShifts { get; set; } = false; // اعمال سقف هفتگی
        public bool EnforceNightShiftMonthlyCap { get; set; } = false; // اعمال سقف شب ماهانه
        public bool EnforceSpecialtyCapacity { get; set; } = true; // ظرفیت تخصص
        public bool EnforceProductivityHours { get; set; } = true; // اعمال سقف ساعات بهره‌وری

        public static OrToolsHardRules CreateDefault()
        {
            return new OrToolsHardRules();
        }
    }

    /// <summary>
    /// وزن قوانین نرم برای OR-Tools
    /// </summary>
    public class OrToolsSoftWeights // وزن قیود نرم
    {
        public double GenderBalanceWeight { get; set; } = 1.0; // وزن تعادل جنسیتی
        public double SpecialtyPreferenceWeight { get; set; } = 1.0; // وزن ترجیح تخصص
        public double UserUnwantedShiftWeight { get; set; } = 1.0; // وزن شیفت ناخواسته
        public double UserPreferredShiftWeight { get; set; } = 1.0; // وزن شیفت ترجیحی
        public double WeeklyMaxWeight { get; set; } = 1.0; // وزن سقف هفتگی
        public double MonthlyNightCapWeight { get; set; } = 1.0; // وزن سقف شب ماهانه

        public static OrToolsSoftWeights CreateDefault()
        {
            return new OrToolsSoftWeights();
        }
    }

    /// <summary>
    /// کلید متغیر برای OR-Tools
    /// </summary>
    public static class OrToolsVariableKeys
    {
        public static string GetAssignmentKey(int userIndex, int shiftIndex, int dateIndex)
        {
            return $"assign_{userIndex}_{shiftIndex}_{dateIndex}";
        }

        public static string GetOnCallKey(int userIndex, int shiftIndex, int dateIndex)
        {
            return $"oncall_{userIndex}_{shiftIndex}_{dateIndex}";
        }

        public static string GetGenderBalanceKey(int shiftIndex, int dateIndex, int genderIndex)
        {
            return $"gender_{shiftIndex}_{dateIndex}_{genderIndex}";
        }

        public static string GetSpecialtyBalanceKey(int shiftIndex, int dateIndex, int specialtyIndex)
        {
            return $"specialty_{shiftIndex}_{dateIndex}_{specialtyIndex}";
        }

        public static string GetConsecutiveShiftsKey(int userIndex, int dateIndex, int consecutiveCount)
        {
            return $"consec_{userIndex}_{dateIndex}_{consecutiveCount}";
        }

        public static string GetWeeklyShiftsKey(int userIndex, int weekIndex)
        {
            return $"weekly_{userIndex}_{weekIndex}";
        }

        public static string GetMonthlyNightShiftsKey(int userIndex, int monthIndex)
        {
            return $"monthly_night_{userIndex}_{monthIndex}";
        }
    }
}
