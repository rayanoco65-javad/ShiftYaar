using System;
using System.Globalization;
using System.Text;
using ShiftYar.Domain.Entities.ProductivityModel;

namespace ShiftYar.Application.Common.Utilities
{
    /// <summary>
    /// تبدیل تاریخ شمسی ↔ میلادی با پشتیبانی از ارقام فارسی/عربی و جداکننده‌های رایج.
    /// </summary>
    public static class DateConverter
    {
        private static readonly PersianCalendar PersianCalendar = new();

        /// <summary>
        /// اگر تاریخ استخدام به‌اشتباه با اجزای شمسی در فیلد میلادی ذخیره شده باشد، آن را به میلادی واقعی تبدیل می‌کند.
        /// </summary>
        public static DateTime NormalizeEmploymentDate(DateTime stored)
            => StaffEmploymentInfo.NormalizeEmploymentDate(stored);

        public static DateTime? NormalizeEmploymentDate(DateTime? stored)
            => StaffEmploymentInfo.NormalizeEmploymentDate(stored);

        /// <summary>
        /// تاریخ استخدام ذخیره‌شده را برای نمایش/ویرایش به رشته شمسی yyyy/MM/dd برمی‌گرداند.
        /// </summary>
        public static string? EmploymentDateToPersianString(DateTime? stored)
        {
            if (!stored.HasValue)
                return null;

            return ConvertToPersianDate(NormalizeEmploymentDate(stored.Value));
        }

        /// <summary>
        /// تبدیل رشته تاریخ شمسی به تاریخ میلادی (نیمه‌شب، <see cref="DateTimeKind.Unspecified"/>).
        /// ورودی‌های مجاز مثلاً: 1405/06/03 ، ۱۴۰۵-۰۶-۰۳ ، 1405.6.3
        /// اگر روز شمسی واردشده برای ماه‌های نیمه دوم سال (۳۰ روزه) یا اسفند (۲۹ روزه، یا ۳۰ در سال کبیسه)
        /// بیشتر از تعداد روزهای آن ماه باشد (مثلاً ۳۱ برای آبان)، مقدار روز به آخرین روز معتبر همان ماه کلمپ می‌شود.
        /// </summary>
        public static DateTime ConvertToGregorianDate(string persianDate)
            => ConvertToGregorianDate(persianDate, clampDayToMonthEnd: true);

        /// <summary>
        /// تبدیل رشته تاریخ شمسی به تاریخ میلادی با امکان تعیین کلمپ روز به انتهای ماه.
        /// </summary>
        public static DateTime ConvertToGregorianDate(string persianDate, bool clampDayToMonthEnd)
        {
            if (string.IsNullOrWhiteSpace(persianDate))
                throw new ArgumentException("تاریخ وارد شده نامعتبر است.", nameof(persianDate));

            var normalized = NormalizePersianDateInput(persianDate);
            var parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length != 3)
                throw new FormatException($"فرمت تاریخ وارد شده صحیح نیست (مقدار: '{persianDate}'). فرمت مورد انتظار: yyyy/MM/dd");

