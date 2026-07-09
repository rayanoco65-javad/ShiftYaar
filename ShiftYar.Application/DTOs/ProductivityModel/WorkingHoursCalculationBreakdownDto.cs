using System.Collections.Generic;

namespace ShiftYar.Application.DTOs.ProductivityModel
{
    /// <summary>
    /// Detailed breakdown of the working hours calculation.
    /// </summary>
    public class WorkingHoursCalculationBreakdownDto
    {
        public decimal BaseWeeklyHours { get; set; }
        public decimal WeeklyRequiredHours { get; set; }
        public decimal SeniorityReductionPerWeek { get; set; }
        public decimal HardshipReductionPerWeek { get; set; }
        public decimal RotatingShiftReductionPerWeek { get; set; }
        public decimal TotalWeeklyReduction { get; set; }
        public decimal MonthlyReductionFromWeeklyAdjustments { get; set; }
        public decimal NightHolidayHoursReported { get; set; }
        public decimal NightHolidayWeightedHours { get; set; }
        public decimal NightHolidayCreditHours { get; set; }
        public IList<string> Notes { get; set; } = new List<string>();
    }
}

