using System.Collections.Generic;
using ShiftYar.Domain.Entities.ProductivityModel;

namespace ShiftYar.Application.DTOs.ProductivityModel
{
    /// <summary>
    /// Detailed breakdown of the working hours calculation.
    /// </summary>
    public class WorkingHoursCalculationBreakdownDto
    {
        public bool IsIncludedInProductivityPlan { get; set; } = true;
        public int TotalDays { get; set; }
        public int WorkingDays { get; set; }
        public decimal BaseHoursPerDay { get; set; } = 22m / 3m;
        public decimal BaseWeeklyHours { get; set; }
        public decimal WeeklyRequiredHours { get; set; }
        public decimal SeniorityReductionPerWeek { get; set; }
        public decimal HardshipReductionPerWeek { get; set; }
        public ShiftPatternType ShiftPattern { get; set; } = ShiftPatternType.FixedDay;
        public decimal ShiftPatternReductionPerWeek { get; set; }
        public decimal RotatingShiftReductionPerWeek { get; set; }
        public decimal TotalWeeklyReduction { get; set; }
        public decimal MonthlyReductionFromWeeklyAdjustments { get; set; }
        public decimal NightHolidayHoursReported { get; set; }
        public decimal NightHolidayWeightedHours { get; set; }
        public decimal NightHolidayCreditHours { get; set; }
        public IList<string> Notes { get; set; } = new List<string>();
    }
}

