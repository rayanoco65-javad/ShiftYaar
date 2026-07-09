using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.Features.ShiftModel.OrTools.Models
{
    /// <summary>
    /// راه‌حل شیفت‌بندی که توسط الگوریتم OR-Tools CP-SAT بهینه‌سازی می‌شود
    /// </summary>
    public class OrToolsShiftSolution
    {
        public Dictionary<string, OrToolsShiftAssignment> Assignments { get; set; } = new Dictionary<string, OrToolsShiftAssignment>();
        public double ObjectiveValue { get; set; }
        public List<string> Violations { get; set; } = new List<string>();
        public OrToolsSolverStatus Status { get; set; }
        public TimeSpan SolveTime { get; set; }
        public int NumVariables { get; set; }
        public int NumConstraints { get; set; }

        public OrToolsShiftSolution()
        {
            ObjectiveValue = double.MaxValue;
            Status = OrToolsSolverStatus.Unknown;
        }

        /// <summary>
        /// ایجاد کپی از راه‌حل فعلی
        /// </summary>
        public OrToolsShiftSolution Clone()
        {
            var clone = new OrToolsShiftSolution
            {
                ObjectiveValue = this.ObjectiveValue,
                Status = this.Status,
                SolveTime = this.SolveTime,
                NumVariables = this.NumVariables,
                NumConstraints = this.NumConstraints,
                Violations = new List<string>(this.Violations),
                Assignments = new Dictionary<string, OrToolsShiftAssignment>()
            };

            foreach (var assignment in this.Assignments)
            {
                clone.Assignments[assignment.Key] = assignment.Value.Clone();
            }

            return clone;
        }

        /// <summary>
        /// محاسبه کلید یکتای انتساب
        /// </summary>
        public static string GetAssignmentKey(int userId, int shiftId, DateTime date)
        {
            return $"{userId}_{shiftId}_{date:yyyyMMdd}";
        }

        /// <summary>
        /// اضافه کردن انتساب جدید
        /// </summary>
        public void AddAssignment(int userId, int shiftId, DateTime date, ShiftLabel shiftLabel, bool isOnCall = false)
        {
            var key = GetAssignmentKey(userId, shiftId, date);
            Assignments[key] = new OrToolsShiftAssignment
            {
                UserId = userId,
                ShiftId = shiftId,
                Date = date,
                ShiftLabel = shiftLabel,
                IsOnCall = isOnCall
            };
        }

        /// <summary>
        /// حذف انتساب
        /// </summary>
        public void RemoveAssignment(int userId, int shiftId, DateTime date)
        {
            var key = GetAssignmentKey(userId, shiftId, date);
            Assignments.Remove(key);
        }

        /// <summary>
        /// دریافت انتساب‌های یک کاربر در تاریخ مشخص
        /// </summary>
        public List<OrToolsShiftAssignment> GetUserAssignments(int userId, DateTime date)
        {
            return Assignments.Values
                .Where(a => a.UserId == userId && a.Date.Date == date.Date)
                .ToList();
        }

        /// <summary>
        /// دریافت انتساب‌های یک شیفت در تاریخ مشخص
        /// </summary>
        public List<OrToolsShiftAssignment> GetShiftAssignments(int shiftId, DateTime date)
        {
            return Assignments.Values
                .Where(a => a.ShiftId == shiftId && a.Date.Date == date.Date)
                .ToList();
        }

        /// <summary>
        /// دریافت تمام انتساب‌های یک کاربر
        /// </summary>
        public List<OrToolsShiftAssignment> GetUserAllAssignments(int userId)
        {
            return Assignments.Values
                .Where(a => a.UserId == userId)
                .OrderBy(a => a.Date)
                .ToList();
        }

        /// <summary>
        /// بررسی وجود انتساب
        /// </summary>
        public bool HasAssignment(int userId, int shiftId, DateTime date)
        {
            var key = GetAssignmentKey(userId, shiftId, date);
            return Assignments.ContainsKey(key);
        }

        /// <summary>
        /// محاسبه امتیاز راه‌حل (برای مقایسه با Simulated Annealing)
        /// </summary>
        public double CalculateScore()
        {
            // تبدیل ObjectiveValue به Score (هرچه کمتر بهتر)
            return ObjectiveValue;
        }
    }

    /// <summary>
    /// انتساب شیفت در راه‌حل OR-Tools
    /// </summary>
    public class OrToolsShiftAssignment
    {
        public int UserId { get; set; }
        public int ShiftId { get; set; }
        public DateTime Date { get; set; }
        public ShiftLabel ShiftLabel { get; set; }
        public bool IsOnCall { get; set; }

        public OrToolsShiftAssignment Clone()
        {
            return new OrToolsShiftAssignment
            {
                UserId = this.UserId,
                ShiftId = this.ShiftId,
                Date = this.Date,
                ShiftLabel = this.ShiftLabel,
                IsOnCall = this.IsOnCall
            };
        }
    }

    /// <summary>
    /// وضعیت حل‌کننده OR-Tools
    /// </summary>
    public enum OrToolsSolverStatus
    {
        Unknown = 0,
        Optimal = 1,
        Feasible = 2,
        Infeasible = 3,
        Unbounded = 4,
        Abnormal = 5,
        NotSolved = 6
    }

    /// <summary>
    /// پارامترهای الگوریتم OR-Tools CP-SAT
    /// </summary>
    public class OrToolsParameters
    {
        public int MaxTimeInSeconds { get; set; } = 300; // 5 دقیقه
        public int NumSearchWorkers { get; set; } = 4;
        public bool LogSearchProgress { get; set; } = true;
        public int MaxSolutions { get; set; } = 1;
        public double RelativeGapLimit { get; set; } = 0.01; // 1%
        public bool UseLinearRelaxation { get; set; } = true;
        public bool UseCumulativeConstraint { get; set; } = true;
        public bool UseNoOverlapConstraint { get; set; } = true;

        // پارامترهای تخصصی برای شیفت‌بندی
        public double HardConstraintWeight { get; set; } = 1000.0;
        public double SoftConstraintWeight { get; set; } = 1.0;
        public double GenderBalanceWeight { get; set; } = 10.0;
        public double SpecialtyPreferenceWeight { get; set; } = 5.0;
        public double UserPreferenceWeight { get; set; } = 3.0;
    }

    /// <summary>
    /// آمارهای الگوریتم OR-Tools
    /// </summary>
    public class OrToolsStatistics
    {
        public OrToolsSolverStatus Status { get; set; }
        public TimeSpan SolveTime { get; set; }
        public int NumVariables { get; set; }
        public int NumConstraints { get; set; }
        public double ObjectiveValue { get; set; }
        public double BestBound { get; set; }
        public int NumBranches { get; set; }
        public int NumConflicts { get; set; }
        public int NumRestarts { get; set; }
        public List<string> SolverLogs { get; set; } = new List<string>();
    }
}
