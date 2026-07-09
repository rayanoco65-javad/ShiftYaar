using System.Collections.Generic;

namespace ShiftYar.Application.DTOs.ProductivityModel
{
    /// <summary>
    /// Optional overrides for the national productivity regulation. All values are nullable so callers can override selectively.
    /// </summary>
    public class ProductivityRuleOverrideDto
    {
        public decimal? BaseWeeklyHours { get; set; }
        public decimal? MaxWeeklyReduction { get; set; }
        public decimal? HardshipReductionPerWeek { get; set; }
        public decimal? RotatingShiftReductionPerWeek { get; set; }
        public decimal? NightHolidayMultiplier { get; set; }
        public IList<SeniorityReductionBandDto>? SeniorityReductionBands { get; set; }
    }

    public class SeniorityReductionBandDto
    {
        public int MinYearsInclusive { get; set; }
        public int? MaxYearsInclusive { get; set; }
        public decimal ReductionHours { get; set; }
    }
}

