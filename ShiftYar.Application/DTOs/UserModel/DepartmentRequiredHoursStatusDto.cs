using System.Collections.Generic;

namespace ShiftYar.Application.DTOs.UserModel
{
    /// <summary>
    /// وضعیت تأیید ساعات موظفی پرسنل یک بخش برای یک ماه شمسی مشخص
    /// </summary>
    public class DepartmentRequiredHoursStatusDto
    {
        public int DepartmentId { get; set; }
        public int PersianYear { get; set; }
        public int PersianMonth { get; set; }
        public int TotalActiveUsers { get; set; }
        public int ConfirmedUsersCount { get; set; }
        public bool IsComplete { get; set; }
        public List<string> MissingUsers { get; set; } = new();
        public string? Message { get; set; }
    }
}
