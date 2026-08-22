using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShiftYar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SyncPendingModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'dbo.UserMonthlyComboShiftQuotas', N'U') IS NULL
BEGIN
    CREATE TABLE [UserMonthlyComboShiftQuotas] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [PersianYear] int NOT NULL,
        [PersianMonth] int NOT NULL,
        [MorningEveningShiftCount] int NULL,
        [MorningEveningFallbackParticipation] bit NULL,
        [MorningEveningHolidayCount] int NULL,
        [MorningEveningHolidayFallback] bit NULL,
        [MorningNightShiftCount] int NULL,
        [MorningNightFallbackParticipation] bit NULL,
        [MorningNightHolidayCount] int NULL,
        [MorningNightHolidayFallback] bit NULL,
        [CreateDate] datetime2 NULL,
        [UpdateDate] datetime2 NULL,
        [TheUserId] int NULL,
        CONSTRAINT [PK_UserMonthlyComboShiftQuotas] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserMonthlyComboShiftQuotas_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
    CREATE UNIQUE INDEX [IX_UserMonthlyComboShiftQuotas_UserId_PersianYear_PersianMonth]
        ON [UserMonthlyComboShiftQuotas] ([UserId], [PersianYear], [PersianMonth]);
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'dbo.UserMonthlyComboShiftQuotas', N'U') IS NOT NULL
    DROP TABLE [UserMonthlyComboShiftQuotas];
");
        }
    }
}
