using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShiftYar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MakeDayShiftFallbackNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'dbo.UserMonthlyDayShiftQuotas', N'U') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = N'DF_UserMonthlyDayShiftQuotas_MorningFallback')
        ALTER TABLE [UserMonthlyDayShiftQuotas] DROP CONSTRAINT [DF_UserMonthlyDayShiftQuotas_MorningFallback];
    IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = N'DF_UserMonthlyDayShiftQuotas_MorningHolidayFallback')
        ALTER TABLE [UserMonthlyDayShiftQuotas] DROP CONSTRAINT [DF_UserMonthlyDayShiftQuotas_MorningHolidayFallback];
    IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = N'DF_UserMonthlyDayShiftQuotas_EveningFallback')
        ALTER TABLE [UserMonthlyDayShiftQuotas] DROP CONSTRAINT [DF_UserMonthlyDayShiftQuotas_EveningFallback];
    IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = N'DF_UserMonthlyDayShiftQuotas_EveningHolidayFallback')
        ALTER TABLE [UserMonthlyDayShiftQuotas] DROP CONSTRAINT [DF_UserMonthlyDayShiftQuotas_EveningHolidayFallback];

    ALTER TABLE [UserMonthlyDayShiftQuotas] ALTER COLUMN [MorningFallbackParticipation] bit NULL;
    ALTER TABLE [UserMonthlyDayShiftQuotas] ALTER COLUMN [MorningHolidayFallbackParticipation] bit NULL;
    ALTER TABLE [UserMonthlyDayShiftQuotas] ALTER COLUMN [EveningFallbackParticipation] bit NULL;
    ALTER TABLE [UserMonthlyDayShiftQuotas] ALTER COLUMN [EveningHolidayFallbackParticipation] bit NULL;
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'dbo.UserMonthlyDayShiftQuotas', N'U') IS NOT NULL
BEGIN
    UPDATE [UserMonthlyDayShiftQuotas] SET [MorningFallbackParticipation] = 0 WHERE [MorningFallbackParticipation] IS NULL;
    UPDATE [UserMonthlyDayShiftQuotas] SET [MorningHolidayFallbackParticipation] = 0 WHERE [MorningHolidayFallbackParticipation] IS NULL;
    UPDATE [UserMonthlyDayShiftQuotas] SET [EveningFallbackParticipation] = 0 WHERE [EveningFallbackParticipation] IS NULL;
    UPDATE [UserMonthlyDayShiftQuotas] SET [EveningHolidayFallbackParticipation] = 0 WHERE [EveningHolidayFallbackParticipation] IS NULL;

    ALTER TABLE [UserMonthlyDayShiftQuotas] ALTER COLUMN [MorningFallbackParticipation] bit NOT NULL;
    ALTER TABLE [UserMonthlyDayShiftQuotas] ALTER COLUMN [MorningHolidayFallbackParticipation] bit NOT NULL;
    ALTER TABLE [UserMonthlyDayShiftQuotas] ALTER COLUMN [EveningFallbackParticipation] bit NOT NULL;
    ALTER TABLE [UserMonthlyDayShiftQuotas] ALTER COLUMN [EveningHolidayFallbackParticipation] bit NOT NULL;

    ALTER TABLE [UserMonthlyDayShiftQuotas] ADD CONSTRAINT [DF_UserMonthlyDayShiftQuotas_MorningFallback] DEFAULT 0 FOR [MorningFallbackParticipation];
    ALTER TABLE [UserMonthlyDayShiftQuotas] ADD CONSTRAINT [DF_UserMonthlyDayShiftQuotas_MorningHolidayFallback] DEFAULT 0 FOR [MorningHolidayFallbackParticipation];
    ALTER TABLE [UserMonthlyDayShiftQuotas] ADD CONSTRAINT [DF_UserMonthlyDayShiftQuotas_EveningFallback] DEFAULT 0 FOR [EveningFallbackParticipation];
    ALTER TABLE [UserMonthlyDayShiftQuotas] ADD CONSTRAINT [DF_UserMonthlyDayShiftQuotas_EveningHolidayFallback] DEFAULT 0 FOR [EveningHolidayFallbackParticipation];
END
");
        }
    }
}
