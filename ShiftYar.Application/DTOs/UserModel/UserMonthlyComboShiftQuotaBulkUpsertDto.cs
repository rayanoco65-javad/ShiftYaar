using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShiftYar.Application.DTOs.UserModel
{
    public class UserMonthlyComboShiftQuotaBulkUpsertDto
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
        public List<UserMonthlyComboShiftQuotaItemDto> Items { get; set; } = new();
    }

    public class UserMonthlyComboShiftQuotaItemDto
    {
        [Required]
        public int UserId { get; set; }

        [Range(0, 62)]
        public int? MorningEveningShiftCount { get; set; }

        public bool? MorningEveningFallbackParticipation { get; set; }

        [Range(0, 62)]
        public int? MorningEveningHolidayCount { get; set; }

        public bool? MorningEveningHolidayFallback { get; set; }

        [Range(0, 62)]
        public int? MorningNightShiftCount { get; set; }

        public bool? MorningNightFallbackParticipation { get; set; }

        [Range(0, 62)]
        public int? MorningNightHolidayCount { get; set; }

        public bool? MorningNightHolidayFallback { get; set; }
    }
}
