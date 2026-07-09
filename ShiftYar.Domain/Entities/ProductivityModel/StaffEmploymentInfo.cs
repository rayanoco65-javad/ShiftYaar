using System;
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
        public bool HasHardshipDuty { get; init; }
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

            var totalMonths = (referenceDate.Year - DateOfEmployment.Value.Year) * 12
                              + (referenceDate.Month - DateOfEmployment.Value.Month);

            if (totalMonths < 0)
            {
                return 0;
            }

            return (int)Math.Floor(totalMonths / 12m);
        }

        /// <summary>
        /// Convenience helper to build employment info straight from existing User aggregate to avoid data duplication.
        /// </summary>
        public static StaffEmploymentInfo FromUser(
            User user,
            bool hasHardshipDuty = false,
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
                HasHardshipDuty = hasHardshipDuty,
                HasUncommonRotatingShifts = hasUncommonRotatingShifts,
                YearsOfServiceOverride = yearsOfServiceOverride
            };
        }
    }
}

