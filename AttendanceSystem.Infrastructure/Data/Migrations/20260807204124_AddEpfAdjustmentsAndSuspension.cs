using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AttendanceSystem.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEpfAdjustmentsAndSuspension : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPayrollSuspended",
                table: "EmployeePayrollInfos",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SuspendReason",
                table: "EmployeePayrollInfos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SuspendedFrom",
                table: "EmployeePayrollInfos",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SuspendedTo",
                table: "EmployeePayrollInfos",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EmployeeLeaveEntitlements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    LeaveTypeId = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    EntitledDays = table.Column<int>(type: "int", nullable: false),
                    CarriedForwardDays = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeLeaveEntitlements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeLeaveEntitlements_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EmployeeLeaveEntitlements_LeaveTypes_LeaveTypeId",
                        column: x => x.LeaveTypeId,
                        principalTable: "LeaveTypes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EpfAdjustments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    Target = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    AffectsReturn = table.Column<bool>(type: "bit", nullable: false),
                    AppliedInPayrollPeriodId = table.Column<int>(type: "int", nullable: true),
                    AppliedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EpfAdjustments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EpfAdjustments_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EpfAdjustments_PayrollPeriods_AppliedInPayrollPeriodId",
                        column: x => x.AppliedInPayrollPeriodId,
                        principalTable: "PayrollPeriods",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeLeaveEntitlements_EmployeeId_LeaveTypeId_Year",
                table: "EmployeeLeaveEntitlements",
                columns: new[] { "EmployeeId", "LeaveTypeId", "Year" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeLeaveEntitlements_LeaveTypeId",
                table: "EmployeeLeaveEntitlements",
                column: "LeaveTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_EpfAdjustments_AppliedInPayrollPeriodId",
                table: "EpfAdjustments",
                column: "AppliedInPayrollPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_EpfAdjustments_EmployeeId",
                table: "EpfAdjustments",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EpfAdjustments_Year_Month_EmployeeId",
                table: "EpfAdjustments",
                columns: new[] { "Year", "Month", "EmployeeId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeeLeaveEntitlements");

            migrationBuilder.DropTable(
                name: "EpfAdjustments");

            migrationBuilder.DropColumn(
                name: "IsPayrollSuspended",
                table: "EmployeePayrollInfos");

            migrationBuilder.DropColumn(
                name: "SuspendReason",
                table: "EmployeePayrollInfos");

            migrationBuilder.DropColumn(
                name: "SuspendedFrom",
                table: "EmployeePayrollInfos");

            migrationBuilder.DropColumn(
                name: "SuspendedTo",
                table: "EmployeePayrollInfos");
        }
    }
}
