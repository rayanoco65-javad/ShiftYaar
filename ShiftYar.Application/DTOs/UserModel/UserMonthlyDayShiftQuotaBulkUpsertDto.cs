using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShiftYar.Application.DTOs.UserModel
{
    public class UserMonthlyDayShiftQuotaBulkUpsertDto
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
        public List<UserMonthlyDayShiftQuotaItemDto> Items { get; set; } = new();
    }

    public class UserMonthlyDayShiftQuotaItemDto
    {
        [Required]
        public int UserId { get; set; }

        [Range(0, 31)]
        public int? ExactMorningShiftCount { get; set; }

        public bool? MorningFallbackParticipation { get; set; }

        [Range(0, 31)]
        public int? ExactHolidayMorningShiftCount { get; set; }

        public bool? MorningHolidayFallbackParticipation { get; set; }

        [Range(0, 31)]
        public int? ExactEveningShiftCount { get; set; }

        public bool? EveningFallbackParticipation { get; set; }

        [Range(0, 31)]
        public int? ExactHolidayEveningShiftCount { get; set; }

        public bool? EveningHolidayFallbackParticipation { get; set; }

        public bool? IsWeeklyAlternatingActive { get; set; }

        public ShiftYar.Domain.Enums.ShiftModel.ShiftEnums.ShiftLabel? FirstWeekShiftLabel { get; set; }
    }
}
