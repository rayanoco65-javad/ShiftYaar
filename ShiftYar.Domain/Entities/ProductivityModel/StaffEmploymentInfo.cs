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
        public decimal? HardshipPercentage => HardshipPercent;
        public decimal? HardshipScore { get; init; }
        public decimal? HardshipPoints => HardshipScore;
        public string? Position { get; init; }
        public string? JobTitle { get; init; }
        public string? Role { get; init; }
        public bool? IsSupervisor { get; init; }
        public bool? IsHeadNurse { get; init; }
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
        /// Resolves fractional years of service (e.g. 4 years 1 month = 4.0833 years) based on start date.
        /// </summary>
        public decimal ResolveYearsOfServiceDecimal(DateTime referenceDate)
        {
            if (YearsOfServiceOverride.HasValue && YearsOfServiceOverride.Value >= 0)
            {
                return YearsOfServiceOverride.Value;
            }

            if (!DateOfEmployment.HasValue)
            {
                return 0m;
            }

            var employment = NormalizeEmploymentDate(DateOfEmployment.Value);
            var totalMonths = (referenceDate.Year - employment.Year) * 12
                              + (referenceDate.Month - employment.Month);

            if (totalMonths < 0)
            {
                return 0m;
            }

            return Math.Round(totalMonths / 12.0m, 4);
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

            ShiftPatternType pattern;
            if (shiftPattern.HasValue)
            {
                pattern = shiftPattern.Value;
            }
            else if (user.ShiftType == Domain.Enums.ShiftModel.ShiftEnums.ShiftTypes.RotatingShift)
            {
                pattern = user.ShiftSubType == Domain.Enums.ShiftModel.ShiftEnums.ShiftSubTypes.TwoShifts
                    ? ShiftPatternType.TwoShiftRotating
                    : ShiftPatternType.ThreeShiftRotating;
            }
            else if (user.ShiftType == Domain.Enums.ShiftModel.ShiftEnums.ShiftTypes.FixedShift)
            {
                var isNight = user.AllowedShiftPermissions.HasValue &&
                              user.AllowedShiftPermissions.Value.HasFlag(Domain.Enums.ShiftModel.ShiftEnums.UserShiftPermission.Night);
                pattern = isNight ? ShiftPatternType.FixedNight : ShiftPatternType.FixedDay;
            }
            else
            {
                pattern = hasUncommonRotatingShifts ? ShiftPatternType.ThreeShiftRotating : ShiftPatternType.FixedDay;
            }

            var isRotating = pattern == ShiftPatternType.ThreeShiftRotating || pattern == ShiftPatternType.TwoShiftRotating || pattern == ShiftPatternType.FixedNight;

            return new StaffEmploymentInfo
            {
                StaffId = user.Id ?? 0,
                StaffFullName = user.FullName,
                DateOfEmployment = NormalizeEmploymentDate(user.DateOfEmployment),
                HardshipPercent = user.HardshipPercent ?? 0m,
                HardshipScore = user.HardshipScore,
                Position = user.Position,
                JobTitle = user.JobTitle,
                IsSupervisor = user.IsSupervisor,
                IsHeadNurse = user.IsHeadNurse,
                HasUncommonRotatingShifts = hasUncommonRotatingShifts || isRotating,
                ShiftPattern = pattern,
                YearsOfServiceOverride = yearsOfServiceOverride
            };
        }
    }
}
