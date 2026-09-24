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
        public int FridaysCount { get; set; }
        public int OfficialHolidaysCount { get; set; }
        public int ThursdaysCount { get; set; }
        public bool IsCappedToStandardMonth { get; set; }
        public decimal BaseHoursPerDay { get; set; } = 22m / 3m;
        public decimal BaseWeeklyHours { get; set; }
        public decimal WeeklyRequiredHours { get; set; }
        public decimal NetRequiredHours { get; set; }
        /// <summary>ساعت موظفی خالص گردشده به نزدیک‌ترین عدد صحیح.</summary>
        public int NetRequiredHoursRounded => (int)System.Math.Round(NetRequiredHours, System.MidpointRounding.AwayFromZero);
        public decimal SeniorityReductionPerWeek { get; set; }
        public decimal HardshipReductionPerWeek { get; set; }
        /// <summary>امتیاز سختی کار قانون مدیریت خدمات کشوری (در صورت استفاده).</summary>
        public decimal? HardshipScore { get; set; }
        /// <summary>آیا رده مدیریت بالینی (ماده ۴: سوپروایزر، سرپرستار، مترون) تشخیص داده شده است.</summary>
        public bool IsClinicalManager { get; set; }
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

