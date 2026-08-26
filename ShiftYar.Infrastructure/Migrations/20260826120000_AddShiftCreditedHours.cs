using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShiftYar.Infrastructure.Persistence.AppDbContext;

#nullable disable

namespace ShiftYar.Infrastructure.Migrations
{
    [DbContext(typeof(ShiftYarDbContext))]
    [Migration("20260826120000_AddShiftCreditedHours")]
    public partial class AddShiftCreditedHours : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('Shifts', 'WeekdayNonProductivityHours') IS NULL
    ALTER TABLE [Shifts] ADD [WeekdayNonProductivityHours] float NULL;
");
            migrationBuilder.Sql(@"
IF COL_LENGTH('Shifts', 'HolidayNonProductivityHours') IS NULL
    ALTER TABLE [Shifts] ADD [HolidayNonProductivityHours] float NULL;
");
            migrationBuilder.Sql(@"
IF COL_LENGTH('Shifts', 'WeekdayProductivityPlanHours') IS NULL
    ALTER TABLE [Shifts] ADD [WeekdayProductivityPlanHours] float NULL;
");
            migrationBuilder.Sql(@"
IF COL_LENGTH('Shifts', 'HolidayProductivityPlanHours') IS NULL
    ALTER TABLE [Shifts] ADD [HolidayProductivityPlanHours] float NULL;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('Shifts', 'WeekdayNonProductivityHours') IS NOT NULL
    ALTER TABLE [Shifts] DROP COLUMN [WeekdayNonProductivityHours];
");
            migrationBuilder.Sql(@"
IF COL_LENGTH('Shifts', 'HolidayNonProductivityHours') IS NOT NULL
    ALTER TABLE [Shifts] DROP COLUMN [HolidayNonProductivityHours];
");
            migrationBuilder.Sql(@"
IF COL_LENGTH('Shifts', 'WeekdayProductivityPlanHours') IS NOT NULL
    ALTER TABLE [Shifts] DROP COLUMN [WeekdayProductivityPlanHours];
");
            migrationBuilder.Sql(@"
IF COL_LENGTH('Shifts', 'HolidayProductivityPlanHours') IS NOT NULL
    ALTER TABLE [Shifts] DROP COLUMN [HolidayProductivityPlanHours];
");
        }
    }
}
