using System;
using System.Collections.Generic;

namespace ShiftYar.Application.DTOs.ProductivityModel
{
    /// <summary>
    /// نتیجه تفصیلی محاسبه تقویمی ساعت موظفی ماهانه پرسنل بالینی نظام سلامت.
    /// منطبق بر دستورالعمل اجرایی قانون ارتقای بهره‌وری:
    /// ۱. ساعت کار پایه هر روز کاری = ۷.۳۳۳۳ ساعت (معادل ۷ ساعت و ۲۰ دقیقه = ۴۴ / ۶)
    /// ۲. تعداد روزهای کاری = کل روزهای ماه - (جمعه‌ها + تعطیلات رسمی تقویمی غیرجمعه)
    /// ۳. ساعت موظفی خام (ناخالص) = روزهای کاری × (۴۴ / ۶)
    /// ۴. کسر ساعت بهره‌وری ماهانه = کسر ساعت هفتگی کاربر × (طول ماه / ۷)
    /// ۵. ساعت موظفی خالص نهایی = Max(0, ساعت موظفی خام - کسر ساعت ماهانه)
    /// </summary>
    public class MonthlyCalendarWorkingHoursResultDto
    {
        public int StaffId { get; set; }
        public string? StaffFullName { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        public bool IsPersianCalendar { get; set; } = true;

        public DateTime MonthStartDate { get; set; }
        public DateTime MonthEndDate { get; set; }

        /// <summary>تعداد کل روزهای ماه (۲۹، ۳۰ یا ۳۱ روز)</summary>
        public int TotalDaysInMonth { get; set; }

        /// <summary>تعداد روزهای جمعه تقویم در آن ماه</summary>
        public int FridaysCount { get; set; }

        /// <summary>تعداد تعطیلات رسمی تقویمی وسط هفته (بدون احتساب جمعه‌ها)</summary>
        public int OfficialHolidaysCount { get; set; }

        /// <summary>تعداد روزهای کاری موظف ماه = کل روزها - (جمعه‌ها + تعطیلات رسمی تقویمی)</summary>
        public int WorkingDaysCount { get; set; }

        /// <summary>ساعت کار پایه هر روز کاری (۴۴ / ۶ = ۷.۳۳۳۳ ساعت)</summary>
        public decimal BaseDailyWorkingHours { get; set; } = 44.0m / 6.0m;

        /// <summary>ساعت موظفی خام/ناخالص ماهانه (Gross Monthly Hours) = WorkingDaysCount * (44 / 6)</summary>
        public decimal GrossMonthlyHours { get; set; }

        /// <summary>کسر ساعت هفتگی کاربر بر اساس قانون بهره‌وری (بین ۰.۰ تا حداکثر ۸.۰ ساعت)</summary>
        public decimal WeeklyProductivityReduction { get; set; }

        /// <summary>نسبت تعداد هفته‌های ماه بر اساس طول ماه = TotalDaysInMonth / 7.0</summary>
        public decimal MonthWeeksFactor { get; set; }

        /// <summary>کسر ساعت موظفی در طول کل ماه = WeeklyReduction * MonthWeeksFactor</summary>
        public decimal TotalMonthlyReduction { get; set; }

        /// <summary>ساعت موظفی خالص ماهانه (Net Monthly Required Hours) = Max(0, Gross - Deduction)</summary>
        public decimal NetMonthlyRequiredHours { get; set; }

        /// <summary>ساعت موظفی خالص گردشده به نزدیک‌ترین عدد صحیح</summary>
        public int NetMonthlyRequiredHoursRounded => (int)Math.Round(NetMonthlyRequiredHours, MidpointRounding.AwayFromZero);

        /// <summary>آیا پرسنل مشمول قانون ارتقای بهره‌وری است</summary>
        public bool IsIncludedInProductivityPlan { get; set; } = true;

        /// <summary>آیا ساعت موظفی دستی (MaxProductivityRequiredHours) برای کاربر تنظیم شده است</summary>
        public bool HasManualOverride { get; set; }

        /// <summary>مقدار ساعت موظفی دستی کاربر در صورت تنظیم</summary>
        public decimal? ManualOverrideHours { get; set; }

        /// <summary>تخفیف هفتگی سنوات خدمت</summary>
        public decimal SeniorityReductionPerWeek { get; set; }

        /// <summary>تخفیف هفتگی صعوبت کار</summary>
        public decimal HardshipReductionPerWeek { get; set; }

        /// <summary>تخفیف هفتگی نوبت‌کاری گردشی</summary>
        public decimal ShiftPatternReductionPerWeek { get; set; }

        /// <summary>یادداشت‌ها و مراحل محاسباتی</summary>
        public IList<string> Notes { get; set; } = new List<string>();
    }
}
