using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models
{
    /// <summary>
    /// راه‌حل شیفت‌بندی که توسط الگوریتم Simulated Annealing بهینه‌سازی می‌شود
    /// </summary>
    public class ShiftSolution
    {
        public Dictionary<string, SaShiftAssignment> Assignments { get; set; } = new Dictionary<string, SaShiftAssignment>();
        public double Score { get; set; }
        public List<string> Violations { get; set; } = new List<string>();

        public ShiftSolution()
        {
            Score = double.MaxValue; // شروع با بدترین امتیاز
        }

        /// <summary>
        /// ایجاد کپی از راه‌حل فعلی
        /// </summary>
        public ShiftSolution Clone()
        {
            var clone = new ShiftSolution
            {
                Score = this.Score,
                Violations = new List<string>(this.Violations),
                Assignments = new Dictionary<string, SaShiftAssignment>()
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
            Assignments[key] = new SaShiftAssignment
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
        public List<SaShiftAssignment> GetUserAssignments(int userId, DateTime date)
        {
            return Assignments.Values
                .Where(a => a.UserId == userId && a.Date.Date == date.Date)
                .ToList();
        }

        /// <summary>
        /// دریافت انتساب‌های یک شیفت در تاریخ مشخص
        /// </summary>
        public List<SaShiftAssignment> GetShiftAssignments(int shiftId, DateTime date)
        {
            return Assignments.Values
                .Where(a => a.ShiftId == shiftId && a.Date.Date == date.Date)
                .ToList();
        }

        /// <summary>
        /// دریافت تمام انتساب‌های یک کاربر
        /// </summary>
        public List<SaShiftAssignment> GetUserAllAssignments(int userId)
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
    }

    /// <summary>
    /// انتساب شیفت در راه‌حل
    /// </summary>
    public class SaShiftAssignment
    {
        public int UserId { get; set; }
        public int ShiftId { get; set; }
        public DateTime Date { get; set; }
        public ShiftLabel ShiftLabel { get; set; }
        public bool IsOnCall { get; set; }

        public SaShiftAssignment Clone()
        {
            return new SaShiftAssignment
            {
                UserId = this.UserId,
                ShiftId = this.ShiftId,
                Date = this.Date,
                ShiftLabel = this.ShiftLabel,
                IsOnCall = this.IsOnCall
            };
        }
    }
}
