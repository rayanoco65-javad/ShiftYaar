namespace ShiftYar.Application.DTOs.UserModel
{
    public class UserMonthlyNightQuotaDtoGet
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string? UserName { get; set; }
        public int? DepartmentId { get; set; }
        public int PersianYear { get; set; }
        public int PersianMonth { get; set; }
        public int? ExactNightShiftCount { get; set; }
        public int? ExactHolidayWeekendNightShiftCount { get; set; }
    }
}
