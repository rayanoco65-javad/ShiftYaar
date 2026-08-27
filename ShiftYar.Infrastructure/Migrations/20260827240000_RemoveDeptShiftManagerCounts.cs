using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShiftYar.Infrastructure.Persistence.AppDbContext;

#nullable disable

namespace ShiftYar.Infrastructure.Migrations
{
    /// <summary>
    /// حذف فیلدهای تعداد/الزام مسئول از تنظیمات دپارتمان؛ منبع فقط Shift است.
    /// ShiftManagerRequirementWeight روی دپارتمان باقی می‌ماند.
    /// </summary>
    [DbContext(typeof(ShiftYarDbContext))]
    [Migration("20260827240000_RemoveDeptShiftManagerCounts")]
    public partial class RemoveDeptShiftManagerCounts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            DropColumnIfExists(migrationBuilder, "RequireManagerForMorningShift");
            DropColumnIfExists(migrationBuilder, "RequireManagerForEveningShift");
            DropColumnIfExists(migrationBuilder, "RequireManagerForNightShift");
            DropColumnIfExists(migrationBuilder, "MorningShiftManagerRequiredCount");
            DropColumnIfExists(migrationBuilder, "MorningShiftManagerMinLevel1Count");
            DropColumnIfExists(migrationBuilder, "EveningShiftManagerRequiredCount");
            DropColumnIfExists(migrationBuilder, "EveningShiftManagerMinLevel1Count");
            DropColumnIfExists(migrationBuilder, "NightShiftManagerRequiredCount");
            DropColumnIfExists(migrationBuilder, "NightShiftManagerMinLevel1Count");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('DepartmentSchedulingSettings', 'RequireManagerForMorningShift') IS NULL
    ALTER TABLE [DepartmentSchedulingSettings] ADD [RequireManagerForMorningShift] bit NULL;
IF COL_LENGTH('DepartmentSchedulingSettings', 'RequireManagerForEveningShift') IS NULL
    ALTER TABLE [DepartmentSchedulingSettings] ADD [RequireManagerForEveningShift] bit NULL;
IF COL_LENGTH('DepartmentSchedulingSettings', 'RequireManagerForNightShift') IS NULL
    ALTER TABLE [DepartmentSchedulingSettings] ADD [RequireManagerForNightShift] bit NULL;
IF COL_LENGTH('DepartmentSchedulingSettings', 'MorningShiftManagerRequiredCount') IS NULL
    ALTER TABLE [DepartmentSchedulingSettings] ADD [MorningShiftManagerRequiredCount] int NULL;
IF COL_LENGTH('DepartmentSchedulingSettings', 'MorningShiftManagerMinLevel1Count') IS NULL
    ALTER TABLE [DepartmentSchedulingSettings] ADD [MorningShiftManagerMinLevel1Count] int NULL;
IF COL_LENGTH('DepartmentSchedulingSettings', 'EveningShiftManagerRequiredCount') IS NULL
    ALTER TABLE [DepartmentSchedulingSettings] ADD [EveningShiftManagerRequiredCount] int NULL;
IF COL_LENGTH('DepartmentSchedulingSettings', 'EveningShiftManagerMinLevel1Count') IS NULL
    ALTER TABLE [DepartmentSchedulingSettings] ADD [EveningShiftManagerMinLevel1Count] int NULL;
IF COL_LENGTH('DepartmentSchedulingSettings', 'NightShiftManagerRequiredCount') IS NULL
    ALTER TABLE [DepartmentSchedulingSettings] ADD [NightShiftManagerRequiredCount] int NULL;
IF COL_LENGTH('DepartmentSchedulingSettings', 'NightShiftManagerMinLevel1Count') IS NULL
    ALTER TABLE [DepartmentSchedulingSettings] ADD [NightShiftManagerMinLevel1Count] int NULL;
");
        }

        private static void DropColumnIfExists(MigrationBuilder migrationBuilder, string columnName)
        {
            migrationBuilder.Sql($@"
IF COL_LENGTH('DepartmentSchedulingSettings', '{columnName}') IS NOT NULL
    ALTER TABLE [DepartmentSchedulingSettings] DROP COLUMN [{columnName}];
");
        }
    }
}
