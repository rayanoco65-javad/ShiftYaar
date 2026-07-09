using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.DTOs.ShiftModel.ShiftSchedulingModel
{
    /// <summary>
    /// نتیجه بهینه‌سازی شیفت‌بندی
    /// </summary>
    public class ShiftSchedulingResultDto // خروجی نهایی زمان‌بندی
    {
        public List<ShiftAssignmentDto> Assignments { get; set; } = new List<ShiftAssignmentDto>(); // لیست انتساب‌ها
        public double FinalScore { get; set; } // امتیاز نهایی (هرچه کمتر بهتر)
        public int TotalIterations { get; set; } // تعداد تکرارهای الگوریتم (در SA)
        public TimeSpan ExecutionTime { get; set; } // زمان اجرای الگوریتم
        public List<string> Violations { get; set; } = new List<string>(); // نقض‌های رخ‌داده
        public ShiftSchedulingStatisticsDto Statistics { get; set; } = new ShiftSchedulingStatisticsDto(); // آمار جانبی
        public SchedulingAlgorithm AlgorithmUsed { get; set; } // الگوریتم استفاده‌شده
        public string AlgorithmStatus { get; set; } = ""; // وضعیت الگوریتم (Optimal/Feasible/...)
        public HybridResultDto? HybridResult { get; set; } // جزئیات Hybrid در صورت استفاده
    }

    /// <summary>
    /// انتساب شیفت
    /// </summary>
    public class ShiftAssignmentDto // یک انتساب شیفت به کاربر
    {
        public int UserId { get; set; } // شناسه کاربر
        public string UserName { get; set; } = ""; // نام کاربر
        public int ShiftId { get; set; } // شناسه شیفت
        public ShiftLabel ShiftLabel { get; set; } // نوع شیفت (صبح/عصر/شب)
        public DateTime Date { get; set; } // تاریخ انتساب
        public bool IsOnCall { get; set; } // آیا آماده‌باش است
        public int SpecialtyId { get; set; } // تخصص کاربر
        public string SpecialtyName { get; set; } = ""; // نام تخصص
    }

    /// <summary>
    /// آمارهای شیفت‌بندی
    /// </summary>
    public class ShiftSchedulingStatisticsDto // آمار توصیفی برنامه شیفت
    {
        public int TotalShifts { get; set; } // تعداد کل انتساب‌ها
        public int TotalUsers { get; set; } // تعداد کاربران
        public int SatisfiedConstraints { get; set; } // تعداد قیود برآورده‌شده
        public int ViolatedConstraints { get; set; } // تعداد قیود نقض‌شده
        public double AverageShiftsPerUser { get; set; } // میانگین شیفت به ازای هر کاربر
        public Dictionary<ShiftLabel, int> ShiftsByType { get; set; } = new Dictionary<ShiftLabel, int>(); // توزیع برحسب نوع شیفت
        public Dictionary<int, int> ShiftsByUser { get; set; } = new Dictionary<int, int>(); // تعداد شیفت هر کاربر
        public Dictionary<int, double> WorkedHoursByUser { get; set; } = new Dictionary<int, double>(); // ساعات واقعی کار شده
        public Dictionary<int, double> ProductivityRequiredHoursByUser { get; set; } = new Dictionary<int, double>(); // سقف موظفی هر کاربر
        public Dictionary<int, double> ProductivityOvertimeByUser { get; set; } = new Dictionary<int, double>(); // میزان مازاد ساعات
        public double ProductivityComplianceRate { get; set; } // درصد رعایت سقف بهره‌وری
        public double SoftConstraintViolationRate { get; set; } // نسبت نقض قیود نرم به کاربران
        public double TotalScheduledHours { get; set; } // مجموع ساعات برنامه‌ریزی‌شده
    }

    /// <summary>
    /// نتیجه الگوریتم ترکیبی
    /// </summary>
    public class HybridResultDto // جزئیات اجرای Hybrid
    {
        public string StrategyUsed { get; set; } = ""; // استراتژی به‌کاررفته
        public TimeSpan TotalExecutionTime { get; set; } // زمان کل اجرا
        public TimeSpan Phase1ExecutionTime { get; set; } // زمان فاز اول
        public TimeSpan Phase2ExecutionTime { get; set; } // زمان فاز دوم
        public TimeSpan ParallelExecutionTime { get; set; } // زمان اجرای موازی
        public TimeSpan IterativeExecutionTime { get; set; } // زمان اجرای تکراری
        public TimeSpan FallbackExecutionTime { get; set; } // زمان مسیر جایگزین
        public string Phase1Status { get; set; } = ""; // وضعیت فاز اول
        public string Phase2Status { get; set; } = ""; // وضعیت فاز دوم
        public bool FallbackUsed { get; set; } // آیا fallback استفاده شد
        public int TotalIterations { get; set; } // تعداد تکرارها
        public List<int> Improvements { get; set; } = new List<int>(); // نقاط بهبود
        public double ProblemComplexity { get; set; } // پیچیدگی مسأله
        public List<string> Errors { get; set; } = new List<string>(); // خطاها

        // نتایج جزئی
        public OrToolsResultDto? OrToolsResult { get; set; } // خلاصه خروجی OR-Tools
        public SimulatedAnnealingResultDto? SimulatedAnnealingResult { get; set; } // خلاصه خروجی SA
    }

    /// <summary>
    /// نتیجه OR-Tools
    /// </summary>
    public class OrToolsResultDto // خلاصه آمار OR-Tools
    {
        public string Status { get; set; } = ""; // وضعیت حل (Optimal/Feasible/...)
        public TimeSpan SolveTime { get; set; } // زمان حل
        public int NumVariables { get; set; } // تعداد متغیرها
        public int NumConstraints { get; set; } // تعداد قیود
        public double ObjectiveValue { get; set; } // مقدار تابع هدف
        public double BestBound { get; set; } // بهترین کران
        public int NumBranches { get; set; } // تعداد شاخه‌ها
        public int NumConflicts { get; set; } // تعداد تضادها
        public int NumRestarts { get; set; } // تعداد ری‌استارت‌ها
        public List<string> SolverLogs { get; set; } = new List<string>(); // لاگ‌های حل‌کننده
    }

    /// <summary>
    /// نتیجه Simulated Annealing
    /// </summary>
    public class SimulatedAnnealingResultDto // خلاصه آمار SA
    {
        public int TotalIterations { get; set; } // تعداد کل تکرارها
        public int AcceptedMoves { get; set; } // حرکت‌های پذیرفته‌شده
        public int RejectedMoves { get; set; } // حرکت‌های ردشده
        public double BestScore { get; set; } // بهترین امتیاز
        public double CurrentScore { get; set; } // امتیاز فعلی
        public double CurrentTemperature { get; set; } // دمای فعلی
        public TimeSpan ExecutionTime { get; set; } // زمان اجرا
        public List<double> ScoreHistory { get; set; } = new List<double>(); // تاریخچه امتیاز
        public List<double> TemperatureHistory { get; set; } = new List<double>(); // تاریخچه دما
    }
}
