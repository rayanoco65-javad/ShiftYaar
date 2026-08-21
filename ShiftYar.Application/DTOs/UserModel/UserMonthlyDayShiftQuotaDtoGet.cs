namespace ShiftYar.Application.DTOs.UserModel
{
    public class UserMonthlyDayShiftQuotaDtoGet
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string? UserName { get; set; }
        public int? DepartmentId { get; set; }
        public int PersianYear { get; set; }
        public int PersianMonth { get; set; }

        public int? ExactMorningShiftCount { get; set; }
        public bool? MorningFallbackParticipation { get; set; }
        public int? ExactHolidayMorningShiftCount { get; set; }
        public bool? MorningHolidayFallbackParticipation { get; set; }

        public int? ExactEveningShiftCount { get; set; }
        public bool? EveningFallbackParticipation { get; set; }
        public int? ExactHolidayEveningShiftCount { get; set; }
        public bool? EveningHolidayFallbackParticipation { get; set; }
    }
}