            if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var year) ||
                !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var month) ||
                !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var day))
            {
                throw new FormatException($"اجزای تاریخ شمسی قابل عدد شدن نیستند (مقدار: '{persianDate}').");
            }

            day = ValidateAndNormalizePersianDateParts(year, month, day, persianDate, clampDayToMonthEnd);

            try
            {
                var result = PersianCalendar.ToDateTime(year, month, day, 0, 0, 0, 0);
                return DateTime.SpecifyKind(result.Date, DateTimeKind.Unspecified);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(persianDate),
                    $"تاریخ شمسی خارج از محدوده مجاز است (مقدار: '{persianDate}'). {ex.Message}");
            }
        }

        /// <summary>
        /// تعداد روزهای یک ماه شمسی در سال مشخص (نیمه اول ۳۱، نیمه دوم ۳۰، اسفند ۲۹ یا ۳۰).
        /// </summary>
        public static int GetDaysInPersianMonth(int year, int month)
        {
            if (month < 1 || month > 12)
                throw new ArgumentOutOfRangeException(nameof(month), "ماه باید بین ۱ تا ۱۲ باشد.");

            return month <= 6 ? 31
                : month <= 11 ? 30
                : (PersianCalendar.IsLeapYear(year) ? 30 : 29);
        }

        /// <summary>
        /// اگر رشته تاریخ شمسی دارای روزی بیشتر از روزهای آن ماه باشد (مثلاً 1405/08/31)،
        /// آن را به آخرین روز معتبر ماه نرمال می‌کند (مثلاً 1405/08/30).
        /// </summary>
        public static string NormalizePersianDateStringToMonthEnd(string persianDate)
        {
            var g = ConvertToGregorianDate(persianDate, clampDayToMonthEnd: true);
            return ConvertToPersianDate(g);
        }

        /// <summary>
        /// تبدیل تاریخ میلادی به رشته شمسی با فرمت yyyy/MM/dd
        /// </summary>
        public static string ConvertToPersianDate(DateTime date)
        {
            var d = date.Date;
            var year = PersianCalendar.GetYear(d);
            var month = PersianCalendar.GetMonth(d);
            var day = PersianCalendar.GetDayOfMonth(d);
            return $"{year:0000}/{month:00}/{day:00}";
        }

        /// <summary>
        /// نرمال‌سازی ورودی: ارقام فارسی/عربی → لاتین، جداکننده‌ها → '/'، حذف فاصله و نویز رایج.
        /// </summary>
        internal static string NormalizePersianDateInput(string input)
        {
            var sb = new StringBuilder(input.Trim().Length);
            foreach (var ch in input.Trim())
            {
                if (char.IsWhiteSpace(ch))
                    continue;

                sb.Append(ch switch
                {
                    // ارقام فارسی
                    '۰' => '0',
                    '۱' => '1',
                    '۲' => '2',
                    '۳' => '3',
                    '۴' => '4',
                    '۵' => '5',
                    '۶' => '6',
                    '۷' => '7',
                    '۸' => '8',
                    '۹' => '9',
                    // ارقام عربی-هندی
                    '٠' => '0',
                    '١' => '1',
                    '٢' => '2',
                    '٣' => '3',
                    '٤' => '4',
                    '٥' => '5',
                    '٦' => '6',
                    '٧' => '7',
                    '٨' => '8',
                    '٩' => '9',
                    // جداکننده‌های رایج
                    '-' or '.' or '\\' or '٫' or '،' or '/' => '/',
                    _ => ch
                });
            }

            // حذف اسلش‌های تکراری
            var normalized = sb.ToString();
            while (normalized.Contains("//", StringComparison.Ordinal))
                normalized = normalized.Replace("//", "/", StringComparison.Ordinal);

            return normalized.Trim('/');
        }

        private static int ValidateAndNormalizePersianDateParts(
            int year,
            int month,
            int day,
            string original,
            bool clampDayToMonthEnd)
        {
            if (year < 1200 || year > 1500)
                throw new ArgumentOutOfRangeException(nameof(original), $"سال شمسی نامعتبر است ({year}). مقدار خام: '{original}'");

            if (month < 1 || month > 12)
                throw new ArgumentOutOfRangeException(nameof(original), $"ماه شمسی نامعتبر است ({month}). مقدار خام: '{original}'");

            var maxDay = month <= 6 ? 31
                : month <= 11 ? 30
                : (PersianCalendar.IsLeapYear(year) ? 30 : 29);

            if (day < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(original),
                    $"روز شمسی نامعتبر است ({day}) برای {year}/{month:00}. مقدار خام: '{original}'");
            }

            if (day > maxDay)
            {
                if (clampDayToMonthEnd && day <= 31)
                {
                    // در نیمه دوم سال ماه‌ها ۳۰ روزه هستند و اسفند ۲۹ روزه (یا ۳۰ در سال کبیسه).
                    // اگر کلاینت یا کاربر روز ۳۱ را برای ماه ۳۰ روزه ارسال کند، به آخرین روز همان ماه کلمپ می‌شود.
                    return maxDay;
                }

                throw new ArgumentOutOfRangeException(
                    nameof(original),
                    $"روز شمسی نامعتبر است ({day}) برای {year}/{month:00} (حداکثر {maxDay}). مقدار خام: '{original}'");
            }

            return day;
        }
    }
}
