using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShiftYar.Infrastructure.Persistence.AppDbContext;

#nullable disable

namespace ShiftYar.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ShiftYarDbContext))]
    [Migration("20260821200000_AddUserMonthlyDayShiftQuota")]
    public partial class AddUserMonthlyDayShiftQuota : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'dbo.UserMonthlyDayShiftQuotas', N'U') IS NULL
BEGIN
    CREATE TABLE [UserMonthlyDayShiftQuotas] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [PersianYear] int NOT NULL,
        [PersianMonth] int NOT NULL,
        [ExactMorningShiftCount] int NULL,
        [MorningFallbackParticipation] bit NOT NULL CONSTRAINT [DF_UserMonthlyDayShiftQuotas_MorningFallback] DEFAULT 0,
        [ExactHolidayMorningShiftCount] int NULL,
        [MorningHolidayFallbackParticipation] bit NOT NULL CONSTRAINT [DF_UserMonthlyDayShiftQuotas_MorningHolidayFallback] DEFAULT 0,
        [ExactEveningShiftCount] int NULL,
        [EveningFallbackParticipation] bit NOT NULL CONSTRAINT [DF_UserMonthlyDayShiftQuotas_EveningFallback] DEFAULT 0,
        [ExactHolidayEveningShiftCount] int NULL,
        [EveningHolidayFallbackParticipation] bit NOT NULL CONSTRAINT [DF_UserMonthlyDayShiftQuotas_EveningHolidayFallback] DEFAULT 0,
        [CreateDate] datetime2 NULL,
        [UpdateDate] datetime2 NULL,
        [TheUserId] int NULL,
        CONSTRAINT [PK_UserMonthlyDayShiftQuotas] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserMonthlyDayShiftQuotas_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );

    CREATE UNIQUE INDEX [IX_UserMonthlyDayShiftQuotas_UserId_PersianYear_PersianMonth]
        ON [UserMonthlyDayShiftQuotas] ([UserId], [PersianYear], [PersianMonth]);
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'dbo.UserMonthlyDayShiftQuotas', N'U') IS NOT NULL
    DROP TABLE [UserMonthlyDayShiftQuotas];
");
        }
    }
}
