using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShiftYar.Infrastructure.Persistence.AppDbContext;

#nullable disable

namespace ShiftYar.Infrastructure.Migrations
{
    /// <summary>
    /// رکوردهایی که اجزای تاریخ شمسی را داخل datetime میلادی ذخیره کرده‌اند
    /// (مثلاً ۱۳۸۰/۰۱/۰۴ به‌صورت 1380-01-04) به میلادی واقعی تبدیل می‌کند.
    /// </summary>
    [DbContext(typeof(ShiftYarDbContext))]
    [Migration("20260804120000_FixMisstoredPersianEmploymentDates")]
    public partial class FixMisstoredPersianEmploymentDates : Migration
    {
        private const int FirstPersianYear = 1300;
        private const int LastPersianYear = 1450;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var persian = new PersianCalendar();

            // یک UPDATE با ~۵۵هزار ردیف VALUES باعث timeout می‌شود؛ سال‌به‌سال اجرا می‌کنیم.
            for (var year = FirstPersianYear; year <= LastPersianYear; year++)
            {
                // هر batch جدا commit شود؛ یک transaction طولانی روی Users قفل نمی‌گذارد.
                migrationBuilder.Sql(BuildYearBatchSql(persian, year), suppressTransaction: true);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // برگشت‌پذیر نیست؛ دادهٔ خام شمسی پس از تبدیل از دست می‌رود.
        }

        private static string BuildYearBatchSql(PersianCalendar persian, int year)
        {
            var sb = new StringBuilder(16 * 1024);
            sb.AppendLine(@"
UPDATE u
SET u.DateOfEmployment = m.GregorianDate
FROM [Users] u
INNER JOIN (VALUES");

            var first = true;
            for (var month = 1; month <= 12; month++)
            {
                var maxDay = persian.GetDaysInMonth(year, month);
                for (var day = 1; day <= maxDay; day++)
                {
                    var gregorian = persian.ToDateTime(year, month, day, 0, 0, 0, 0).Date;
                    if (!first)
                        sb.AppendLine(",");
                    first = false;
                    sb.Append("    ('")
                        .Append(year.ToString("0000", CultureInfo.InvariantCulture)).Append('-')
                        .Append(month.ToString("00", CultureInfo.InvariantCulture)).Append('-')
                        .Append(day.ToString("00", CultureInfo.InvariantCulture))
                        .Append("', '")
                        .Append(gregorian.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                        .Append("')");
                }
            }

            sb.AppendLine();
            sb.AppendLine(@") AS m(PersianAsGregorian, GregorianDate)
ON CONVERT(date, u.DateOfEmployment) = CONVERT(date, m.PersianAsGregorian)
WHERE YEAR(u.DateOfEmployment) = ").Append(year).Append(';');

            return sb.ToString();
        }
    }
}
