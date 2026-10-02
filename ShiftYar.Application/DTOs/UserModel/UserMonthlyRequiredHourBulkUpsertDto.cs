using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShiftYar.Application.DTOs.UserModel
{
    /// <summary>
    /// مدل ذخیره یا به‌روزرسانی گروهی ساعت موظفی ماهانه پرسنل یک بخش
    /// </summary>
    public class UserMonthlyRequiredHourBulkUpsertDto
    {
        [Required]
        public int DepartmentId { get; set; }

        [Required]
        [Range(1300, 1500, ErrorMessage = "سال شمسی معتبر نیست.")]
        public int PersianYear { get; set; }

        [Required]
        [Range(1, 12, ErrorMessage = "ماه شمسی باید بین ۱ تا ۱۲ باشد.")]
        public int PersianMonth { get; set; }

        [Required]
        [MinLength(1, ErrorMessage = "حداقل اطلاعات یک کاربر باید ارسال شود.")]
        public List<UserMonthlyRequiredHourItemDto> Items { get; set; } = new();
    }

    public class UserMonthlyRequiredHourItemDto
    {
        [Required]
        public int UserId { get; set; }

        [Required]
        [Range(0, 400, ErrorMessage = "ساعت موظفی باید بین ۰ تا ۴۰۰ ساعت باشد.")]
        public decimal ApprovedHours { get; set; }

        [MaxLength(500, ErrorMessage = "توضیحات نمی‌تواند بیش از ۵۰۰ کاراکتر باشد.")]
        public string? Notes { get; set; }
    }
}
