using System;
using System.Collections.Generic;
using System.Linq;

namespace ShiftYar.Domain.Entities.ProductivityModel
{
    /// <summary>
    /// تنظیمات و بازه‌های محاسباتی قانون ارتقای بهره‌وری کارکنان بالینی نظام سلامت وزارت بهداشت.
    /// منطبق بر مصوبه هیئت وزیران و دستورالعمل اجرایی ابلاغی وزارت بهداشت.
    /// </summary>
    public class ProductivityRuleConfig
    {
        // ۱. کاهش سابقه خدمت (سنوات):
        // ۰ تا ۴ سال: ۱.۰ ساعت کسر در هفته (شامل پرسنل بدو خدمت و طرحی)
        // ۴ سال و ۱ ماه تا ۸ سال: ۲.۰ ساعت کسر در هفته
        // ۸ سال و ۱ ماه تا ۱۲ سال: ۳.۰ ساعت کسر در هفته
        // ۱۲ سال و ۱ ماه تا ۱۶ سال: ۴.۰ ساعت کسر در هفته
        // ۱۶ سال و ۱ ماه به بالا: ۵.۰ ساعت کسر در هفته
        private static readonly IReadOnlyCollection<SeniorityReductionBand> DefaultSeniorityBands = new List<SeniorityReductionBand>
        {
            new SeniorityReductionBand(0m, 4m, 1.0m),
            new SeniorityReductionBand(5m, 8m, 2.0m),
            new SeniorityReductionBand(9m, 12m, 3.0m),
            new SeniorityReductionBand(13m, 16m, 4.0m),
            new SeniorityReductionBand(17m, null, 5.0m)
        };

        // ۲. کاهش صعوبت/سختی کار بر اساس درصد نظام هماهنگ (حداکثر ۲ ساعت):
        // ۸ تا ۲۵ درصد: ۰.۵ ساعت کسر در هفته
        // ۲۶ تا ۵۰ درصد: ۱.۰ ساعت کسر در هفته
        // ۵۱ تا ۷۵ درصد: ۱.۵ ساعت کسر در هفته
        // ۷۶ تا ۱۰۰ درصد: ۲.۰ ساعت کسر در هفته
        // زیر ۸ درصد: ۰.۰ ساعت کسر
        private static readonly IReadOnlyCollection<HardshipReductionBand> DefaultHardshipBands = new List<HardshipReductionBand>
        {
            new HardshipReductionBand(8m, 25m, 0.5m),
            new HardshipReductionBand(26m, 50m, 1.0m),
            new HardshipReductionBand(51m, 75m, 1.5m),
            new HardshipReductionBand(76m, null, 2.0m)
        };

        // ۲. کاهش صعوبت/سختی کار بر اساس امتیاز قانون مدیریت خدمات کشوری (حداکثر ۲ ساعت):
        // ۰ تا ۳۷۵ امتیاز: ۰.۵ ساعت کسر در هفته
        // ۳۷۶ تا ۷۵۰ امتیاز: ۱.۰ ساعت کسر در هفته
        // ۷۵۱ تا ۱۰۰۰ امتیاز: ۱.۵ ساعت کسر در هفته
        // ۱۰۰۰ امتیاز به بالا: ۲.۰ ساعت کسر در هفته
        // فاقد امتیاز / منفی: ۰.۰ ساعت کسر
        private static readonly IReadOnlyCollection<HardshipScoreReductionBand> DefaultHardshipScoreBands = new List<HardshipScoreReductionBand>
        {
            new HardshipScoreReductionBand(0m, 375m, 0.5m),
            new HardshipScoreReductionBand(376m, 750m, 1.0m),
            new HardshipScoreReductionBand(751m, 1000m, 1.5m),
            new HardshipScoreReductionBand(1001m, null, 2.0m)
        };

        /// <summary>ساعت کار پایه روزانه غیرتعطیل (۷ ساعت و ۲۰ دقیقه = ۲۲/۳ = ۷.۳۳۳۳۳۳ ساعت)</summary>
        public const decimal BaseDailyWorkingHours = 22m / 3m;

