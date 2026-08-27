using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShiftYar.Infrastructure.Persistence.AppDbContext;

#nullable disable

namespace ShiftYar.Infrastructure.Migrations
{
    /// <summary>
    /// انتقال الزام مسئول شیفت از تنظیمات دپارتمان به تعریف شیفت.
    /// ستون‌های دپارتمان برای fallback نگه داشته می‌شوند.
    /// </summary>
    [DbContext(typeof(ShiftYarDbContext))]
    [Migration("20260827230000_AddShiftManagerCountsToShift")]
    public partial class AddShiftManagerCountsToShift : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('Shifts', 'ManagerRequiredCount') IS NULL
    ALTER TABLE [Shifts] ADD [ManagerRequiredCount] int NOT NULL CONSTRAINT DF_Shifts_ManagerRequiredCount DEFAULT (0);
");
            migrationBuilder.Sql(@"
IF COL_LENGTH('Shifts', 'ManagerMinLevel1Count') IS NULL
    ALTER TABLE [Shifts] ADD [ManagerMinLevel1Count] int NOT NULL CONSTRAINT DF_Shifts_ManagerMinLevel1Count DEFAULT (0);
");

            // کپی از تنظیمات دپارتمان بر اساس Label (Morning=0, Evening=1, Night=2)
            // فقط وقتی روی شیفت هنوز ۰ است تا داده‌های دستی حفظ شوند.
            migrationBuilder.Sql(@"
UPDATE s
SET
    s.[ManagerRequiredCount] = CASE s.[Label]
        WHEN 0 THEN ISNULL(d.[MorningShiftManagerRequiredCount],
            CASE WHEN d.[RequireManagerForMorningShift] = 1 THEN 1 ELSE 0 END)
        WHEN 1 THEN ISNULL(d.[EveningShiftManagerRequiredCount],
            CASE WHEN d.[RequireManagerForEveningShift] = 1 THEN 1 ELSE 0 END)
        WHEN 2 THEN ISNULL(d.[NightShiftManagerRequiredCount],
            CASE WHEN d.[RequireManagerForNightShift] = 1 THEN 1 ELSE 0 END)
        ELSE 0
    END,
    s.[ManagerMinLevel1Count] = CASE s.[Label]
        WHEN 0 THEN ISNULL(d.[MorningShiftManagerMinLevel1Count], 0)
        WHEN 1 THEN ISNULL(d.[EveningShiftManagerMinLevel1Count], 0)
        WHEN 2 THEN ISNULL(d.[NightShiftManagerMinLevel1Count], 0)
        ELSE 0
    END
FROM [Shifts] s
INNER JOIN [DepartmentSchedulingSettings] d ON d.[DepartmentId] = s.[DepartmentId]
WHERE ISNULL(s.[ManagerRequiredCount], 0) = 0
  AND s.[Label] IN (0, 1, 2);
");

            // Clamp: MinLevel1 نباید از Required بیشتر باشد
            migrationBuilder.Sql(@"
UPDATE [Shifts]
SET [ManagerMinLevel1Count] = [ManagerRequiredCount]
WHERE [ManagerMinLevel1Count] > [ManagerRequiredCount];
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('DF_Shifts_ManagerRequiredCount', 'D') IS NOT NULL
    ALTER TABLE [Shifts] DROP CONSTRAINT [DF_Shifts_ManagerRequiredCount];
");
            migrationBuilder.Sql(@"
IF OBJECT_ID('DF_Shifts_ManagerMinLevel1Count', 'D') IS NOT NULL
    ALTER TABLE [Shifts] DROP CONSTRAINT [DF_Shifts_ManagerMinLevel1Count];
");
            migrationBuilder.Sql(@"
IF COL_LENGTH('Shifts', 'ManagerRequiredCount') IS NOT NULL
    ALTER TABLE [Shifts] DROP COLUMN [ManagerRequiredCount];
");
            migrationBuilder.Sql(@"
IF COL_LENGTH('Shifts', 'ManagerMinLevel1Count') IS NOT NULL
    ALTER TABLE [Shifts] DROP COLUMN [ManagerMinLevel1Count];
");
        }
    }
}
