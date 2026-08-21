using ShiftYar.Domain.Entities.BaseModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShiftYar.Domain.Entities.UserModel
{
    /// <summary>
    /// سهمیه حداقل شیفت شب هر کاربر برای یک ماه شمسی مشخص.
    /// سوپروایزر قبل از شیفت‌بندی هر ماه این مقادیر را تنظیم می‌کند.
    /// </summary>
    public class UserMonthlyNightQuota : BaseEntity
    {
        [Key]
        public int? Id { get; set; }

        [ForeignKey(nameof(User))]
        public int UserId { get; set; }
        public User? User { get; set; }

        /// <summary>سال شمسی (مثلاً ۱۴۰۵)</summary>
        public int PersianYear { get; set; }

        /// <summary>ماه شمسی ۱ تا ۱۲</summary>
        public int PersianMonth { get; set; }

        /// <summary>حداقل تعداد شیفت شب در آن ماه. null = بدون حداقل اجباری.</summary>
        public int? ExactNightShiftCount { get; set; }

        /// <summary>حداقل تعداد شب تعطیل/آخرهفته در آن ماه. null = بدون اجبار.</summary>
        public int? ExactHolidayWeekendNightShiftCount { get; set; }

        /// <summary>null = مشارکت در مازاد شب (پیش‌فرض)؛ false = بدون مازاد؛ true = مشارکت صریح.</summary>
        public bool? NightFallbackParticipation { get; set; }

        /// <summary>null = مشارکت در مازاد شب تعطیل/آخرهفته (پیش‌فرض)؛ false = بدون مازاد.</summary>
        public bool? HolidayWeekendNightFallbackParticipation { get; set; }
    }
}
