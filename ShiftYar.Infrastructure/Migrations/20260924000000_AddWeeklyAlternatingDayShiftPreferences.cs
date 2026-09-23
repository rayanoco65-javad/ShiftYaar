using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShiftYar.Infrastructure.Persistence.AppDbContext;

#nullable disable

namespace ShiftYar.Infrastructure.Migrations
{
    [DbContext(typeof(ShiftYarDbContext))]
    [Migration("20260924000000_AddWeeklyAlternatingDayShiftPreferences")]
    public partial class AddWeeklyAlternatingDayShiftPreferences : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('UserMonthlyDayShiftQuotas', 'IsWeeklyAlternatingActive') IS NULL
    ALTER TABLE [UserMonthlyDayShiftQuotas] ADD [IsWeeklyAlternatingActive] bit NOT NULL CONSTRAINT [DF_UserMonthlyDayShiftQuotas_IsWeeklyAlternatingActive] DEFAULT 0;

IF COL_LENGTH('UserMonthlyDayShiftQuotas', 'FirstWeekShiftLabel') IS NULL
    ALTER TABLE [UserMonthlyDayShiftQuotas] ADD [FirstWeekShiftLabel] int NULL;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('UserMonthlyDayShiftQuotas', 'FirstWeekShiftLabel') IS NOT NULL
    ALTER TABLE [UserMonthlyDayShiftQuotas] DROP COLUMN [FirstWeekShiftLabel];

IF COL_LENGTH('UserMonthlyDayShiftQuotas', 'IsWeeklyAlternatingActive') IS NOT NULL
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[UserMonthlyDayShiftQuotas]') AND [c].[name] = N'IsWeeklyAlternatingActive');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [UserMonthlyDayShiftQuotas] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [UserMonthlyDayShiftQuotas] DROP COLUMN [IsWeeklyAlternatingActive];
END
");
        }
    }
}
