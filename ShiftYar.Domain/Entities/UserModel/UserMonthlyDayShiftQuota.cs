using ShiftYar.Domain.Entities.BaseModel;
using ShiftYar.Domain.Enums.ShiftModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShiftYar.Domain.Entities.UserModel
{
    /// <summary>
    /// سهمیه صبح/عصر هر کاربر برای یک ماه شمسی + ترجیح مشارکت در توزیع مازاد.
    /// </summary>
    public class UserMonthlyDayShiftQuota : BaseEntity
    {
        [Key]
        public int? Id { get; set; }

        [ForeignKey(nameof(User))]
        public int UserId { get; set; }
        public User? User { get; set; }

        public int PersianYear { get; set; }
        public int PersianMonth { get; set; }

        public int? ExactMorningShiftCount { get; set; }
        /// <summary>null = مشارکت در مازاد (پیش‌فرض)؛ false = بدون مازاد؛ true = مشارکت صریح در مازاد</summary>
        public bool? MorningFallbackParticipation { get; set; }
        public int? ExactHolidayMorningShiftCount { get; set; }
        public bool? MorningHolidayFallbackParticipation { get; set; }

        public int? ExactEveningShiftCount { get; set; }
        public bool? EveningFallbackParticipation { get; set; }
        public int? ExactHolidayEveningShiftCount { get; set; }
        public bool? EveningHolidayFallbackParticipation { get; set; }

        /// <summary>وضعیت فعال بودن تخصیص شیفت‌های متناوب هفتگی (صبح و عصر) در ماه جاری</summary>
        public bool IsWeeklyAlternatingActive { get; set; } = false;

        /// <summary>شیفت انتخابی برای هفته اول ماه: صبح (0) یا عصر (1)</summary>
        public ShiftEnums.ShiftLabel? FirstWeekShiftLabel { get; set; }
    }
}
