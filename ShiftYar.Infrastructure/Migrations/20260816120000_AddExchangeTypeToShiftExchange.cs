using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShiftYar.Infrastructure.Persistence.AppDbContext;

#nullable disable

namespace ShiftYar.Infrastructure.Migrations
{
    [DbContext(typeof(ShiftYarDbContext))]
    [Migration("20260816120000_AddExchangeTypeToShiftExchange")]
    public partial class AddExchangeTypeToShiftExchange : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // دو batch جدا: SQL Server نام ستون را در compile-time کل batch می‌سنجد.
            migrationBuilder.Sql(@"
IF COL_LENGTH('ShiftExchanges', 'ExchangeType') IS NULL
    ALTER TABLE [ShiftExchanges] ADD [ExchangeType] int NULL;
");

            migrationBuilder.Sql(@"
IF COL_LENGTH('ShiftExchanges', 'ExchangeType') IS NOT NULL
    UPDATE [ShiftExchanges] SET [ExchangeType] = 0 WHERE [ExchangeType] IS NULL;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('ShiftExchanges', 'ExchangeType') IS NOT NULL
    ALTER TABLE [ShiftExchanges] DROP COLUMN [ExchangeType];
");
        }
    }
}
