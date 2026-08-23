using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShiftYar.Infrastructure.Persistence.AppDbContext;

#nullable disable

namespace ShiftYar.Infrastructure.Migrations
{
    [DbContext(typeof(ShiftYarDbContext))]
    [Migration("20260823120000_AddAllowNightShiftAfterNightShift")]
    public partial class AddAllowNightShiftAfterNightShift : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('DepartmentSchedulingSettings', 'AllowNightShiftAfterNightShift') IS NULL
    ALTER TABLE [DepartmentSchedulingSettings] ADD [AllowNightShiftAfterNightShift] bit NULL;
");

            migrationBuilder.Sql(@"
UPDATE [DepartmentSchedulingSettings]
SET [AllowNightShiftAfterNightShift] = 0
WHERE [AllowNightShiftAfterNightShift] IS NULL;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('DepartmentSchedulingSettings', 'AllowNightShiftAfterNightShift') IS NOT NULL
    ALTER TABLE [DepartmentSchedulingSettings] DROP COLUMN [AllowNightShiftAfterNightShift];
");
        }
    }
}
