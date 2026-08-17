using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShiftYar.Infrastructure.Persistence.AppDbContext;

#nullable disable

namespace ShiftYar.Infrastructure.Migrations
{
    [DbContext(typeof(ShiftYarDbContext))]
    [Migration("20260817120000_AddMaxProductivityRequiredHoursToUser")]
    public partial class AddMaxProductivityRequiredHoursToUser : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('Users', 'MaxProductivityRequiredHours') IS NULL
    ALTER TABLE [Users] ADD [MaxProductivityRequiredHours] decimal(18,2) NULL;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('Users', 'MaxProductivityRequiredHours') IS NOT NULL
    ALTER TABLE [Users] DROP COLUMN [MaxProductivityRequiredHours];
");
        }
    }
}
