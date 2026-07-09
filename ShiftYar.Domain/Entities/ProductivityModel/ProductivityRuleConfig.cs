using System;
using System.Collections.Generic;
using System.Linq;

namespace ShiftYar.Domain.Entities.ProductivityModel
{
    /// <summary>
    /// Describes configurable limits required to comply with the Regulation of Productivity Promotion
    /// for clinical employees in Iran.
    /// </summary>
    public class ProductivityRuleConfig
    {
        private static readonly IReadOnlyCollection<SeniorityReductionBand> DefaultBands = new List<SeniorityReductionBand>
        {
            new SeniorityReductionBand(1, 5, 1m),
            new SeniorityReductionBand(6, 10, 2m),
            new SeniorityReductionBand(11, 15, 3m),
            new SeniorityReductionBand(16, 20, 4m),
            new SeniorityReductionBand(21, null, 5m)
        };

        public decimal BaseWeeklyHours { get; init; } = 44m;
        public decimal MaxWeeklyReduction { get; init; } = 8m;
        public decimal HardshipReductionPerWeek { get; init; } = 2m;
        public decimal RotatingShiftReductionPerWeek { get; init; } = 1m;
        public decimal NightHolidayMultiplier { get; init; } = 1.5m;
        public IReadOnlyCollection<SeniorityReductionBand> SeniorityReductionBands { get; init; } = DefaultBands;

        public static ProductivityRuleConfig CreateDefault() => new ProductivityRuleConfig();

        /// <summary>
        /// Resolves the weekly seniority reduction defined by the national regulation.
        /// </summary>
        /// <param name="yearsOfService">Whole years of service for the staff.</param>
        /// <returns>Weekly hours reduction granted because of seniority.</returns>
        public decimal GetSeniorityReduction(int yearsOfService)
        {
            if (yearsOfService <= 0 || SeniorityReductionBands == null || SeniorityReductionBands.Count == 0)
            {
                return 0m;
            }

            var band = SeniorityReductionBands.FirstOrDefault(band => band.Contains(yearsOfService));
            return band?.ReductionHours ?? SeniorityReductionBands.Max(b => b.ReductionHours);
        }
    }
    
    /// <summary>
    /// Represents a band of seniority reduction hours defined by the national regulation.
    /// </summary>
    public class SeniorityReductionBand
    {
        public SeniorityReductionBand(int minYearsInclusive, int? maxYearsInclusive, decimal reductionHours)
        {
            if (minYearsInclusive < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(minYearsInclusive));
            }

            MinYearsInclusive = minYearsInclusive;
            MaxYearsInclusive = maxYearsInclusive;
            ReductionHours = reductionHours < 0 ? throw new ArgumentOutOfRangeException(nameof(reductionHours)) : reductionHours;
        }

        public int MinYearsInclusive { get; }
        public int? MaxYearsInclusive { get; }
        public decimal ReductionHours { get; }

        public bool Contains(int yearsOfService)
        {
            if (yearsOfService < MinYearsInclusive)
            {
                return false;
            }

            if (MaxYearsInclusive.HasValue)
            {
                return yearsOfService <= MaxYearsInclusive.Value;
            }

            return true;
        }
    }
}
