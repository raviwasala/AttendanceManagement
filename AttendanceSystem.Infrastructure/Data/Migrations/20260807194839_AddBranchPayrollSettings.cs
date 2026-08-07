using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AttendanceSystem.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchPayrollSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BranchPayrollSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    EpfDCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    EpfContactPerson = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    EpfContactPhone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    PayeRegistrationNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    NonCitizenTaxYears = table.Column<int>(type: "int", nullable: false),
                    EmployeeEpfPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    EmployerEpfPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    EmployerEtfPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    DaysPerMonth = table.Column<int>(type: "int", nullable: false),
                    HoursPerDay = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    BankBranchId = table.Column<int>(type: "int", nullable: true),
                    AccountNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    GratuityPercentOfBasic = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    GratuityQualifyingYears = table.Column<int>(type: "int", nullable: false),
                    RoundOffSalaryPayable = table.Column<bool>(type: "bit", nullable: false),
                    RoundNearest = table.Column<decimal>(type: "decimal(9,2)", nullable: false),
                    CarryForwardMinusSalary = table.Column<bool>(type: "bit", nullable: false),
                    CarryForwardCoins = table.Column<bool>(type: "bit", nullable: false),
                    EpfRounding = table.Column<int>(type: "int", nullable: false),
                    EtfRounding = table.Column<int>(type: "int", nullable: false),
                    NoPayRounding = table.Column<int>(type: "int", nullable: false),
                    TaxRounding = table.Column<int>(type: "int", nullable: false),
                    LoanRounding = table.Column<int>(type: "int", nullable: false),
                    OvertimeRounding = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BranchPayrollSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BranchPayrollSettings_BankBranches_BankBranchId",
                        column: x => x.BankBranchId,
                        principalTable: "BankBranches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BranchPayrollSettings_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_BranchPayrollSettings_BankBranchId",
                table: "BranchPayrollSettings",
                column: "BankBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_BranchPayrollSettings_BranchId",
                table: "BranchPayrollSettings",
                column: "BranchId",
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BranchPayrollSettings");
        }
    }
}