        public decimal DailyWorkingHours { get; init; } = BaseDailyWorkingHours;
        public decimal BaseWeeklyHours { get; init; } = 44m;
        public decimal MaxWeeklyReduction { get; init; } = 8m;
        /// <summary>سقف استاندارد ساعت کار پایه ماهانه در صورت فعال‌سازی تنظیم کارگزینی (۴۴ × ۴ = ۱۷۶ ساعت)</summary>
        public decimal MaxMonthlyBaseHours { get; init; } = 176m;
        /// <summary>سقف استاندارد روزهای کاری موظف در صورت فعال‌سازی تنظیم کارگزینی (۶ × ۴ = ۲۴ روز)</summary>
        public int MaxMonthlyWorkingDays { get; init; } = 24;
        /// <summary>فعال بودن سقف‌گذاری ساعت پایه بر مبنای ماه استاندارد (پیش‌فرض: false تا روزهای کاری تقویمی دقیقاً محاسبه شوند)</summary>
        public bool CapBaseHoursToStandardMonth { get; init; } = false;
        public decimal SpecialSectionHardshipReduction { get; init; } = 2.0m;
        public decimal GeneralSectionHardshipReduction { get; init; } = 0.0m;
        public decimal RotatingShiftReductionPerWeek { get; init; } = 1.0m;
        public decimal ThreeShiftRotatingReductionHours { get; init; } = 1.0m;
        public decimal TwoShiftRotatingReductionHours { get; init; } = 1.0m;
        public decimal FixedNightReductionHours { get; init; } = 1.0m;
        public decimal FixedDayReductionHours { get; init; } = 0.0m;
        public decimal NightHolidayMultiplier { get; init; } = 1.5m;
        public decimal MaxMonthlyOvertimeHours { get; init; } = 80m;
        public decimal MaxConsecutiveWorkHours { get; init; } = 12m;
        public decimal HandoverHoursBetweenShifts { get; init; } = 1m;

        public IReadOnlyCollection<SeniorityReductionBand> SeniorityReductionBands { get; init; } = DefaultSeniorityBands;
        public IReadOnlyCollection<HardshipReductionBand> HardshipReductionBands { get; init; } = DefaultHardshipBands;
        public IReadOnlyCollection<HardshipScoreReductionBand> HardshipScoreReductionBands { get; init; } = DefaultHardshipScoreBands;

        public static ProductivityRuleConfig CreateDefault() => new ProductivityRuleConfig();

        /// <summary>
        /// استخراج کاهش ساعت ناشی از سابقه خدمت بر اساس سال اعشاری یا ماه‌های سابقه خدمت.
        /// </summary>
        public decimal GetSeniorityReduction(decimal yearsOfService)
        {
            if (yearsOfService < 0m)
            {
                return 0m;
            }

            if (SeniorityReductionBands != null && SeniorityReductionBands.Count > 0)
            {
                var band = SeniorityReductionBands.FirstOrDefault(b => b.Contains(yearsOfService));
                if (band != null)
                {
                    return band.ReductionHours;
                }
            }

            // فال‌بک مطابق دستورالعمل رسمی وزارت بهداشت
            if (yearsOfService <= 4.0m) return 1.0m;
            if (yearsOfService <= 8.0m) return 2.0m;
            if (yearsOfService <= 12.0m) return 3.0m;
            if (yearsOfService <= 16.0m) return 4.0m;
            return 5.0m;
        }

        public decimal GetSeniorityReduction(int yearsOfService)
            => GetSeniorityReduction((decimal)yearsOfService);

