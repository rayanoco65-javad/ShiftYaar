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
        // قانون ارتقای بهره‌وری: ۰.۵ ساعت به ازای هر ۵ سال سابقه کار بالینی (سقف ۲ ساعت برای ۲۰ سال و بالاتر)
        private static readonly IReadOnlyCollection<SeniorityReductionBand> DefaultSeniorityBands = new List<SeniorityReductionBand>
        {
            new SeniorityReductionBand(0, 4, 0m),
            new SeniorityReductionBand(5, 9, 0.5m),
            new SeniorityReductionBand(10, 14, 1.0m),
            new SeniorityReductionBand(15, 19, 1.5m),
            new SeniorityReductionBand(20, null, 2.0m)
        };

        // صعوبت کار: بخش‌های ویژه (۲ ساعت) و بخش‌های جنرال/عادی (۱ تا ۱.۵ ساعت)
        private static readonly IReadOnlyCollection<HardshipReductionBand> DefaultHardshipBands = new List<HardshipReductionBand>
        {
            new HardshipReductionBand(0m, 25m, 1.0m),
            new HardshipReductionBand(26m, 50m, 1.5m),
            new HardshipReductionBand(51m, null, 2.0m)
        };

        /// <summary>ساعت کار پایه روزانه غیرتعطیل (۷ ساعت و ۲۰ دقیقه = ۲۲/۳ = ۷.۳۳۳۳۳۳ ساعت)</summary>
        public const decimal BaseDailyWorkingHours = 22m / 3m;

        public decimal BaseWeeklyHours { get; init; } = 44m;
        public decimal MaxWeeklyReduction { get; init; } = 8m;
        public decimal SpecialSectionHardshipReduction { get; init; } = 2.0m;
        public decimal GeneralSectionHardshipReduction { get; init; } = 1.0m;
        public decimal RotatingShiftReductionPerWeek { get; init; } = 3.0m;
        public decimal ThreeShiftRotatingReductionHours { get; init; } = 1.0m;
        public decimal TwoShiftRotatingReductionHours { get; init; } = 0.5m;
        public decimal FixedNightReductionHours { get; init; } = 1.0m;
        public decimal FixedDayReductionHours { get; init; } = 0.0m;
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
            if (hardshipPercent <= 0m || HardshipReductionBands == null || HardshipReductionBands.Count == 0)
            {
                return 0m;
            }

            var band = HardshipReductionBands.FirstOrDefault(b => b.Contains(hardshipPercent));
            return band?.ReductionHours ?? 0m;
        }

        public decimal GetSectionHardshipReduction(bool isSpecialSection, decimal? customHardshipReduction = null)
        {
            if (customHardshipReduction.HasValue && customHardshipReduction.Value >= 0)
            {
                return customHardshipReduction.Value;
            }

            return isSpecialSection ? SpecialSectionHardshipReduction : GeneralSectionHardshipReduction;
        }

        public decimal GetShiftPatternReduction(ShiftPatternType pattern)
        {
            return pattern switch
            {
                ShiftPatternType.ThreeShiftRotating => ThreeShiftRotatingReductionHours,
                ShiftPatternType.TwoShiftRotating   => TwoShiftRotatingReductionHours,
                ShiftPatternType.FixedNight        => FixedNightReductionHours,
                ShiftPatternType.FixedDay          => FixedDayReductionHours,
                _ => 0.0m
            };
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
