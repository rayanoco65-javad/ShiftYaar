using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShiftYar.Infrastructure.Persistence.AppDbContext;

#nullable disable

namespace ShiftYar.Infrastructure.Migrations
{
    [DbContext(typeof(ShiftYarDbContext))]
    [Migration("20260817140000_AddAllowedShiftPermissionsToUser")]
    public partial class AddAllowedShiftPermissionsToUser : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('Users', 'AllowedShiftPermissions') IS NULL
    ALTER TABLE [Users] ADD [AllowedShiftPermissions] int NULL;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('Users', 'AllowedShiftPermissions') IS NOT NULL
    ALTER TABLE [Users] DROP COLUMN [AllowedShiftPermissions];
");
        }
    }
}
