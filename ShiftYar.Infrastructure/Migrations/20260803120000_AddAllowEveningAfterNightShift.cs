using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShiftYar.Infrastructure.Persistence.AppDbContext;

#nullable disable

namespace ShiftYar.Infrastructure.Migrations
{
    [DbContext(typeof(ShiftYarDbContext))]
    [Migration("20260803120000_AddAllowEveningAfterNightShift")]
    public partial class AddAllowEveningAfterNightShift : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('DepartmentSchedulingSettings', 'AllowEveningAfterNightShift') IS NULL
    ALTER TABLE [DepartmentSchedulingSettings] ADD [AllowEveningAfterNightShift] bit NULL;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('DepartmentSchedulingSettings', 'AllowEveningAfterNightShift') IS NOT NULL
    ALTER TABLE [DepartmentSchedulingSettings] DROP COLUMN [AllowEveningAfterNightShift];
");
        }
    }
}
