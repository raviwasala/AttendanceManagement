using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AttendanceSystem.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMonthlyTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MonthlyTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    SalaryComponentId = table.Column<int>(type: "int", nullable: false),
                    YearMonth = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonthlyTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MonthlyTransactions_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MonthlyTransactions_SalaryComponents_SalaryComponentId",
                        column: x => x.SalaryComponentId,
                        principalTable: "SalaryComponents",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyTransactions_EmployeeId_SalaryComponentId_YearMonth",
                table: "MonthlyTransactions",
                columns: new[] { "EmployeeId", "SalaryComponentId", "YearMonth" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyTransactions_SalaryComponentId",
                table: "MonthlyTransactions",
                column: "SalaryComponentId");

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyTransactions_YearMonth_SalaryComponentId",
                table: "MonthlyTransactions",
                columns: new[] { "YearMonth", "SalaryComponentId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MonthlyTransactions");
        }
    }
}