        /// <summary>
        /// استخراج کاهش ساعت ناشی از درصد سختی کار (۸–۲۵: ۰.۵h، ۲۶–۵۰: ۱.۰h، ۵۱–۷۵: ۱.۵h، ۷۶–۱۰۰: ۲.۰h، زیر ۸: ۰h).
        /// </summary>
        public decimal GetHardshipReduction(decimal hardshipPercent)
        {
            if (hardshipPercent < 8m)
            {
                return 0m;
            }

            if (HardshipReductionBands != null && HardshipReductionBands.Count > 0)
            {
                var band = HardshipReductionBands.FirstOrDefault(b => b.Contains(hardshipPercent));
                if (band != null)
                {
                    return band.ReductionHours;
                }
            }

            if (hardshipPercent <= 25m) return 0.5m;
            if (hardshipPercent <= 50m) return 1.0m;
            if (hardshipPercent <= 75m) return 1.5m;
            return 2.0m;
        }

        /// <summary>
        /// استخراج کاهش ساعت ناشی از امتیاز سختی کار قانون مدیریت خدمات کشوری (۰–۳۷۵: ۰.۵h، ۳۷۶–۷۵۰: ۱.۰h، ۷۵۱–۱۰۰۰: ۱.۵h، ۱۰۰۰ به بالا: ۲.۰h).
        /// </summary>
        public decimal GetHardshipReductionFromScore(decimal hardshipScore)
        {
            if (hardshipScore < 0m)
            {
                return 0m;
            }

            if (HardshipScoreReductionBands != null && HardshipScoreReductionBands.Count > 0)
            {
                var band = HardshipScoreReductionBands.FirstOrDefault(b => b.Contains(hardshipScore));
                if (band != null)
                {
                    return band.ReductionHours;
                }
            }

            if (hardshipScore <= 375m) return 0.5m;
            if (hardshipScore <= 750m) return 1.0m;
            if (hardshipScore <= 1000m) return 1.5m;
            return 2.0m;
        }

        /// <summary>
        /// استخراج یکپارچه تخفیف صعوبت کار با در نظر گرفتن رده‌های مدیریتی بالینی (ماده ۴ دستورالعمل) و فیلدهای دوگانه.
        /// </summary>
        public decimal GetHardshipReduction(decimal hardshipPercent, decimal? hardshipScore = null, bool isClinicalManager = false)
        {
            if (isClinicalManager)
            {
                return 2.0m; // ماده ۴ دستورالعمل: سوپروایزر، سرپرستار، مترون -> سقف ۲ ساعت کسر
            }

            if (hardshipScore.HasValue)
            {
                return GetHardshipReductionFromScore(hardshipScore.Value);
            }

            return GetHardshipReduction(hardshipPercent);
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

        public decimal CalculateWeeklyRequiredHours(int yearsOfService, decimal hardshipPercent, decimal? hardshipScore, bool hasRotatingShifts, bool isClinicalManager = false)
        {
            var seniorityReduction = GetSeniorityReduction(yearsOfService);
            var hardshipReduction = GetHardshipReduction(hardshipPercent, hardshipScore, isClinicalManager);
            var rotatingReduction = hasRotatingShifts ? RotatingShiftReductionPerWeek : 0m;
            var totalReduction = Math.Min(MaxWeeklyReduction, seniorityReduction + hardshipReduction + rotatingReduction);
            return Math.Max(0m, BaseWeeklyHours - totalReduction);
        }
    }

    /// <summary>
    /// بازه تخفیف سنوات خدمت بر اساس مصوبه دستورالعمل اجرایی وزارت بهداشت.
    /// </summary>
    public class SeniorityReductionBand
    {
        public SeniorityReductionBand(decimal minYearsInclusive, decimal? maxYearsInclusive, decimal reductionHours)
        {
            if (minYearsInclusive < 0m)
            {
                throw new ArgumentOutOfRangeException(nameof(minYearsInclusive));
            }

            MinYearsInclusive = minYearsInclusive;
            MaxYearsInclusive = maxYearsInclusive;
            ReductionHours = reductionHours < 0m ? throw new ArgumentOutOfRangeException(nameof(reductionHours)) : reductionHours;
        }

        public SeniorityReductionBand(int minYearsInclusive, int? maxYearsInclusive, decimal reductionHours)
            : this((decimal)minYearsInclusive, maxYearsInclusive.HasValue ? (decimal?)maxYearsInclusive.Value : null, reductionHours)
        {
        }

