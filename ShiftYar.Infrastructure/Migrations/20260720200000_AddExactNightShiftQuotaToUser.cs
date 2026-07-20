using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShiftYar.Infrastructure.Persistence.AppDbContext;

#nullable disable

namespace ShiftYar.Infrastructure.Migrations
{
    [DbContext(typeof(ShiftYarDbContext))]
    [Migration("20260720200000_AddExactNightShiftQuotaToUser")]
    public partial class AddExactNightShiftQuotaToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('Users', 'ExactNightShiftCount') IS NULL
    ALTER TABLE Users ADD ExactNightShiftCount int NULL;
IF COL_LENGTH('Users', 'ExactHolidayWeekendNightShiftCount') IS NULL
    ALTER TABLE Users ADD ExactHolidayWeekendNightShiftCount int NULL;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('Users', 'ExactNightShiftCount') IS NOT NULL
    ALTER TABLE Users DROP COLUMN ExactNightShiftCount;
IF COL_LENGTH('Users', 'ExactHolidayWeekendNightShiftCount') IS NOT NULL
    ALTER TABLE Users DROP COLUMN ExactHolidayWeekendNightShiftCount;
");
        }
    }
}
