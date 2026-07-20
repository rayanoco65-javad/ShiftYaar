using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShiftYar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHolidayStaffingToShiftRequiredSpecialty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent: ستون‌ها ممکن است قبلاً (دستی یا اجرای ناقص) اضافه شده باشند
            migrationBuilder.Sql(@"
IF COL_LENGTH('ShiftRequiredSpecialties', 'HolidayRequiredMaleCount') IS NULL
BEGIN
    ALTER TABLE ShiftRequiredSpecialties ADD
        HolidayRequiredMaleCount int NULL,
        HolidayRequiredFemaleCount int NULL,
        HolidayRequiredTottalCount int NULL,
        HolidayOnCallMaleCount int NULL,
        HolidayOnCallFemaleCount int NULL,
        HolidayOnCallTottalCount int NULL;
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('ShiftRequiredSpecialties', 'HolidayRequiredMaleCount') IS NOT NULL
    ALTER TABLE ShiftRequiredSpecialties DROP COLUMN HolidayRequiredMaleCount;
IF COL_LENGTH('ShiftRequiredSpecialties', 'HolidayRequiredFemaleCount') IS NOT NULL
    ALTER TABLE ShiftRequiredSpecialties DROP COLUMN HolidayRequiredFemaleCount;
IF COL_LENGTH('ShiftRequiredSpecialties', 'HolidayRequiredTottalCount') IS NOT NULL
    ALTER TABLE ShiftRequiredSpecialties DROP COLUMN HolidayRequiredTottalCount;
IF COL_LENGTH('ShiftRequiredSpecialties', 'HolidayOnCallMaleCount') IS NOT NULL
    ALTER TABLE ShiftRequiredSpecialties DROP COLUMN HolidayOnCallMaleCount;
IF COL_LENGTH('ShiftRequiredSpecialties', 'HolidayOnCallFemaleCount') IS NOT NULL
    ALTER TABLE ShiftRequiredSpecialties DROP COLUMN HolidayOnCallFemaleCount;
IF COL_LENGTH('ShiftRequiredSpecialties', 'HolidayOnCallTottalCount') IS NOT NULL
    ALTER TABLE ShiftRequiredSpecialties DROP COLUMN HolidayOnCallTottalCount;
");
        }
    }
}
