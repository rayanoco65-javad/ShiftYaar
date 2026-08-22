using ShiftYar.Domain.Entities.BaseModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShiftYar.Domain.Entities.UserModel
{
    /// <summary>
    /// سهمیه الگوی صبح/عصر و صبح/شب برای پرسنل گردشی (دو‌نوبته یا سه‌نوبته؛ نه شیفت ثابت).
    /// </summary>
    public class UserMonthlyComboShiftQuota : BaseEntity
    {
        [Key]
        public int? Id { get; set; }

        [ForeignKey(nameof(User))]
        public int UserId { get; set; }
        public User? User { get; set; }

        public int PersianYear { get; set; }
        public int PersianMonth { get; set; }

        /// <summary>حداقل/هدف تعداد کل انتساب صبح+عصر در ماه. null = بدون کف اجباری.</summary>
        public int? MorningEveningShiftCount { get; set; }

        /// <summary>null = مشارکت در مازاد (پیش‌فرض)؛ false = فقط سهمیه قطعی.</summary>
        public bool? MorningEveningFallbackParticipation { get; set; }

        /// <summary>حداقل انتساب صبح+عصر در روزهای تعطیل.</summary>
        public int? MorningEveningHolidayCount { get; set; }

        /// <summary>null = مشارکت در مازاد تعطیل (پیش‌فرض)؛ false = بدون مازاد.</summary>
        public bool? MorningEveningHolidayFallback { get; set; }

        /// <summary>حداقل/هدف تعداد کل انتساب صبح+شب در ماه. null = بدون کف اجباری.</summary>
        public int? MorningNightShiftCount { get; set; }

        /// <summary>null = مشارکت در مازاد (پیش‌فرض)؛ false = فقط سهمیه قطعی.</summary>
        public bool? MorningNightFallbackParticipation { get; set; }

        /// <summary>حداقل انتساب صبح+شب در روزهای تعطیل.</summary>
        public int? MorningNightHolidayCount { get; set; }

        /// <summary>null = مشارکت در مازاد تعطیل (پیش‌فرض)؛ false = بدون مازاد.</summary>
        public bool? MorningNightHolidayFallback { get; set; }
    }
}
