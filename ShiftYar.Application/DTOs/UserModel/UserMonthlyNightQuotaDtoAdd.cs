using System.ComponentModel.DataAnnotations;

namespace ShiftYar.Application.DTOs.UserModel
{
    public class UserMonthlyNightQuotaDtoAdd
    {
        [Required]
        public int UserId { get; set; }

        [Required]
        [Range(1300, 1500, ErrorMessage = "سال شمسی نامعتبر است.")]
        public int PersianYear { get; set; }

        [Required]
        [Range(1, 12, ErrorMessage = "ماه شمسی باید بین ۱ تا ۱۲ باشد.")]
        public int PersianMonth { get; set; }

        /// <summary>حداقل تعداد شیفت شب؛ خالی = بدون حداقل اجباری</summary>
        [Range(0, 31)]
        public int? ExactNightShiftCount { get; set; }

        /// <summary>حداقل تعداد شب تعطیل/آخرهفته؛ باید ≤ تعداد کل شب باشد</summary>
        [Range(0, 31)]
        public int? ExactHolidayWeekendNightShiftCount { get; set; }

        /// <summary>null = مشارکت در مازاد (پیش‌فرض)؛ false = بدون مازاد؛ true = مشارکت صریح</summary>
        public bool? NightFallbackParticipation { get; set; }

        /// <summary>null = مشارکت در مازاد شب تعطیل/آخرهفته (پیش‌فرض)؛ false = بدون مازاد</summary>
        public bool? HolidayWeekendNightFallbackParticipation { get; set; }
    }
}
