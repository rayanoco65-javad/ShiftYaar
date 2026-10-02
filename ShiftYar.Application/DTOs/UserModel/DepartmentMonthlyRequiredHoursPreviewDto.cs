using System.Collections.Generic;

namespace ShiftYar.Application.DTOs.UserModel
{
    /// <summary>
    /// مدل پیش‌نمایش متمرکز ساعات موظفی پرسنل یک دپارتمان قبل از شیفت‌بندی
    /// </summary>
    public class DepartmentMonthlyRequiredHoursPreviewDto
    {
        public int DepartmentId { get; set; }
        public string? DepartmentName { get; set; }
        public int PersianYear { get; set; }
        public int PersianMonth { get; set; }

        /// <summary>تعداد کل پرسنل فعال این بخش در ماه مورد نظر</summary>
        public int TotalActiveUsers { get; set; }

        /// <summary>تعداد پرسنلی که ساعات موظفی آن‌ها تأیید و ذخیره شده است</summary>
        public int TotalConfirmedUsers { get; set; }

        /// <summary>آیا ساعات موظفی تمام پرسنل فعال تأیید شده و شیفت‌بندی مجاز است؟</summary>
        public bool AllUsersConfirmed { get; set; }

        /// <summary>تعداد کل روزهای تقویمی ماه</summary>
        public int TotalDaysInMonth { get; set; }

        /// <summary>تعداد روزهای کاری غیرتعطیل ماه</summary>
        public int WorkingDaysCount { get; set; }

        /// <summary>لیست ساعات موظفی پرسنل (با مقادیر ذخیره‌شده یا پیشنهادی سیستم)</summary>
        public List<UserMonthlyRequiredHourDtoGet> Users { get; set; } = new();
    }
}
