using System;

namespace ShiftYar.Application.DTOs.ProductivityModel
{
    public class WorkingHoursCalculationRequestDto
    {
        public StaffEmploymentInfoDto Staff { get; set; } = new StaffEmploymentInfoDto();
        public DateTime TargetMonth { get; set; }
        /// <summary>تعداد کل روزهای ماه (مثلاً ۳۰، ۳۱، ۲۹ روز).</summary>
        public int TotalDays { get; set; }
        /// <summary>تعداد روزهای کاری غیرتعطیل ماه (بدون جمعه‌ها و تعطیلات رسمی تقویمی).</summary>
        public int WorkingDays { get; set; }
        /// <summary>تعداد روزهای جمعه در بازه ماه.</summary>
        public int FridaysCount { get; set; }
        /// <summary>تعداد روزهای تعطیل رسمی تقویم شمسی به جز جمعه‌ها.</summary>
        public int OfficialHolidaysCount { get; set; }
        /// <summary>تعداد روزهای پنجشنبه در بازه ماه.</summary>
        public int ThursdaysCount { get; set; }
        /// <summary>آیا پنج‌شنبه‌ها از روزهای کاری کسر شوند.</summary>
        public bool ExcludeThursdays { get; set; }
        /// <summary>اعمال سقف قانونی ماه استاندارد (حداکثر ۲۴ روز کاری / ۱۷۶ ساعت در ماه ۴ هفته‌ای).</summary>
        public bool CapBaseHoursToStandardMonth { get; set; } = true;
        /// <summary>سقف دستی اختیاری برای ساعت پایه ناخالص ماهانه (پیش‌فرض ۱۷۶ ساعت).</summary>
        public decimal? MaxMonthlyBaseHours { get; set; }
        /// <summary>سقف دستی اختیاری برای تعداد روزهای کاری ماهانه (پیش‌فرض ۲۴ روز).</summary>
        public int? MaxMonthlyWorkingDays { get; set; }
        public int NumberOfWeeksInMonth { get; set; }
        public decimal NightHolidayHours { get; set; }
        public ProductivityRuleOverrideDto? RuleOverrides { get; set; }
    }
}

