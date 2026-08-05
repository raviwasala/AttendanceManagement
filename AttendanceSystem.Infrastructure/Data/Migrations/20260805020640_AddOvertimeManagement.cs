using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AttendanceSystem.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOvertimeManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OvertimeRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    DepartmentId = table.Column<int>(type: "int", nullable: true),
                    ShiftId = table.Column<int>(type: "int", nullable: true),
                    DayType = table.Column<int>(type: "int", nullable: false),
                    RateMultiplier = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    MinimumMinutes = table.Column<int>(type: "int", nullable: false),
                    MaxMinutesPerDay = table.Column<int>(type: "int", nullable: true),
                    RoundToMinutes = table.Column<int>(type: "int", nullable: false),
                    RequiresApproval = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OvertimeRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OvertimeRules_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OvertimeRules_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "Shifts",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "OvertimeRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    OvertimeDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AttendanceLogId = table.Column<int>(type: "int", nullable: true),
                    ShiftId = table.Column<int>(type: "int", nullable: true),
                    RawMinutes = table.Column<int>(type: "int", nullable: false),
                    ClaimedMinutes = table.Column<int>(type: "int", nullable: false),
                    ApprovedMinutes = table.Column<int>(type: "int", nullable: true),
                    OvertimeRuleId = table.Column<int>(type: "int", nullable: true),
                    RuleName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RateMultiplier = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    DayType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ApprovedBy = table.Column<int>(type: "int", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsManual = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OvertimeRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OvertimeRecords_AttendanceLogs_AttendanceLogId",
                        column: x => x.AttendanceLogId,
                        principalTable: "AttendanceLogs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OvertimeRecords_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OvertimeRecords_OvertimeRules_OvertimeRuleId",
                        column: x => x.OvertimeRuleId,
                        principalTable: "OvertimeRules",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OvertimeRecords_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "Shifts",
                        principalColumn: "Id");
                });

            // Permissions are inserted by (Module, Action) with IDENTITY-assigned ids rather
            // than the scaffolded literals 57-62. This database already has 57 permission rows
            // (Devices.Sync is 57), so the generated InsertData would have collided on the
            // primary key and failed at startup. Matching on the natural key also makes the
            // migration idempotent if it is ever re-run against a partially seeded database.
            migrationBuilder.Sql(@"
                INSERT INTO Permissions (Module, Action, DisplayName, IsDeleted, CreatedAt)
                SELECT v.Module, v.Action, v.Action + ' Overtime', 0, '2024-01-01T00:00:00'
                FROM (VALUES ('Overtime','View'), ('Overtime','Create'), ('Overtime','Edit'),
                             ('Overtime','Delete'), ('Overtime','Approve'), ('Overtime','Export')
                     ) AS v(Module, Action)
                WHERE NOT EXISTS (
                    SELECT 1 FROM Permissions p
                    WHERE p.Module = v.Module AND p.Action = v.Action);");

            // Administrator (1) gets everything; HR Manager (2) gets overtime including
            // approval, matching how the catalogue grants every non-excluded module.
            migrationBuilder.Sql(@"
                INSERT INTO RolePermissions (RoleId, PermissionId)
                SELECT r.RoleId, p.Id
                FROM (VALUES (1), (2)) AS r(RoleId)
                CROSS JOIN Permissions p
                WHERE p.Module = 'Overtime'
                  AND NOT EXISTS (
                      SELECT 1 FROM RolePermissions rp
                      WHERE rp.RoleId = r.RoleId AND rp.PermissionId = p.Id);");
            migrationBuilder.CreateIndex(
                name: "IX_OvertimeRecords_AttendanceLogId",
                table: "OvertimeRecords",
                column: "AttendanceLogId");

            migrationBuilder.CreateIndex(
                name: "IX_OvertimeRecords_EmployeeId_OvertimeDate",
                table: "OvertimeRecords",
                columns: new[] { "EmployeeId", "OvertimeDate" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_OvertimeRecords_OvertimeDate_Status",
                table: "OvertimeRecords",
                columns: new[] { "OvertimeDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_OvertimeRecords_OvertimeRuleId",
                table: "OvertimeRecords",
                column: "OvertimeRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_OvertimeRecords_ShiftId",
                table: "OvertimeRecords",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_OvertimeRules_DepartmentId",
                table: "OvertimeRules",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_OvertimeRules_Name",
                table: "OvertimeRules",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OvertimeRules_ShiftId",
                table: "OvertimeRules",
                column: "ShiftId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OvertimeRecords");

            migrationBuilder.DropTable(
                name: "OvertimeRules");

            // Mirrors the Up above: remove by natural key, since the ids were assigned by
            // IDENTITY and are not known here. Grants go first - RolePermissions has a
            // foreign key to Permissions.
            migrationBuilder.Sql(@"
                DELETE rp FROM RolePermissions rp
                INNER JOIN Permissions p ON p.Id = rp.PermissionId
                WHERE p.Module = 'Overtime';");

            migrationBuilder.Sql("DELETE FROM Permissions WHERE Module = 'Overtime';");
        }
    }
}
