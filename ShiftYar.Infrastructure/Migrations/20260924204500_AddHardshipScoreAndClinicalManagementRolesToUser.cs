using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShiftYar.Infrastructure.Persistence.AppDbContext;

#nullable disable

namespace ShiftYar.Infrastructure.Migrations
{
    [DbContext(typeof(ShiftYarDbContext))]
    [Migration("20260924204500_AddHardshipScoreAndClinicalManagementRolesToUser")]
    public partial class AddHardshipScoreAndClinicalManagementRolesToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
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
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('Users', 'HardshipScore') IS NOT NULL
    ALTER TABLE [Users] DROP COLUMN [HardshipScore];

IF COL_LENGTH('Users', 'Position') IS NOT NULL
    ALTER TABLE [Users] DROP COLUMN [Position];

IF COL_LENGTH('Users', 'JobTitle') IS NOT NULL
    ALTER TABLE [Users] DROP COLUMN [JobTitle];

IF COL_LENGTH('Users', 'IsSupervisor') IS NOT NULL
    ALTER TABLE [Users] DROP COLUMN [IsSupervisor];

IF COL_LENGTH('Users', 'IsHeadNurse') IS NOT NULL
    ALTER TABLE [Users] DROP COLUMN [IsHeadNurse];
");
        }
    }
}
