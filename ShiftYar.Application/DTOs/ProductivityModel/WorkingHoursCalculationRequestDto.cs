using System;

namespace ShiftYar.Application.DTOs.ProductivityModel
{
    public class WorkingHoursCalculationRequestDto
    {
        public StaffEmploymentInfoDto Staff { get; set; } = new StaffEmploymentInfoDto();
        public DateTime TargetMonth { get; set; }
        public int NumberOfWeeksInMonth { get; set; }
        public decimal NightHolidayHours { get; set; }
        public ProductivityRuleOverrideDto? RuleOverrides { get; set; }
    }
}

