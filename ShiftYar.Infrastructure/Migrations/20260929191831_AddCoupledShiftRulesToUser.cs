using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShiftYar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCoupledShiftRulesToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('Users', 'MorningRequiresEvening') IS NULL
    ALTER TABLE [Users] ADD [MorningRequiresEvening] bit NULL;

IF COL_LENGTH('Users', 'MorningRequiresNight') IS NULL
    ALTER TABLE [Users] ADD [MorningRequiresNight] bit NULL;

IF COL_LENGTH('Users', 'EveningRequiresMorning') IS NULL
    ALTER TABLE [Users] ADD [EveningRequiresMorning] bit NULL;

IF COL_LENGTH('Users', 'NightRequiresMorning') IS NULL
    ALTER TABLE [Users] ADD [NightRequiresMorning] bit NULL;

IF COL_LENGTH('Users', 'HardshipScore') IS NULL
    ALTER TABLE [Users] ADD [HardshipScore] decimal(18,2) NULL;

IF COL_LENGTH('Users', 'Position') IS NULL
    ALTER TABLE [Users] ADD [Position] nvarchar(max) NULL;

IF COL_LENGTH('Users', 'JobTitle') IS NULL
    ALTER TABLE [Users] ADD [JobTitle] nvarchar(max) NULL;

IF COL_LENGTH('Users', 'IsSupervisor') IS NULL
    ALTER TABLE [Users] ADD [IsSupervisor] bit NULL;

IF COL_LENGTH('Users', 'IsHeadNurse') IS NULL
    ALTER TABLE [Users] ADD [IsHeadNurse] bit NULL;

IF COL_LENGTH('Users', 'ShiftManagerLevel') IS NULL
    ALTER TABLE [Users] ADD [ShiftManagerLevel] tinyint NULL;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('Users', 'MorningRequiresEvening') IS NOT NULL
    ALTER TABLE [Users] DROP COLUMN [MorningRequiresEvening];

IF COL_LENGTH('Users', 'MorningRequiresNight') IS NOT NULL
    ALTER TABLE [Users] DROP COLUMN [MorningRequiresNight];

IF COL_LENGTH('Users', 'EveningRequiresMorning') IS NOT NULL
    ALTER TABLE [Users] DROP COLUMN [EveningRequiresMorning];

IF COL_LENGTH('Users', 'NightRequiresMorning') IS NOT NULL
    ALTER TABLE [Users] DROP COLUMN [NightRequiresMorning];
");
        }
    }
}
