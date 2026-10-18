using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShiftYar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDepartmentResponsibilityToShiftRequiredSpecialties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ShiftExchanges_ShiftAssignments_OfferingShiftAssignmentId",
                table: "ShiftExchanges");

            migrationBuilder.DropForeignKey(
                name: "FK_ShiftExchanges_ShiftAssignments_RequestingShiftAssignmentId",
                table: "ShiftExchanges");

            migrationBuilder.AddColumn<int>(
                name: "DepartmentResponsibilityId",
                table: "ShiftRequiredSpecialties",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShiftRequiredSpecialties_DepartmentResponsibilityId",
                table: "ShiftRequiredSpecialties",
                column: "DepartmentResponsibilityId");

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftExchanges_ShiftAssignments_OfferingShiftAssignmentId",
                table: "ShiftExchanges",
                column: "OfferingShiftAssignmentId",
                principalTable: "ShiftAssignments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftExchanges_ShiftAssignments_RequestingShiftAssignmentId",
                table: "ShiftExchanges",
                column: "RequestingShiftAssignmentId",
                principalTable: "ShiftAssignments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftRequiredSpecialties_DepartmentResponsibilities_DepartmentResponsibilityId",
                table: "ShiftRequiredSpecialties",
                column: "DepartmentResponsibilityId",
                principalTable: "DepartmentResponsibilities",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'ShiftRequiredResponsibilities', N'U') IS NOT NULL AND OBJECT_ID(N'ShiftRequiredSpecialties', N'U') IS NOT NULL
                BEGIN
                    INSERT INTO ShiftRequiredSpecialties (
                        ShiftId,
                        SpecialtyId,
                        DepartmentResponsibilityId,
                        RequiredMaleCount,
                        RequiredFemaleCount,
                        RequiredTottalCount,
                        HolidayRequiredMaleCount,
                        HolidayRequiredFemaleCount,
                        HolidayRequiredTottalCount,
                        CreateDate
                    )
                    SELECT 
                        r.ShiftId,
                        dr.SpecialtyId,
                        r.DepartmentResponsibilityId,
                        r.RequiredMaleCount,
                        r.RequiredFemaleCount,
                        r.RequiredTotalCount,
                        r.HolidayRequiredMaleCount,
                        r.HolidayRequiredFemaleCount,
                        r.HolidayRequiredTotalCount,
                        GETDATE()
                    FROM ShiftRequiredResponsibilities r
                    LEFT JOIN DepartmentResponsibilities dr ON r.DepartmentResponsibilityId = dr.Id
                    WHERE NOT EXISTS (
                        SELECT 1 
                        FROM ShiftRequiredSpecialties s 
                        WHERE s.ShiftId = r.ShiftId 
                          AND s.DepartmentResponsibilityId = r.DepartmentResponsibilityId
                    );
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM ShiftRequiredSpecialties 
                WHERE DepartmentResponsibilityId IS NOT NULL;
            ");

            migrationBuilder.DropForeignKey(
                name: "FK_ShiftExchanges_ShiftAssignments_OfferingShiftAssignmentId",
                table: "ShiftExchanges");

            migrationBuilder.DropForeignKey(
                name: "FK_ShiftExchanges_ShiftAssignments_RequestingShiftAssignmentId",
                table: "ShiftExchanges");

            migrationBuilder.DropForeignKey(
                name: "FK_ShiftRequiredSpecialties_DepartmentResponsibilities_DepartmentResponsibilityId",
                table: "ShiftRequiredSpecialties");

            migrationBuilder.DropIndex(
                name: "IX_ShiftRequiredSpecialties_DepartmentResponsibilityId",
                table: "ShiftRequiredSpecialties");

            migrationBuilder.DropColumn(
                name: "DepartmentResponsibilityId",
                table: "ShiftRequiredSpecialties");

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftExchanges_ShiftAssignments_OfferingShiftAssignmentId",
                table: "ShiftExchanges",
                column: "OfferingShiftAssignmentId",
                principalTable: "ShiftAssignments",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftExchanges_ShiftAssignments_RequestingShiftAssignmentId",
                table: "ShiftExchanges",
                column: "RequestingShiftAssignmentId",
                principalTable: "ShiftAssignments",
                principalColumn: "Id");
        }
    }
}
