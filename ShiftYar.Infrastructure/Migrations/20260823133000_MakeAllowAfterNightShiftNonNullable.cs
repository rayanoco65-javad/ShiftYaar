using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShiftYar.Infrastructure.Persistence.AppDbContext;

#nullable disable

namespace ShiftYar.Infrastructure.Migrations
{
    [DbContext(typeof(ShiftYarDbContext))]
    [Migration("20260823133000_MakeAllowAfterNightShiftNonNullable")]
    public partial class MakeAllowAfterNightShiftNonNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('DepartmentSchedulingSettings', 'AllowEveningAfterNightShift') IS NOT NULL
BEGIN
    UPDATE [DepartmentSchedulingSettings]
    SET [AllowEveningAfterNightShift] = 0
    WHERE [AllowEveningAfterNightShift] IS NULL;
END
");

            migrationBuilder.Sql(@"
IF COL_LENGTH('DepartmentSchedulingSettings', 'AllowEveningAfterNightShift') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1 FROM sys.default_constraints
        WHERE parent_object_id = OBJECT_ID('DepartmentSchedulingSettings')
          AND name = 'DF_DepartmentSchedulingSettings_AllowEveningAfterNightShift')
BEGIN
    ALTER TABLE [DepartmentSchedulingSettings]
        ADD CONSTRAINT [DF_DepartmentSchedulingSettings_AllowEveningAfterNightShift] DEFAULT (0) FOR [AllowEveningAfterNightShift];
END
");

            migrationBuilder.Sql(@"
IF COL_LENGTH('DepartmentSchedulingSettings', 'AllowEveningAfterNightShift') IS NOT NULL
BEGIN
    ALTER TABLE [DepartmentSchedulingSettings]
        ALTER COLUMN [AllowEveningAfterNightShift] bit NOT NULL;
END
");

            migrationBuilder.Sql(@"
IF COL_LENGTH('DepartmentSchedulingSettings', 'AllowNightShiftAfterNightShift') IS NULL
BEGIN
    ALTER TABLE [DepartmentSchedulingSettings]
        ADD [AllowNightShiftAfterNightShift] bit NOT NULL
        CONSTRAINT [DF_DepartmentSchedulingSettings_AllowNightShiftAfterNightShift] DEFAULT (0);
END
");

            migrationBuilder.Sql(@"
UPDATE [DepartmentSchedulingSettings]
SET [AllowNightShiftAfterNightShift] = 0
WHERE [AllowNightShiftAfterNightShift] IS NULL;
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (
    SELECT 1 FROM sys.default_constraints
    WHERE parent_object_id = OBJECT_ID('DepartmentSchedulingSettings')
      AND name = 'DF_DepartmentSchedulingSettings_AllowNightShiftAfterNightShift')
BEGIN
    ALTER TABLE [DepartmentSchedulingSettings]
        ADD CONSTRAINT [DF_DepartmentSchedulingSettings_AllowNightShiftAfterNightShift] DEFAULT (0) FOR [AllowNightShiftAfterNightShift];
END
");

            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID('DepartmentSchedulingSettings')
      AND name = 'AllowNightShiftAfterNightShift'
      AND is_nullable = 1)
BEGIN
    ALTER TABLE [DepartmentSchedulingSettings]
        ALTER COLUMN [AllowNightShiftAfterNightShift] bit NOT NULL;
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('DepartmentSchedulingSettings', 'AllowEveningAfterNightShift') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_DepartmentSchedulingSettings_AllowEveningAfterNightShift')
        ALTER TABLE [DepartmentSchedulingSettings] DROP CONSTRAINT [DF_DepartmentSchedulingSettings_AllowEveningAfterNightShift];

    ALTER TABLE [DepartmentSchedulingSettings]
        ALTER COLUMN [AllowEveningAfterNightShift] bit NULL;
END
");

            migrationBuilder.Sql(@"
IF COL_LENGTH('DepartmentSchedulingSettings', 'AllowNightShiftAfterNightShift') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_DepartmentSchedulingSettings_AllowNightShiftAfterNightShift')
        ALTER TABLE [DepartmentSchedulingSettings] DROP CONSTRAINT [DF_DepartmentSchedulingSettings_AllowNightShiftAfterNightShift];

    ALTER TABLE [DepartmentSchedulingSettings]
        ALTER COLUMN [AllowNightShiftAfterNightShift] bit NULL;
END
");
        }
    }
}
