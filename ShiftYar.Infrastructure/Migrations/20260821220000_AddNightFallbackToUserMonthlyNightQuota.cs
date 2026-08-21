using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShiftYar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNightFallbackToUserMonthlyNightQuota : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'dbo.UserMonthlyNightQuotas', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('UserMonthlyNightQuotas', 'NightFallbackParticipation') IS NULL
        ALTER TABLE [UserMonthlyNightQuotas] ADD [NightFallbackParticipation] bit NULL;

    IF COL_LENGTH('UserMonthlyNightQuotas', 'HolidayWeekendNightFallbackParticipation') IS NULL
        ALTER TABLE [UserMonthlyNightQuotas] ADD [HolidayWeekendNightFallbackParticipation] bit NULL;
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('UserMonthlyNightQuotas', 'NightFallbackParticipation') IS NOT NULL
    ALTER TABLE [UserMonthlyNightQuotas] DROP COLUMN [NightFallbackParticipation];
IF COL_LENGTH('UserMonthlyNightQuotas', 'HolidayWeekendNightFallbackParticipation') IS NOT NULL
    ALTER TABLE [UserMonthlyNightQuotas] DROP COLUMN [HolidayWeekendNightFallbackParticipation];
");
        }
    }
}
