using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AttendanceSystem.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeLoans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmployeeLoans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    LoanTypeId = table.Column<int>(type: "int", nullable: false),
                    LoanDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    InterestRate = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    InterestType = table.Column<int>(type: "int", nullable: false),
                    LoanAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    InterestAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalPayable = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NumberOfInstallments = table.Column<int>(type: "int", nullable: false),
                    MonthlyInstallment = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ReduceThisMonth = table.Column<bool>(type: "bit", nullable: false),
                    FirstDeductionYear = table.Column<int>(type: "int", nullable: false),
                    FirstDeductionMonth = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AllowGuarantorsToGrantLoans = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeLoans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeLoans_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EmployeeLoans_LoanTypes_LoanTypeId",
                        column: x => x.LoanTypeId,
                        principalTable: "LoanTypes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "LoanGuarantors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeLoanId = table.Column<int>(type: "int", nullable: false),
                    GuarantorEmployeeId = table.Column<int>(type: "int", nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoanGuarantors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoanGuarantors_EmployeeLoans_EmployeeLoanId",
                        column: x => x.EmployeeLoanId,
                        principalTable: "EmployeeLoans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LoanGuarantors_Employees_GuarantorEmployeeId",
                        column: x => x.GuarantorEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "LoanTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeLoanId = table.Column<int>(type: "int", nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: true),
                    Month = table.Column<int>(type: "int", nullable: true),
                    TransactionType = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PayrollPeriodId = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoanTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoanTransactions_EmployeeLoans_EmployeeLoanId",
                        column: x => x.EmployeeLoanId,
                        principalTable: "EmployeeLoans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LoanTransactions_PayrollPeriods_PayrollPeriodId",
                        column: x => x.PayrollPeriodId,
                        principalTable: "PayrollPeriods",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeLoans_EmployeeId_Status",
                table: "EmployeeLoans",
                columns: new[] { "EmployeeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeLoans_LoanTypeId",
                table: "EmployeeLoans",
                column: "LoanTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_LoanGuarantors_EmployeeLoanId_GuarantorEmployeeId",
                table: "LoanGuarantors",
                columns: new[] { "EmployeeLoanId", "GuarantorEmployeeId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_LoanGuarantors_GuarantorEmployeeId",
                table: "LoanGuarantors",
                column: "GuarantorEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_LoanTransactions_EmployeeLoanId_Year_Month",
                table: "LoanTransactions",
                columns: new[] { "EmployeeLoanId", "Year", "Month" });

            migrationBuilder.CreateIndex(
                name: "IX_LoanTransactions_PayrollPeriodId",
                table: "LoanTransactions",
                column: "PayrollPeriodId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LoanGuarantors");

            migrationBuilder.DropTable(
                name: "LoanTransactions");

            migrationBuilder.DropTable(
                name: "EmployeeLoans");
        }
    }
}
