using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShiftYar.Application.DTOs.UserModel
{
    /// <summary>
    /// تنظیم یک‌جای سهمیه شب همه کاربران یک دپارتمان برای یک ماه شمسی.
    /// </summary>
    public class UserMonthlyNightQuotaBulkUpsertDto
    {
        [Required]
        public int DepartmentId { get; set; }

        [Required]
        [Range(1300, 1500)]
        public int PersianYear { get; set; }

        [Required]
        [Range(1, 12)]
        public int PersianMonth { get; set; }

        [Required]
        [MinLength(1)]
        public List<UserMonthlyNightQuotaItemDto> Items { get; set; } = new();
    }

    public class UserMonthlyNightQuotaItemDto
    {
        [Required]
        public int UserId { get; set; }

        [Range(0, 31)]
        public int? ExactNightShiftCount { get; set; }

        [Range(0, 31)]
        public int? ExactHolidayWeekendNightShiftCount { get; set; }

        public bool? NightFallbackParticipation { get; set; }

        public bool? HolidayWeekendNightFallbackParticipation { get; set; }
    }
}
