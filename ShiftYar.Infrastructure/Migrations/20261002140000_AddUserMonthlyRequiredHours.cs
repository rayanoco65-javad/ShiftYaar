using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShiftYar.Infrastructure.Persistence.AppDbContext;

#nullable disable

namespace ShiftYar.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ShiftYarDbContext))]
    [Migration("20261002140000_AddUserMonthlyRequiredHours")]
    public partial class AddUserMonthlyRequiredHours : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'dbo.UserMonthlyRequiredHours', N'U') IS NULL
BEGIN
    CREATE TABLE [UserMonthlyRequiredHours] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [DepartmentId] int NOT NULL,
        [PersianYear] int NOT NULL,
        [PersianMonth] int NOT NULL,
        [CalculatedHours] decimal(18,2) NOT NULL,
        [ApprovedHours] decimal(18,2) NOT NULL,
        [IsManuallyEdited] bit NOT NULL CONSTRAINT [DF_UserMonthlyRequiredHours_IsManuallyEdited] DEFAULT 0,
        [Notes] nvarchar(500) NULL,
        [ConfirmedAt] datetime2 NULL,
        [ConfirmedByUserId] int NULL,
        [CreateDate] datetime2 NULL,
        [UpdateDate] datetime2 NULL,
        [TheUserId] int NULL,
        CONSTRAINT [PK_UserMonthlyRequiredHours] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserMonthlyRequiredHours_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_UserMonthlyRequiredHours_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [Departments] ([Id])
    );

    CREATE UNIQUE INDEX [IX_UserMonthlyRequiredHours_UserId_PersianYear_PersianMonth]
        ON [UserMonthlyRequiredHours] ([UserId], [PersianYear], [PersianMonth]);

    CREATE INDEX [IX_UserMonthlyRequiredHours_DepartmentId_PersianYear_PersianMonth]
        ON [UserMonthlyRequiredHours] ([DepartmentId], [PersianYear], [PersianMonth]);
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'dbo.UserMonthlyRequiredHours', N'U') IS NOT NULL
    DROP TABLE [UserMonthlyRequiredHours];
");
        }
    }
}
