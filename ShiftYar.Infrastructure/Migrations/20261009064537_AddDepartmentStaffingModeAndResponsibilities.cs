using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShiftYar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDepartmentStaffingModeAndResponsibilities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DepartmentResponsibilityId",
                table: "ShiftAssignments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StaffingMode",
                table: "DepartmentSchedulingSettings",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DepartmentResponsibilities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DepartmentId = table.Column<int>(type: "int", nullable: true),
                    SpecialtyId = table.Column<int>(type: "int", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsDefault = table.Column<bool>(type: "bit", nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: true),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TheUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepartmentResponsibilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DepartmentResponsibilities_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DepartmentResponsibilities_Specialties_SpecialtyId",
                        column: x => x.SpecialtyId,
                        principalTable: "Specialties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

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
        [IsManuallyEdited] bit NOT NULL,
        [Notes] nvarchar(max) NULL,
        [ConfirmedAt] datetime2 NULL,
        [ConfirmedByUserId] int NULL,
        [CreateDate] datetime2 NULL,
        [UpdateDate] datetime2 NULL,
        [TheUserId] int NULL,
        CONSTRAINT [PK_UserMonthlyRequiredHours] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserMonthlyRequiredHours_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [Departments] ([Id]),
        CONSTRAINT [FK_UserMonthlyRequiredHours_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END
");

            migrationBuilder.CreateTable(
                name: "ShiftRequiredResponsibilities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShiftId = table.Column<int>(type: "int", nullable: true),
                    DepartmentResponsibilityId = table.Column<int>(type: "int", nullable: true),
                    RequiredMaleCount = table.Column<int>(type: "int", nullable: true),
                    RequiredFemaleCount = table.Column<int>(type: "int", nullable: true),
                    RequiredTotalCount = table.Column<int>(type: "int", nullable: true),
                    HolidayRequiredMaleCount = table.Column<int>(type: "int", nullable: true),
                    HolidayRequiredFemaleCount = table.Column<int>(type: "int", nullable: true),
                    HolidayRequiredTotalCount = table.Column<int>(type: "int", nullable: true),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TheUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftRequiredResponsibilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShiftRequiredResponsibilities_DepartmentResponsibilities_DepartmentResponsibilityId",
                        column: x => x.DepartmentResponsibilityId,
                        principalTable: "DepartmentResponsibilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ShiftRequiredResponsibilities_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "Shifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserDepartmentResponsibilities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    DepartmentResponsibilityId = table.Column<int>(type: "int", nullable: true),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TheUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDepartmentResponsibilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserDepartmentResponsibilities_DepartmentResponsibilities_DepartmentResponsibilityId",
                        column: x => x.DepartmentResponsibilityId,
                        principalTable: "DepartmentResponsibilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserDepartmentResponsibilities_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignments_DepartmentResponsibilityId",
                table: "ShiftAssignments",
                column: "DepartmentResponsibilityId");

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentResponsibilities_DepartmentId",
                table: "DepartmentResponsibilities",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentResponsibilities_SpecialtyId",
                table: "DepartmentResponsibilities",
                column: "SpecialtyId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftRequiredResponsibilities_DepartmentResponsibilityId",
                table: "ShiftRequiredResponsibilities",
                column: "DepartmentResponsibilityId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftRequiredResponsibilities_ShiftId_DepartmentResponsibilityId",
                table: "ShiftRequiredResponsibilities",
                columns: new[] { "ShiftId", "DepartmentResponsibilityId" },
                unique: true,
                filter: "[ShiftId] IS NOT NULL AND [DepartmentResponsibilityId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserDepartmentResponsibilities_DepartmentResponsibilityId",
                table: "UserDepartmentResponsibilities",
                column: "DepartmentResponsibilityId");

            migrationBuilder.CreateIndex(
                name: "IX_UserDepartmentResponsibilities_UserId_DepartmentResponsibilityId",
                table: "UserDepartmentResponsibilities",
                columns: new[] { "UserId", "DepartmentResponsibilityId" },
                unique: true,
                filter: "[UserId] IS NOT NULL AND [DepartmentResponsibilityId] IS NOT NULL");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_UserMonthlyRequiredHours_DepartmentId' AND object_id = OBJECT_ID('UserMonthlyRequiredHours'))
    CREATE INDEX [IX_UserMonthlyRequiredHours_DepartmentId] ON [UserMonthlyRequiredHours] ([DepartmentId]);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_UserMonthlyRequiredHours_UserId_PersianYear_PersianMonth' AND object_id = OBJECT_ID('UserMonthlyRequiredHours'))
    CREATE UNIQUE INDEX [IX_UserMonthlyRequiredHours_UserId_PersianYear_PersianMonth] ON [UserMonthlyRequiredHours] ([UserId], [PersianYear], [PersianMonth]);
");

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftAssignments_DepartmentResponsibilities_DepartmentResponsibilityId",
                table: "ShiftAssignments",
                column: "DepartmentResponsibilityId",
                principalTable: "DepartmentResponsibilities",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ShiftAssignments_DepartmentResponsibilities_DepartmentResponsibilityId",
                table: "ShiftAssignments");

            migrationBuilder.DropTable(
                name: "ShiftRequiredResponsibilities");

            migrationBuilder.DropTable(
                name: "UserDepartmentResponsibilities");

            migrationBuilder.DropTable(
                name: "UserMonthlyRequiredHours");

            migrationBuilder.DropTable(
                name: "DepartmentResponsibilities");

            migrationBuilder.DropIndex(
                name: "IX_ShiftAssignments_DepartmentResponsibilityId",
                table: "ShiftAssignments");

            migrationBuilder.DropColumn(
                name: "DepartmentResponsibilityId",
                table: "ShiftAssignments");

            migrationBuilder.DropColumn(
                name: "StaffingMode",
                table: "DepartmentSchedulingSettings");
        }
    }
}
