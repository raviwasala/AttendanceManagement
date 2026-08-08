using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AttendanceSystem.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIncrementArrears : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ArrearsAmount",
                table: "SalaryIncrements",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "ArrearsMonths",
                table: "SalaryIncrements",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ArrearsPaidInYearMonth",
                table: "SalaryIncrements",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArrearsAmount",
                table: "SalaryIncrements");

            migrationBuilder.DropColumn(
                name: "ArrearsMonths",
                table: "SalaryIncrements");

            migrationBuilder.DropColumn(
                name: "ArrearsPaidInYearMonth",
                table: "SalaryIncrements");
        }
    }
}
