using System.ComponentModel.DataAnnotations;

namespace ShiftYar.Application.DTOs.UserModel
{
    public class UserMonthlyDayShiftQuotaDtoAdd
    {
        [Required]
        public int UserId { get; set; }

        [Required]
        [Range(1300, 1500)]
        public int PersianYear { get; set; }

        [Required]
        [Range(1, 12)]
        public int PersianMonth { get; set; }

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
    }
}
