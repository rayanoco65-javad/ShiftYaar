using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShiftYar.Infrastructure.Persistence.AppDbContext;

#nullable disable

namespace ShiftYar.Infrastructure.Migrations
{
    [DbContext(typeof(ShiftYarDbContext))]
    [Migration("20260828120000_AddOvertimeSeniorityDistributionToDepartmentSettings")]
    public partial class AddOvertimeSeniorityDistributionToDepartmentSettings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('DepartmentSchedulingSettings', 'EnableOvertimeDistributionBySeniority') IS NULL
    ALTER TABLE [DepartmentSchedulingSettings] ADD [EnableOvertimeDistributionBySeniority] bit NULL;
");
            migrationBuilder.Sql(@"
IF COL_LENGTH('DepartmentSchedulingSettings', 'OvertimePreferenceType') IS NULL
    ALTER TABLE [DepartmentSchedulingSettings] ADD [OvertimePreferenceType] int NULL;
");
            migrationBuilder.Sql(@"
IF COL_LENGTH('DepartmentSchedulingSettings', 'OvertimeDistributionWeight') IS NULL
    ALTER TABLE [DepartmentSchedulingSettings] ADD [OvertimeDistributionWeight] float NULL;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('DepartmentSchedulingSettings', 'EnableOvertimeDistributionBySeniority') IS NOT NULL
    ALTER TABLE [DepartmentSchedulingSettings] DROP COLUMN [EnableOvertimeDistributionBySeniority];
");
            migrationBuilder.Sql(@"
IF COL_LENGTH('DepartmentSchedulingSettings', 'OvertimePreferenceType') IS NOT NULL
    ALTER TABLE [DepartmentSchedulingSettings] DROP COLUMN [OvertimePreferenceType];
");
            migrationBuilder.Sql(@"
IF COL_LENGTH('DepartmentSchedulingSettings', 'OvertimeDistributionWeight') IS NOT NULL
    ALTER TABLE [DepartmentSchedulingSettings] DROP COLUMN [OvertimeDistributionWeight];
");
        }
    }
}
