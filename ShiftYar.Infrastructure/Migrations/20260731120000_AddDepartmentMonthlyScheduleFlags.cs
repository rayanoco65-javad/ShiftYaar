using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShiftYar.Infrastructure.Persistence.AppDbContext;

#nullable disable

namespace ShiftYar.Infrastructure.Migrations
{
    [DbContext(typeof(ShiftYarDbContext))]
    [Migration("20260731120000_AddDepartmentMonthlyScheduleFlags")]
    public partial class AddDepartmentMonthlyScheduleFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('DepartmentSchedulingSettings', 'AllowCurrentMonthScheduling') IS NULL
    ALTER TABLE [DepartmentSchedulingSettings] ADD [AllowCurrentMonthScheduling] bit NULL;

IF COL_LENGTH('DepartmentSchedulingSettings', 'AllowMonthlyRescheduleWithAutoDelete') IS NULL
    ALTER TABLE [DepartmentSchedulingSettings] ADD [AllowMonthlyRescheduleWithAutoDelete] bit NULL;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('DepartmentSchedulingSettings', 'AllowCurrentMonthScheduling') IS NOT NULL
    ALTER TABLE [DepartmentSchedulingSettings] DROP COLUMN [AllowCurrentMonthScheduling];

IF COL_LENGTH('DepartmentSchedulingSettings', 'AllowMonthlyRescheduleWithAutoDelete') IS NOT NULL
    ALTER TABLE [DepartmentSchedulingSettings] DROP COLUMN [AllowMonthlyRescheduleWithAutoDelete];
");
        }
    }
}
