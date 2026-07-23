using System.ComponentModel.DataAnnotations;

namespace ShiftYar.Application.DTOs.ShiftModel.ShiftSchedulingModel
{
    /// <summary>
    /// درخواست حذف شیفت‌بندی ذخیره‌شده برای یک ماه شمسی.
    /// </summary>
    public class DeleteMonthlyScheduleRequestDto
    {
        [Range(1, int.MaxValue, ErrorMessage = "شناسه دپارتمان باید مقدار مثبت داشته باشد.")]
        public int DepartmentId { get; set; }

        [Range(1300, 1500, ErrorMessage = "سال شمسی نامعتبر است.")]
        public int PersianYear { get; set; }

        [Range(1, 12, ErrorMessage = "ماه شمسی باید بین ۱ تا ۱۲ باشد.")]
        public int PersianMonth { get; set; }
    }
}
