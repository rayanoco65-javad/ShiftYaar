using System.Collections.Generic;

namespace ShiftYar.Application.DTOs.ProductivityModel
{
    /// <summary>
    /// Optional overrides for the national productivity regulation. All values are nullable so callers can override selectively.
    /// </summary>
    public class ProductivityRuleOverrideDto
    {
        public decimal? BaseDailyWorkingHours { get; set; }
        public decimal? BaseWeeklyHours { get; set; }
        public decimal? MaxWeeklyReduction { get; set; }
        public decimal? MaxMonthlyBaseHours { get; set; }
        public int? MaxMonthlyWorkingDays { get; set; }
        public bool? CapBaseHoursToStandardMonth { get; set; }
        public bool? ExcludeThursdays { get; set; }
        public decimal? HardshipReductionPerWeek { get; set; }
        public decimal? RotatingShiftReductionPerWeek { get; set; }
        public decimal? ThreeShiftRotatingReductionHours { get; set; }
        public decimal? TwoShiftRotatingReductionHours { get; set; }
        public decimal? FixedNightReductionHours { get; set; }
        public decimal? FixedDayReductionHours { get; set; }
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

