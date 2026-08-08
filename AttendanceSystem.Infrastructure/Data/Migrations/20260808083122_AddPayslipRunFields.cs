using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AttendanceSystem.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPayslipRunFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BroughtForward",
                table: "Payslips",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CarriedForward",
                table: "Payslips",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "EarnedBasic",
                table: "Payslips",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsBankTransfer",
                table: "Payslips",
                type: "bit",
                nullable: false,
                // True, matching the entity. EF scaffolds a bool column as false regardless of
                // the property initialiser, and a column that disagrees with its model means
                // any insert omitting it quietly lands in the cash list instead of the bank
                // file. No payslips exist yet so nothing needs backfilling — this only stops
                // the two drifting from the start.
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Payslips",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SalaryArrears",
                table: "Payslips",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SrLevy",
                table: "Payslips",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "StampDuty",
                table: "Payslips",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalLoanInstalments",
                table: "Payslips",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BroughtForward",
                table: "Payslips");

            migrationBuilder.DropColumn(
                name: "CarriedForward",
                table: "Payslips");

            migrationBuilder.DropColumn(
                name: "EarnedBasic",
                table: "Payslips");

            migrationBuilder.DropColumn(
                name: "IsBankTransfer",
                table: "Payslips");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Payslips");

            migrationBuilder.DropColumn(
                name: "SalaryArrears",
                table: "Payslips");

            migrationBuilder.DropColumn(
                name: "SrLevy",
                table: "Payslips");

            migrationBuilder.DropColumn(
                name: "StampDuty",
                table: "Payslips");

            migrationBuilder.DropColumn(
                name: "TotalLoanInstalments",
                table: "Payslips");
        }
    }
}
