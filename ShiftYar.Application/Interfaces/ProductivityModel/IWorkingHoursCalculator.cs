using System;
using System.Collections.Generic;
using ShiftYar.Application.DTOs.ProductivityModel;
using ShiftYar.Domain.Entities.ProductivityModel;
using ShiftYar.Domain.Entities.UserModel;

namespace ShiftYar.Application.Interfaces.ProductivityModel
{
    /// <summary>
    /// محاسبات ساعت موظفی، کسر ساعت بهره‌وری و ساعت موظفی خالص ماهانه پرسنل بالینی نظام سلامت.
    /// </summary>
    public interface IWorkingHoursCalculator
    {
        /// <summary>
        /// متد پیشین: محاسبه ساعت موظفی بر اساس مدل درخواست محاسباتی.
        /// </summary>
        WorkingHoursCalculationResultDto CalculateMonthlyHours(WorkingHoursCalculationRequestDto request);

        /// <summary>
        /// دریافت میزان تخفیف هفتگی بهره‌وری کاربر (عددی بین ۰.۰ تا حداکثر ۸.۰ ساعت)
        /// بر اساس سنوات خدمت، سختی/صعوبت کار و نوبت‌کاری گردشی.
        /// </summary>
        decimal GetWeeklyProductivityReduction(User user, DateTime? referenceDate = null, ProductivityRuleConfig? ruleConfig = null);

        /// <summary>
        /// دریافت میزان تخفیف هفتگی بهره‌وری بر اساس مدل DTO اطلاعات استخدامی پرسنل.
        /// </summary>
        decimal GetWeeklyProductivityReduction(StaffEmploymentInfoDto staff, DateTime? referenceDate = null, ProductivityRuleConfig? ruleConfig = null);

        /// <summary>
        /// محاسبه ساعت موظفی خالص ماهانه (Net Monthly Required Hours) پرسنل درمان برای یک سال و ماه مشخص (شمسی یا میلادی).
        /// با دقت ۲ رقم اعشار گرد می‌شود.
        /// </summary>
        decimal CalculateMonthlyRequiredHours(User user, int year, int month, ISet<DateTime>? officialHolidays = null, int? numberOfWeeksInMonth = null);

        /// <summary>
        /// محاسبه تفصیلی ساعت موظفی تقویمی ماهانه پرسنل درمان شامل روزهای کاری، ساعت خام، کسر ساعت و ساعت موظف خالص.
        /// </summary>
        MonthlyCalendarWorkingHoursResultDto CalculateMonthlyRequiredHoursDetails(User user, int year, int month, ISet<DateTime>? officialHolidays = null, int? numberOfWeeksInMonth = null);

        /// <summary>
        /// محاسبه تفصیلی ساعت موظفی ماهانه پرسنل بر مبنای تعداد کل روزها و روزهای کاری موظف داده‌شده.
        /// </summary>
        MonthlyCalendarWorkingHoursResultDto CalculateMonthlyRequiredHoursForDaysDetails(User user, int totalDaysInMonth, int workingDaysCount, DateTime? referenceDate = null, int? numberOfWeeksInMonth = null);

        /// <summary>
        /// محاسبه ساعت موظفی خالص ماهانه پرسنل بر مبنای تعداد کل روزها و روزهای کاری موظف داده‌شده.
        /// </summary>
        decimal CalculateMonthlyRequiredHoursForDays(User user, int totalDaysInMonth, int workingDaysCount, DateTime? referenceDate = null, int? numberOfWeeksInMonth = null);
    }
}
