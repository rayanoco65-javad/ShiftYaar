using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShiftYar.Infrastructure.Persistence.AppDbContext;

#nullable disable

namespace ShiftYar.Infrastructure.Migrations
{
    [DbContext(typeof(ShiftYarDbContext))]
    [Migration("20260720070000_AddProductivityFieldsToUser")]
    public partial class AddProductivityFieldsToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('Users', 'HardshipPercent') IS NULL
    ALTER TABLE Users ADD HardshipPercent decimal(5,2) NULL;
IF COL_LENGTH('Users', 'OvertimeConsent') IS NULL
    ALTER TABLE Users ADD OvertimeConsent bit NULL;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('Users', 'HardshipPercent') IS NOT NULL
    ALTER TABLE Users DROP COLUMN HardshipPercent;
IF COL_LENGTH('Users', 'OvertimeConsent') IS NOT NULL
    ALTER TABLE Users DROP COLUMN OvertimeConsent;
");
        }
    }
}
