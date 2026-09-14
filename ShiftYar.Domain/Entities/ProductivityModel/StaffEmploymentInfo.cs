using System;
using System.Globalization;
using ShiftYar.Domain.Entities.UserModel;

namespace ShiftYar.Domain.Entities.ProductivityModel
{
    /// <summary>
    /// Captures staff-specific attributes required to apply productivity rules.
    /// </summary>
    public class StaffEmploymentInfo
    {
        public int StaffId { get; init; }
        public string? StaffFullName { get; init; }
        public DateTime? DateOfEmployment { get; init; }
        public int? YearsOfServiceOverride { get; init; }
        public decimal HardshipPercent { get; init; }
        public bool HasUncommonRotatingShifts { get; init; }
        public ShiftPatternType ShiftPattern { get; init; } = ShiftPatternType.FixedDay;

        /// <summary>
        /// Resolves years of service based on either an explicit override or the employment start date.
        /// </summary>
        /// <param name="referenceDate">Typically the first day of the month being calculated.</param>
        public int ResolveYearsOfService(DateTime referenceDate)
        {
            if (YearsOfServiceOverride.HasValue && YearsOfServiceOverride.Value >= 0)
            {
                return YearsOfServiceOverride.Value;
            }

            if (!DateOfEmployment.HasValue)
            {
                return 0;
            }

            var employment = NormalizeEmploymentDate(DateOfEmployment.Value);
            var totalMonths = (referenceDate.Year - employment.Year) * 12
                              + (referenceDate.Month - employment.Month);

            if (totalMonths < 0)
            {
                return 0;
            }

            return (int)Math.Floor(totalMonths / 12m);
        }

        /// <summary>
        /// تشخیص اینکه آیا DateTime به‌اشتباه اجزای شمسی را به‌صورت سال/ماه/روز میلادی نگه داشته است.
        /// </summary>
        public static bool LooksLikeMisstoredPersianComponents(DateTime stored)
            => stored.Year >= 1200 && stored.Year <= 1500;

        /// <summary>
        /// بعضی رکوردهای قدیمی، اجزای تاریخ شمسی را داخل DateTime میلادی ذخیره کرده‌اند (مثلاً ۱۳۸۰/۰۱/۰۴ → 1380-01-04).
        /// این متد آن‌ها را به میلادی واقعی تبدیل می‌کند؛ تاریخ‌های صحیح دست‌نخورده می‌مانند.
        /// </summary>
        public static DateTime NormalizeEmploymentDate(DateTime stored)
        {
            if (!LooksLikeMisstoredPersianComponents(stored))
            {
                return DateTime.SpecifyKind(stored.Date, DateTimeKind.Unspecified);
            }

            try
            {
                var persian = new PersianCalendar();
                var month = Math.Clamp(stored.Month, 1, 12);
                var day = Math.Clamp(stored.Day, 1, persian.GetDaysInMonth(stored.Year, month));
                var result = persian.ToDateTime(stored.Year, month, day, 0, 0, 0, 0);
                return DateTime.SpecifyKind(result.Date, DateTimeKind.Unspecified);
            }
            catch
            {
                return DateTime.SpecifyKind(stored.Date, DateTimeKind.Unspecified);
            }
        }

        public static DateTime? NormalizeEmploymentDate(DateTime? stored)
            => stored.HasValue ? NormalizeEmploymentDate(stored.Value) : null;

        /// <summary>
        /// Convenience helper to build employment info straight from existing User aggregate to avoid data duplication.
        /// </summary>
        public static StaffEmploymentInfo FromUser(
            User user,
            ShiftPatternType? shiftPattern = null,
            bool hasUncommonRotatingShifts = false,
            int? yearsOfServiceOverride = null)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            var pattern = shiftPattern ?? (hasUncommonRotatingShifts ? ShiftPatternType.ThreeShiftRotating : ShiftPatternType.FixedDay);

            return new StaffEmploymentInfo
            {
                StaffId = user.Id ?? 0,
                StaffFullName = user.FullName,
                DateOfEmployment = NormalizeEmploymentDate(user.DateOfEmployment),
                HardshipPercent = user.HardshipPercent ?? 0m,
                HasUncommonRotatingShifts = hasUncommonRotatingShifts || pattern == ShiftPatternType.ThreeShiftRotating || pattern == ShiftPatternType.TwoShiftRotating,
                ShiftPattern = pattern,
                YearsOfServiceOverride = yearsOfServiceOverride
            };
        }
    }
}
