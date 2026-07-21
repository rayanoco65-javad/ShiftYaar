using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShiftYar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserMonthlyNightQuota : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'dbo.UserMonthlyNightQuotas', N'U') IS NULL
BEGIN
    CREATE TABLE [UserMonthlyNightQuotas] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [PersianYear] int NOT NULL,
        [PersianMonth] int NOT NULL,
        [ExactNightShiftCount] int NULL,
        [ExactHolidayWeekendNightShiftCount] int NULL,
        [CreateDate] datetime2 NULL,
        [UpdateDate] datetime2 NULL,
        [TheUserId] int NULL,
        CONSTRAINT [PK_UserMonthlyNightQuotas] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserMonthlyNightQuotas_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );

    CREATE UNIQUE INDEX [IX_UserMonthlyNightQuotas_UserId_PersianYear_PersianMonth]
        ON [UserMonthlyNightQuotas] ([UserId], [PersianYear], [PersianMonth]);
END

IF COL_LENGTH('Users', 'ExactNightShiftCount') IS NOT NULL
    ALTER TABLE [Users] DROP COLUMN [ExactNightShiftCount];
IF COL_LENGTH('Users', 'ExactHolidayWeekendNightShiftCount') IS NOT NULL
    ALTER TABLE [Users] DROP COLUMN [ExactHolidayWeekendNightShiftCount];
");

            migrationBuilder.AlterColumn<decimal>(
                name: "HardshipPercent",
                table: "Users",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(5,2)",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "HardshipPercent",
                table: "Users",
                type: "decimal(5,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.Sql(@"
IF COL_LENGTH('Users', 'ExactNightShiftCount') IS NULL
    ALTER TABLE [Users] ADD [ExactNightShiftCount] int NULL;
IF COL_LENGTH('Users', 'ExactHolidayWeekendNightShiftCount') IS NULL
    ALTER TABLE [Users] ADD [ExactHolidayWeekendNightShiftCount] int NULL;

IF OBJECT_ID(N'dbo.UserMonthlyNightQuotas', N'U') IS NOT NULL
    DROP TABLE [UserMonthlyNightQuotas];
");
        }
    }
}
