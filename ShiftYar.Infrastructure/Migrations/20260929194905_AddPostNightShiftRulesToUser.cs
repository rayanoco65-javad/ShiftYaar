using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShiftYar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPostNightShiftRulesToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('Users', 'AllowEveningAfterNightShift') IS NULL
    ALTER TABLE [Users] ADD [AllowEveningAfterNightShift] bit NULL;

IF COL_LENGTH('Users', 'AllowNightShiftAfterNightShift') IS NULL
    ALTER TABLE [Users] ADD [AllowNightShiftAfterNightShift] bit NULL;

IF COL_LENGTH('Users', 'ForbidEveningAfterNightShift') IS NOT NULL
    ALTER TABLE [Users] DROP COLUMN [ForbidEveningAfterNightShift];

IF COL_LENGTH('Users', 'ForbidNightShiftAfterNightShift') IS NOT NULL
    ALTER TABLE [Users] DROP COLUMN [ForbidNightShiftAfterNightShift];
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('Users', 'AllowEveningAfterNightShift') IS NOT NULL
    ALTER TABLE [Users] DROP COLUMN [AllowEveningAfterNightShift];

IF COL_LENGTH('Users', 'AllowNightShiftAfterNightShift') IS NOT NULL
    ALTER TABLE [Users] DROP COLUMN [AllowNightShiftAfterNightShift];
");
        }
    }
}
