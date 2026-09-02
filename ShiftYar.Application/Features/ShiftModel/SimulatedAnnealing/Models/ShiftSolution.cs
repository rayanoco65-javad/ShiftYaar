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
        private Dictionary<int, List<SaShiftAssignment>> _userAssignmentsCache = null;
        public double Score { get; set; }
        public List<string> Violations { get; set; } = new List<string>();

        /// <summary>
        /// تقویم قفل‌شدهٔ بی‌قید فاز ۱ — گاردهای میانی بدون force/ON حق حذف ندارند.
        /// </summary>
        public HashSet<(int UserId, int ShiftId, DateTime Date)> LockedSkeletonAssignments { get; } = new();

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

            foreach (var locked in LockedSkeletonAssignments)
            {
                clone.LockedSkeletonAssignments.Add(locked);
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

        public bool IsLockedSkeleton(int userId, int shiftId, DateTime date) =>
            LockedSkeletonAssignments.Contains((userId, shiftId, date.Date));

        public void LockSkeletonAssignment(int userId, int shiftId, DateTime date)
        {
            LockedSkeletonAssignments.Add((userId, shiftId, date.Date));
            MarkSkeleton(userId, shiftId, date, isSkeleton: true);
        }

        public void UnlockSkeletonAssignment(int userId, int shiftId, DateTime date)
        {
            LockedSkeletonAssignments.Remove((userId, shiftId, date.Date));
            MarkSkeleton(userId, shiftId, date, isSkeleton: false);
        }

        public void ClearLockedSkeletonAssignments()
        {
            // قبل از پاک کردن set، پرچم IsSkeleton فقط برای انتساب‌های موجود در lock set پاک می‌شود.
            // انتساب‌هایی که با isSkeleton:true مستقیماً علامت‌گذاری شدند (و در lock set نیستند) دست نخورده می‌مانند.
            foreach (var assignment in Assignments.Values)
            {
                if (IsLockedSkeleton(assignment.UserId, assignment.ShiftId, assignment.Date))
                {
                    assignment.IsSkeleton = false;
                }
            }

            LockedSkeletonAssignments.Clear();
        }

        /// <summary>
        /// اضافه کردن انتساب جدید
        /// </summary>
        public void AddAssignment(int userId, int shiftId, DateTime date, ShiftLabel shiftLabel, bool isOnCall = false, bool isSkeleton = false)
        {
            var key = GetAssignmentKey(userId, shiftId, date);
            _userAssignmentsCache = null;  _shiftAssignmentsCache = null;
            Assignments[key] = new SaShiftAssignment
            {
                UserId = userId,
                ShiftId = shiftId,
                Date = date,
                ShiftLabel = shiftLabel,
                IsOnCall = isOnCall,
                IsSkeleton = isSkeleton || IsLockedSkeleton(userId, shiftId, date)
            };
        }

        /// <summary>
        /// حذف انتساب. اسکلت/قفل بدون force=true حذف نمی‌شود.
        /// </summary>
        public bool RemoveAssignment(int userId, int shiftId, DateTime date, bool force = false)
        {
            if (!force && IsLockedSkeleton(userId, shiftId, date))
            {
                return false;
            }

            var key = GetAssignmentKey(userId, shiftId, date);
            if (!force && Assignments.TryGetValue(key, out var existing) && existing.IsSkeleton)
            {
                return false;
            }

            LockedSkeletonAssignments.Remove((userId, shiftId, date.Date));
            _userAssignmentsCache = null;  _shiftAssignmentsCache = null;
            return Assignments.Remove(key);
        }

        public bool TryGetAssignment(int userId, int shiftId, DateTime date, out SaShiftAssignment assignment)
        {
            var key = GetAssignmentKey(userId, shiftId, date);
            return Assignments.TryGetValue(key, out assignment!);
        }

        public void MarkSkeleton(int userId, int shiftId, DateTime date, bool isSkeleton = true)
        {
            var key = GetAssignmentKey(userId, shiftId, date);
            if (Assignments.TryGetValue(key, out var assignment))
            {
                assignment.IsSkeleton = isSkeleton || IsLockedSkeleton(userId, shiftId, date);
            }
        }

        public void ClearAllSkeletonFlags()
        {
            foreach (var assignment in Assignments.Values)
            {
                assignment.IsSkeleton = IsLockedSkeleton(
                    assignment.UserId, assignment.ShiftId, assignment.Date);
            }
        }

        public void SyncSkeletonFlagsFromLockSet()
        {
            foreach (var assignment in Assignments.Values)
            {
                assignment.IsSkeleton = assignment.IsSkeleton || IsLockedSkeleton(
                    assignment.UserId, assignment.ShiftId, assignment.Date);
            }
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
        private Dictionary<string, List<SaShiftAssignment>> _shiftAssignmentsCache = null;
        public List<SaShiftAssignment> GetShiftAssignments(int shiftId, DateTime date)
        {
            if (_shiftAssignmentsCache == null)
            {
                _shiftAssignmentsCache = new Dictionary<string, List<SaShiftAssignment>>();
                foreach (var a in Assignments.Values)
                {
                    string key = a.ShiftId + "_" + a.Date.Date.ToString("yyyyMMdd");
                    if (!_shiftAssignmentsCache.TryGetValue(key, out var list))
                    {
                        list = new List<SaShiftAssignment>();
                        _shiftAssignmentsCache[key] = list;
                    }
                    list.Add(a);
                }
            }
            string reqKey = shiftId + "_" + date.Date.ToString("yyyyMMdd");
            if (_shiftAssignmentsCache.TryGetValue(reqKey, out var res)) return res;
            return new List<SaShiftAssignment>();
        }

        /// <summary>
        /// دریافت تمام انتساب‌های یک کاربر
        /// </summary>
                public List<SaShiftAssignment> GetUserAllAssignments(int userId)
        {
            if (_userAssignmentsCache == null)
            {
                _userAssignmentsCache = Assignments.Values
                    .GroupBy(a => a.UserId)
                    .ToDictionary(g => g.Key, g => g.OrderBy(a => a.Date).ToList());
            }
            if (_userAssignmentsCache.TryGetValue(userId, out var list))
            {
                return list;
            }
            return new List<SaShiftAssignment>();
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

        /// <summary>
        /// آینهٔ LockedSkeletonAssignments — برای سازگاری با کد قدیمی.
        /// </summary>
        public bool IsSkeleton { get; set; }

        public SaShiftAssignment Clone()
        {
            return new SaShiftAssignment
            {
                UserId = this.UserId,
                ShiftId = this.ShiftId,
                Date = this.Date,
                ShiftLabel = this.ShiftLabel,
                IsOnCall = this.IsOnCall,
                IsSkeleton = this.IsSkeleton
            };
        }
    }
}
