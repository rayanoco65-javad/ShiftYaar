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
        public int NumberOfWeeksInMonth { get; set; }
        public decimal NightHolidayHours { get; set; }
        public ProductivityRuleOverrideDto? RuleOverrides { get; set; }
    }
}

