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
        // قانون ارتقای بهره‌وری: ۰–۴، ۴–۸، ۸–۱۲، ۱۲–۱۶، ۱۶+ سال
        private static readonly IReadOnlyCollection<SeniorityReductionBand> DefaultSeniorityBands = new List<SeniorityReductionBand>
        {
            new SeniorityReductionBand(0, 3, 1m),
            new SeniorityReductionBand(4, 7, 2m),
            new SeniorityReductionBand(8, 11, 3m),
            new SeniorityReductionBand(12, 15, 4m),
            new SeniorityReductionBand(16, null, 5m)
        };

        // صعوبت کار: ۸–۲۵٪، ۲۶–۵۰٪، ۵۱–۷۵٪، ۷۶٪+
        private static readonly IReadOnlyCollection<HardshipReductionBand> DefaultHardshipBands = new List<HardshipReductionBand>
        {
            new HardshipReductionBand(8m, 25m, 0.5m),
            new HardshipReductionBand(26m, 50m, 1m),
            new HardshipReductionBand(51m, 75m, 1.5m),
            new HardshipReductionBand(76m, null, 2m)
        };

        public decimal BaseWeeklyHours { get; init; } = 44m;
        public decimal MaxWeeklyReduction { get; init; } = 8m;
        public decimal RotatingShiftReductionPerWeek { get; init; } = 1m;
        public decimal NightHolidayMultiplier { get; init; } = 1.5m;
        public decimal MaxMonthlyOvertimeHours { get; init; } = 80m;
        public decimal MaxConsecutiveWorkHours { get; init; } = 12m;
        public decimal HandoverHoursBetweenShifts { get; init; } = 1m;

        public IReadOnlyCollection<SeniorityReductionBand> SeniorityReductionBands { get; init; } = DefaultSeniorityBands;
        public IReadOnlyCollection<HardshipReductionBand> HardshipReductionBands { get; init; } = DefaultHardshipBands;

        public static ProductivityRuleConfig CreateDefault() => new ProductivityRuleConfig();

        public decimal GetSeniorityReduction(int yearsOfService)
        {
            if (yearsOfService <= 0 || SeniorityReductionBands == null || SeniorityReductionBands.Count == 0)
            {
                return 0m;
            }

            var band = SeniorityReductionBands.FirstOrDefault(b => b.Contains(yearsOfService));
            return band?.ReductionHours ?? SeniorityReductionBands.Max(b => b.ReductionHours);
        }

        public decimal GetHardshipReduction(decimal hardshipPercent)
        {
            if (hardshipPercent < 8m || HardshipReductionBands == null || HardshipReductionBands.Count == 0)
            {
                return 0m;
            }

            var band = HardshipReductionBands.FirstOrDefault(b => b.Contains(hardshipPercent));
            return band?.ReductionHours ?? 0m;
        }

        public decimal CalculateWeeklyRequiredHours(int yearsOfService, decimal hardshipPercent, bool hasRotatingShifts)
        {
            var seniorityReduction = GetSeniorityReduction(yearsOfService);
            var hardshipReduction = GetHardshipReduction(hardshipPercent);
            var rotatingReduction = hasRotatingShifts ? RotatingShiftReductionPerWeek : 0m;
            var totalReduction = Math.Min(MaxWeeklyReduction, seniorityReduction + hardshipReduction + rotatingReduction);
            return Math.Max(0m, BaseWeeklyHours - totalReduction);
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

    /// <summary>
    /// Band of weekly hour reduction based on hardship percentage (صعوبت کار).
    /// </summary>
    public class HardshipReductionBand
    {
        public HardshipReductionBand(decimal minPercentInclusive, decimal? maxPercentInclusive, decimal reductionHours)
        {
            if (minPercentInclusive < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(minPercentInclusive));
            }

            MinPercentInclusive = minPercentInclusive;
            MaxPercentInclusive = maxPercentInclusive;
            ReductionHours = reductionHours < 0 ? throw new ArgumentOutOfRangeException(nameof(reductionHours)) : reductionHours;
        }

        public decimal MinPercentInclusive { get; }
        public decimal? MaxPercentInclusive { get; }
        public decimal ReductionHours { get; }

        public bool Contains(decimal hardshipPercent)
        {
            if (hardshipPercent < MinPercentInclusive)
            {
                return false;
            }

            if (MaxPercentInclusive.HasValue)
            {
                return hardshipPercent <= MaxPercentInclusive.Value;
            }

            return true;
        }
    }
}
