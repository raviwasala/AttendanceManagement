using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AttendanceSystem.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOvertimeRuleDaysHours : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OtHoursDivisor",
                table: "CompanySettings");

            migrationBuilder.AddColumn<int>(
                name: "DaysPerMonth",
                table: "OvertimeRules",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "HoursPerDay",
                table: "OvertimeRules",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DaysPerMonth",
                table: "OvertimeRules");

            migrationBuilder.DropColumn(
                name: "HoursPerDay",
                table: "OvertimeRules");

            migrationBuilder.AddColumn<decimal>(
                name: "OtHoursDivisor",
                table: "CompanySettings",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.UpdateData(
                table: "CompanySettings",
                keyColumn: "Id",
                keyValue: 1,
                column: "OtHoursDivisor",
                value: 240m);
        }
    }
}
