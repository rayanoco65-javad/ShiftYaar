using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShiftYar.Infrastructure.Persistence.AppDbContext;

#nullable disable

namespace ShiftYar.Infrastructure.Migrations
{
    [DbContext(typeof(ShiftYarDbContext))]
    [Migration("20260827120000_AddMorningEveningSeniorityDistribution")]
    public partial class AddMorningEveningSeniorityDistribution : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('DepartmentSchedulingSettings', 'EnableMorningShiftDistributionBySeniority') IS NULL
    ALTER TABLE [DepartmentSchedulingSettings] ADD [EnableMorningShiftDistributionBySeniority] bit NULL;
");
            migrationBuilder.Sql(@"
IF COL_LENGTH('DepartmentSchedulingSettings', 'MorningShiftDistributionType') IS NULL
    ALTER TABLE [DepartmentSchedulingSettings] ADD [MorningShiftDistributionType] int NULL;
");
            migrationBuilder.Sql(@"
IF COL_LENGTH('DepartmentSchedulingSettings', 'MorningShiftDistributionWeight') IS NULL
    ALTER TABLE [DepartmentSchedulingSettings] ADD [MorningShiftDistributionWeight] float NULL;
");
            migrationBuilder.Sql(@"
IF COL_LENGTH('DepartmentSchedulingSettings', 'EnableEveningShiftDistributionBySeniority') IS NULL
    ALTER TABLE [DepartmentSchedulingSettings] ADD [EnableEveningShiftDistributionBySeniority] bit NULL;
");
            migrationBuilder.Sql(@"
IF COL_LENGTH('DepartmentSchedulingSettings', 'EveningShiftDistributionType') IS NULL
    ALTER TABLE [DepartmentSchedulingSettings] ADD [EveningShiftDistributionType] int NULL;
");
            migrationBuilder.Sql(@"
IF COL_LENGTH('DepartmentSchedulingSettings', 'EveningShiftDistributionWeight') IS NULL
    ALTER TABLE [DepartmentSchedulingSettings] ADD [EveningShiftDistributionWeight] float NULL;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('DepartmentSchedulingSettings', 'EnableMorningShiftDistributionBySeniority') IS NOT NULL
    ALTER TABLE [DepartmentSchedulingSettings] DROP COLUMN [EnableMorningShiftDistributionBySeniority];
");
            migrationBuilder.Sql(@"
IF COL_LENGTH('DepartmentSchedulingSettings', 'MorningShiftDistributionType') IS NOT NULL
    ALTER TABLE [DepartmentSchedulingSettings] DROP COLUMN [MorningShiftDistributionType];
");
            migrationBuilder.Sql(@"
IF COL_LENGTH('DepartmentSchedulingSettings', 'MorningShiftDistributionWeight') IS NOT NULL
    ALTER TABLE [DepartmentSchedulingSettings] DROP COLUMN [MorningShiftDistributionWeight];
");
            migrationBuilder.Sql(@"
IF COL_LENGTH('DepartmentSchedulingSettings', 'EnableEveningShiftDistributionBySeniority') IS NOT NULL
    ALTER TABLE [DepartmentSchedulingSettings] DROP COLUMN [EnableEveningShiftDistributionBySeniority];
");
            migrationBuilder.Sql(@"
IF COL_LENGTH('DepartmentSchedulingSettings', 'EveningShiftDistributionType') IS NOT NULL
    ALTER TABLE [DepartmentSchedulingSettings] DROP COLUMN [EveningShiftDistributionType];
");
            migrationBuilder.Sql(@"
IF COL_LENGTH('DepartmentSchedulingSettings', 'EveningShiftDistributionWeight') IS NOT NULL
    ALTER TABLE [DepartmentSchedulingSettings] DROP COLUMN [EveningShiftDistributionWeight];
");
        }
    }
}
