using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.Common.Utilities
{
    /// <summary>
    /// کلاس کمکی برای تبدیل تاریخ‌های شمسی به میلادی
    /// </summary>
    public static class DateConverter
    {
        /// <summary>
        /// تبدیل رشته تاریخ شمسی به تاریخ میلادی
        /// </summary>
        /// <param name="persianDate">تاریخ شمسی به فرمت yyyy/mm/dd</param>
        /// <returns>تاریخ میلادی</returns>
        public static DateTime ConvertToGregorianDate(string persianDate)
        {
            if (string.IsNullOrWhiteSpace(persianDate))
                throw new ArgumentException("تاریخ وارد شده نامعتبر است");

            // رشته تاریخ را به بخش‌های سال، ماه و روز تقسیم می‌کنیم
            var parts = persianDate.Split('/');
            if (parts.Length != 3)
                throw new FormatException("فرمت تاریخ وارد شده صحیح نیست");

            int year = int.Parse(parts[0]);
            int month = int.Parse(parts[1]);
            int day = int.Parse(parts[2]);

            var persianCalendar = new PersianCalendar();
            return persianCalendar.ToDateTime(year, month, day, 0, 0, 0, 0);
        }
    }
}
