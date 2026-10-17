using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShiftYar.Infrastructure.Persistence.AppDbContext;

#nullable disable

namespace ShiftYar.Infrastructure.Migrations
{
    [DbContext(typeof(ShiftYarDbContext))]
    [Migration("20261017210000_AddExactHolidayShiftCountToUserMonthlyQuotas")]
    public partial class AddExactHolidayShiftCountToUserMonthlyQuotas : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('UserMonthlyDayShiftQuotas', 'ExactHolidayShiftCount') IS NULL
    ALTER TABLE [UserMonthlyDayShiftQuotas] ADD [ExactHolidayShiftCount] int NULL;
");
            migrationBuilder.Sql(@"
IF COL_LENGTH('UserMonthlyNightQuotas', 'ExactHolidayShiftCount') IS NULL
    ALTER TABLE [UserMonthlyNightQuotas] ADD [ExactHolidayShiftCount] int NULL;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('UserMonthlyDayShiftQuotas', 'ExactHolidayShiftCount') IS NOT NULL
    ALTER TABLE [UserMonthlyDayShiftQuotas] DROP COLUMN [ExactHolidayShiftCount];
");
            migrationBuilder.Sql(@"
IF COL_LENGTH('UserMonthlyNightQuotas', 'ExactHolidayShiftCount') IS NOT NULL
    ALTER TABLE [UserMonthlyNightQuotas] DROP COLUMN [ExactHolidayShiftCount];
");
        }
    }
}
