using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShiftYar.Infrastructure.Persistence.AppDbContext;

#nullable disable

namespace ShiftYar.Infrastructure.Migrations
{
    [DbContext(typeof(ShiftYarDbContext))]
    [Migration("20261017200000_AddHolidayShiftDistributionToDepartmentSettings")]
    public partial class AddHolidayShiftDistributionToDepartmentSettings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('DepartmentSchedulingSettings', 'EnableHolidayShiftDistribution') IS NULL
    ALTER TABLE [DepartmentSchedulingSettings] ADD [EnableHolidayShiftDistribution] bit NULL;
");
            migrationBuilder.Sql(@"
IF COL_LENGTH('DepartmentSchedulingSettings', 'HolidayShiftDistributionType') IS NULL
    ALTER TABLE [DepartmentSchedulingSettings] ADD [HolidayShiftDistributionType] int NULL;
");
            migrationBuilder.Sql(@"
IF COL_LENGTH('DepartmentSchedulingSettings', 'HolidayShiftDistributionWeight') IS NULL
    ALTER TABLE [DepartmentSchedulingSettings] ADD [HolidayShiftDistributionWeight] float NULL;
");
            migrationBuilder.Sql(@"
IF COL_LENGTH('DepartmentSchedulingSettings', 'HolidaySeniorityDistributionSlope') IS NULL
    ALTER TABLE [DepartmentSchedulingSettings] ADD [HolidaySeniorityDistributionSlope] float NULL;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('DepartmentSchedulingSettings', 'EnableHolidayShiftDistribution') IS NOT NULL
    ALTER TABLE [DepartmentSchedulingSettings] DROP COLUMN [EnableHolidayShiftDistribution];
");
            migrationBuilder.Sql(@"
IF COL_LENGTH('DepartmentSchedulingSettings', 'HolidayShiftDistributionType') IS NOT NULL
    ALTER TABLE [DepartmentSchedulingSettings] DROP COLUMN [HolidayShiftDistributionType];
");
            migrationBuilder.Sql(@"
IF COL_LENGTH('DepartmentSchedulingSettings', 'HolidayShiftDistributionWeight') IS NOT NULL
    ALTER TABLE [DepartmentSchedulingSettings] DROP COLUMN [HolidayShiftDistributionWeight];
");
            migrationBuilder.Sql(@"
IF COL_LENGTH('DepartmentSchedulingSettings', 'HolidaySeniorityDistributionSlope') IS NOT NULL
    ALTER TABLE [DepartmentSchedulingSettings] DROP COLUMN [HolidaySeniorityDistributionSlope];
");
        }
    }
}