        public decimal MinYearsInclusive { get; }
        public decimal? MaxYearsInclusive { get; }
        public decimal ReductionHours { get; }

        public bool Contains(decimal yearsOfService)
        {
            if (yearsOfService >= MinYearsInclusive)
            {
                if (!MaxYearsInclusive.HasValue)
                {
                    return true;
                }
                return yearsOfService <= MaxYearsInclusive.Value;
            }

            // برای سابقه ۴ سال و ۱ ماه (مثلاً ۴.۰۸۳ سال): چنانچه بازه به صورت عددی ۵ تا ۸ ثبت شده باشد
            // مقدار ۴.۰۸۳ سال که بیشتر از ۴ سال است را در بازه بعدی جای می‌دهد.
            if (MinYearsInclusive > 0m && yearsOfService > (MinYearsInclusive - 1.0m))
            {
                if (!MaxYearsInclusive.HasValue || yearsOfService <= MaxYearsInclusive.Value)
                {
                    return true;
                }
            }

            return false;
        }

        public bool Contains(int yearsOfService) => Contains((decimal)yearsOfService);
    }

    /// <summary>
    /// بازه تخفیف هفتگی بر اساس درصد صعوبت کار (نظام هماهنگ پرداخت).
    /// </summary>
    public class HardshipReductionBand
    {
        public HardshipReductionBand(decimal minPercentInclusive, decimal? maxPercentInclusive, decimal reductionHours)
        {
            if (minPercentInclusive < 0m)
            {
                throw new ArgumentOutOfRangeException(nameof(minPercentInclusive));
            }

            MinPercentInclusive = minPercentInclusive;
            MaxPercentInclusive = maxPercentInclusive;
            ReductionHours = reductionHours < 0m ? throw new ArgumentOutOfRangeException(nameof(reductionHours)) : reductionHours;
        }

        public decimal MinPercentInclusive { get; }
        public decimal? MaxPercentInclusive { get; }
        public decimal ReductionHours { get; }

        public bool Contains(decimal hardshipPercent)
        {
            if (hardshipPercent < MinPercentInclusive)
            {
                if (MinPercentInclusive > 0m && hardshipPercent > (MinPercentInclusive - 1.0m))
                {
                    if (!MaxPercentInclusive.HasValue || hardshipPercent <= MaxPercentInclusive.Value)
                    {
                        return true;
                    }
                }
                return false;
            }

            if (MaxPercentInclusive.HasValue)
            {
                return hardshipPercent <= MaxPercentInclusive.Value;
            }

            return true;
        }
    }

    /// <summary>
    /// بازه تخفیف هفتگی بر اساس امتیاز سختی کار (قانون مدیریت خدمات کشوری).
    /// </summary>
    public class HardshipScoreReductionBand
    {
        public HardshipScoreReductionBand(decimal minScoreInclusive, decimal? maxScoreInclusive, decimal reductionHours)
        {
            if (minScoreInclusive < 0m)
            {
                throw new ArgumentOutOfRangeException(nameof(minScoreInclusive));
            }

            MinScoreInclusive = minScoreInclusive;
            MaxScoreInclusive = maxScoreInclusive;
            ReductionHours = reductionHours < 0m ? throw new ArgumentOutOfRangeException(nameof(reductionHours)) : reductionHours;
        }

        public decimal MinScoreInclusive { get; }
        public decimal? MaxScoreInclusive { get; }
        public decimal ReductionHours { get; }

        public bool Contains(decimal score)
        {
            if (score < MinScoreInclusive)
            {
                if (MinScoreInclusive > 0m && score > (MinScoreInclusive - 1.0m))
                {
                    if (!MaxScoreInclusive.HasValue || score <= MaxScoreInclusive.Value)
                    {
                        return true;
                    }
                }
                return false;
            }

            if (MaxScoreInclusive.HasValue)
            {
                return score <= MaxScoreInclusive.Value;
            }

            return true;
        }
    }
}
