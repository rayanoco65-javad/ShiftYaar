using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShiftYar.Infrastructure.Persistence.AppDbContext;

#nullable disable

namespace ShiftYar.Infrastructure.Migrations
{
    [DbContext(typeof(ShiftYarDbContext))]
    [Migration("20260827220000_AddShiftManagerLevels")]
    public partial class AddShiftManagerLevels : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('Users', 'ShiftManagerLevel') IS NULL
    ALTER TABLE [Users] ADD [ShiftManagerLevel] tinyint NULL;
");
            migrationBuilder.Sql(@"
UPDATE [Users]
SET [ShiftManagerLevel] = 1
WHERE [CanBeShiftManager] = 1 AND [ShiftManagerLevel] IS NULL;
");

            migrationBuilder.Sql(@"
IF COL_LENGTH('DepartmentSchedulingSettings', 'RequireManagerForMorningShift') IS NULL
    ALTER TABLE [DepartmentSchedulingSettings] ADD [RequireManagerForMorningShift] bit NULL;
");
            migrationBuilder.Sql(@"
IF COL_LENGTH('DepartmentSchedulingSettings', 'MorningShiftManagerRequiredCount') IS NULL
    ALTER TABLE [DepartmentSchedulingSettings] ADD [MorningShiftManagerRequiredCount] int NULL;
");
            migrationBuilder.Sql(@"
IF COL_LENGTH('DepartmentSchedulingSettings', 'MorningShiftManagerMinLevel1Count') IS NULL
    ALTER TABLE [DepartmentSchedulingSettings] ADD [MorningShiftManagerMinLevel1Count] int NULL;
");
            migrationBuilder.Sql(@"
IF COL_LENGTH('DepartmentSchedulingSettings', 'EveningShiftManagerRequiredCount') IS NULL
    ALTER TABLE [DepartmentSchedulingSettings] ADD [EveningShiftManagerRequiredCount] int NULL;
");
            migrationBuilder.Sql(@"
IF COL_LENGTH('DepartmentSchedulingSettings', 'EveningShiftManagerMinLevel1Count') IS NULL
    ALTER TABLE [DepartmentSchedulingSettings] ADD [EveningShiftManagerMinLevel1Count] int NULL;
");
            migrationBuilder.Sql(@"
IF COL_LENGTH('DepartmentSchedulingSettings', 'NightShiftManagerRequiredCount') IS NULL
    ALTER TABLE [DepartmentSchedulingSettings] ADD [NightShiftManagerRequiredCount] int NULL;
");
            migrationBuilder.Sql(@"
IF COL_LENGTH('DepartmentSchedulingSettings', 'NightShiftManagerMinLevel1Count') IS NULL
    ALTER TABLE [DepartmentSchedulingSettings] ADD [NightShiftManagerMinLevel1Count] int NULL;
");

            // سازگاری: boolهای قبلی → count
            migrationBuilder.Sql(@"
UPDATE [DepartmentSchedulingSettings]
SET [EveningShiftManagerRequiredCount] = CASE WHEN [RequireManagerForEveningShift] = 1 THEN 1 ELSE 0 END
WHERE [EveningShiftManagerRequiredCount] IS NULL;
");
            migrationBuilder.Sql(@"
UPDATE [DepartmentSchedulingSettings]
SET [NightShiftManagerRequiredCount] = CASE WHEN [RequireManagerForNightShift] = 1 THEN 1 ELSE 0 END
WHERE [NightShiftManagerRequiredCount] IS NULL;
");
            migrationBuilder.Sql(@"
UPDATE [DepartmentSchedulingSettings]
SET [MorningShiftManagerRequiredCount] = 0
WHERE [MorningShiftManagerRequiredCount] IS NULL;
");
            migrationBuilder.Sql(@"
UPDATE [DepartmentSchedulingSettings]
SET [MorningShiftManagerMinLevel1Count] = 0,
    [EveningShiftManagerMinLevel1Count] = 0,
    [NightShiftManagerMinLevel1Count] = 0
WHERE [MorningShiftManagerMinLevel1Count] IS NULL
   OR [EveningShiftManagerMinLevel1Count] IS NULL
   OR [NightShiftManagerMinLevel1Count] IS NULL;
");
            migrationBuilder.Sql(@"
UPDATE [DepartmentSchedulingSettings]
SET [RequireManagerForMorningShift] = CASE WHEN ISNULL([MorningShiftManagerRequiredCount], 0) > 0 THEN 1 ELSE 0 END
WHERE [RequireManagerForMorningShift] IS NULL;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('Users', 'ShiftManagerLevel') IS NOT NULL
    ALTER TABLE [Users] DROP COLUMN [ShiftManagerLevel];
");
            migrationBuilder.Sql(@"
IF COL_LENGTH('DepartmentSchedulingSettings', 'RequireManagerForMorningShift') IS NOT NULL
    ALTER TABLE [DepartmentSchedulingSettings] DROP COLUMN [RequireManagerForMorningShift];
");
            migrationBuilder.Sql(@"
IF COL_LENGTH('DepartmentSchedulingSettings', 'MorningShiftManagerRequiredCount') IS NOT NULL
    ALTER TABLE [DepartmentSchedulingSettings] DROP COLUMN [MorningShiftManagerRequiredCount];
");
            migrationBuilder.Sql(@"
IF COL_LENGTH('DepartmentSchedulingSettings', 'MorningShiftManagerMinLevel1Count') IS NOT NULL
    ALTER TABLE [DepartmentSchedulingSettings] DROP COLUMN [MorningShiftManagerMinLevel1Count];
");
            migrationBuilder.Sql(@"
IF COL_LENGTH('DepartmentSchedulingSettings', 'EveningShiftManagerRequiredCount') IS NOT NULL
    ALTER TABLE [DepartmentSchedulingSettings] DROP COLUMN [EveningShiftManagerRequiredCount];
");
            migrationBuilder.Sql(@"
IF COL_LENGTH('DepartmentSchedulingSettings', 'EveningShiftManagerMinLevel1Count') IS NOT NULL
    ALTER TABLE [DepartmentSchedulingSettings] DROP COLUMN [EveningShiftManagerMinLevel1Count];
");
            migrationBuilder.Sql(@"
IF COL_LENGTH('DepartmentSchedulingSettings', 'NightShiftManagerRequiredCount') IS NOT NULL
    ALTER TABLE [DepartmentSchedulingSettings] DROP COLUMN [NightShiftManagerRequiredCount];
");
            migrationBuilder.Sql(@"
IF COL_LENGTH('DepartmentSchedulingSettings', 'NightShiftManagerMinLevel1Count') IS NOT NULL
    ALTER TABLE [DepartmentSchedulingSettings] DROP COLUMN [NightShiftManagerMinLevel1Count];
");
        }
    }
}
