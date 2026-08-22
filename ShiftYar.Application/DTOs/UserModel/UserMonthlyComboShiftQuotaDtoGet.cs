namespace ShiftYar.Application.DTOs.UserModel
{
    public class UserMonthlyComboShiftQuotaDtoGet
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string? UserName { get; set; }
        public int? DepartmentId { get; set; }
        public int PersianYear { get; set; }
        public int PersianMonth { get; set; }
        public int? MorningEveningShiftCount { get; set; }
        public bool? MorningEveningFallbackParticipation { get; set; }
        public int? MorningEveningHolidayCount { get; set; }
        public bool? MorningEveningHolidayFallback { get; set; }
        public int? MorningNightShiftCount { get; set; }
        public bool? MorningNightFallbackParticipation { get; set; }
        public int? MorningNightHolidayCount { get; set; }
        public bool? MorningNightHolidayFallback { get; set; }
    }
}
