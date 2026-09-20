using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShiftYar.Infrastructure.Persistence.AppDbContext;

#nullable disable

namespace ShiftYar.Infrastructure.Migrations
{
    [DbContext(typeof(ShiftYarDbContext))]
    [Migration("20260828130000_AddOvertimeSeniorityDistributionSlopeToDepartmentSettings")]
    public partial class AddOvertimeSeniorityDistributionSlopeToDepartmentSettings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('DepartmentSchedulingSettings', 'OvertimeSeniorityDistributionSlope') IS NULL
    ALTER TABLE [DepartmentSchedulingSettings] ADD [OvertimeSeniorityDistributionSlope] float NULL;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('DepartmentSchedulingSettings', 'OvertimeSeniorityDistributionSlope') IS NOT NULL
    ALTER TABLE [DepartmentSchedulingSettings] DROP COLUMN [OvertimeSeniorityDistributionSlope];
");
        }
    }
}
