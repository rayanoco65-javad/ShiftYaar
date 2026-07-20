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
        /// بعضی رکوردهای قدیمی، اجزای تاریخ شمسی را داخل DateTime میلادی ذخیره کرده‌اند (مثلاً سال ۱۳۸۰).
        /// </summary>
        public static DateTime NormalizeEmploymentDate(DateTime stored)
        {
            if (stored.Year < 1200 || stored.Year > 1500)
            {
                return stored;
            }

            try
            {
                var persian = new PersianCalendar();
                var day = Math.Clamp(stored.Day, 1, persian.GetDaysInMonth(stored.Year, stored.Month));
                return persian.ToDateTime(stored.Year, stored.Month, day, 0, 0, 0, 0);
            }
            catch
            {
                return stored;
            }
        }

        /// <summary>
        /// Convenience helper to build employment info straight from existing User aggregate to avoid data duplication.
        /// </summary>
        public static StaffEmploymentInfo FromUser(
            User user,
            bool hasUncommonRotatingShifts = false,
            int? yearsOfServiceOverride = null)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            return new StaffEmploymentInfo
            {
                StaffId = user.Id ?? 0,
                StaffFullName = user.FullName,
                DateOfEmployment = user.DateOfEmployment,
                HardshipPercent = user.HardshipPercent ?? 0m,
                HasUncommonRotatingShifts = hasUncommonRotatingShifts,
                YearsOfServiceOverride = yearsOfServiceOverride
            };
        }
    }
}
