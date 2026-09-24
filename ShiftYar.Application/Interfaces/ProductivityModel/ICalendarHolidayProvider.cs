using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ShiftYar.Application.Interfaces.ProductivityModel
{
    /// <summary>
    /// اطلاعات آماری روزهای تقویم، جمعه‌ها، تعطیلات رسمی و روزهای کاری موظف ماه.
    /// </summary>
    public class MonthWorkingDaysInfo
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public bool IsPersian { get; set; } = true;
        public DateTime MonthStart { get; set; }
        public DateTime MonthEnd { get; set; }
        public int TotalDays { get; set; }
        public int FridaysCount { get; set; }
        public int MidWeekOfficialHolidaysCount { get; set; }
        public int WorkingDaysCount { get; set; }
        public List<DateTime> FridayDates { get; set; } = new();
        public List<DateTime> MidWeekOfficialHolidayDates { get; set; } = new();
    }

    /// <summary>
    /// تأمین‌کننده محاسبات تقویم و روزهای تعطیل رسمی (پشتیبانی از تقویم شمسی و میلادی).
    /// </summary>
    public interface ICalendarHolidayProvider
    {
        /// <summary>
        /// استخراج مرزهای شروع، پایان و تعداد روزهای ماه بر اساس سال و ماه (شمسی در بازه ۱۲۰۰ تا ۱۶۰۰، در غیر این صورت میلادی).
        /// </summary>
        (DateTime MonthStart, DateTime MonthEnd, int DaysInMonth, bool IsPersian) GetMonthBounds(int year, int month);

        /// <summary>
        /// دریافت روزهای تعطیل رسمی در یک بازه زمانی مشخص به صورت ناهمگام.
        /// </summary>
        Task<ISet<DateTime>> GetOfficialHolidaysAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

        /// <summary>
        /// دریافت روزهای تعطیل رسمی در یک بازه زمانی مشخص به صورت همگام.
        /// </summary>
        ISet<DateTime> GetOfficialHolidays(DateTime startDate, DateTime endDate, int? persianYear = null);

        /// <summary>
        /// محاسبه روزهای کاری، جمعه‌ها و تعطیلات رسمی وسط هفته برای یک سال و ماه معین.
        /// </summary>
        MonthWorkingDaysInfo GetMonthWorkingDaysInfo(int year, int month, ISet<DateTime>? customOfficialHolidays = null);

        /// <summary>
        /// محاسبه روزهای کاری، جمعه‌ها و تعطیلات رسمی وسط هفته برای یک سال و ماه معین به صورت ناهمگام.
        /// </summary>
        Task<MonthWorkingDaysInfo> GetMonthWorkingDaysInfoAsync(int year, int month, ISet<DateTime>? customOfficialHolidays = null, CancellationToken cancellationToken = default);
    }
}
