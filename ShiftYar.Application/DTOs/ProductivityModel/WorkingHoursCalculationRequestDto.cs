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
        /// <summary>تعداد روزهای پنجشنبه در بازه ماه (صرفاً آماری).</summary>
        public int ThursdaysCount { get; set; }
        /// <summary>آیا پنج‌شنبه‌ها از روزهای کاری کسر شوند (پیش‌فرض false: بیمارستان‌ها ۲۴ ساعته هستند و پنج‌شنبه روز کاری است).</summary>
        public bool ExcludeThursdays { get; set; } = false;
        /// <summary>سقف‌گذاری اختیاری ساعت پایه بر مبنای ماه استاندارد کارگزینی (حداکثر ۲۴ روز کاری / ۱۷۶ ساعت).</summary>
        public bool? CapBaseHoursToStandardMonth { get; set; }
        /// <summary>سقف دستی اختیاری برای ساعت پایه ناخالص ماهانه (پیش‌فرض ۱۷۶ ساعت).</summary>
        public decimal? MaxMonthlyBaseHours { get; set; }
        /// <summary>سقف دستی اختیاری برای تعداد روزهای کاری ماهانه (پیش‌فرض ۲۴ روز).</summary>
        public int? MaxMonthlyWorkingDays { get; set; }
        public int NumberOfWeeksInMonth { get; set; }
        public decimal NightHolidayHours { get; set; }
        public ProductivityRuleOverrideDto? RuleOverrides { get; set; }
    }
}

