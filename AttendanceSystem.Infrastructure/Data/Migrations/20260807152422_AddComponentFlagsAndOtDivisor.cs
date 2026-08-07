using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AttendanceSystem.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddComponentFlagsAndOtDivisor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // EF matched the renames by column position and got them the wrong way round —
            // it turned ProRateOnNoPay into IncludeInOtRate and IsFixed into IncludeInNoPay,
            // neither of which is what those columns meant. Corrected here:
            //
            //   ProRateOnNoPay → IncludeInNoPay   (same meaning, clearer name)
            //   IsFixed        → dropped          (split into Recurrence and the explicit
            //                                      EPF flag, so no single column inherits it)
            migrationBuilder.RenameColumn(
                name: "ProRateOnNoPay",
                table: "SalaryComponents",
                newName: "IncludeInNoPay");

            migrationBuilder.DropColumn(
                name: "IsFixed",
                table: "SalaryComponents");

            migrationBuilder.RenameColumn(
                name: "IsFixed",
                table: "PayslipLines",
                newName: "IsRecurring");

            migrationBuilder.AddColumn<bool>(
                name: "IncludeInOtRate",
                table: "SalaryComponents",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "BasedOnWorkingDays",
                table: "SalaryComponents",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IncludeInAllowanceOnlyNoPay",
                table: "SalaryComponents",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // True, matching the entity: an allowance counts as earnings unless somebody
            // says otherwise. Defaulting to false would drop every existing component out
            // of gross pay.
            migrationBuilder.AddColumn<bool>(
                name: "IncludeInGrossPay",
                table: "SalaryComponents",
                type: "bit",
                nullable: false,
                defaultValue: true);

            // 1 = Monthly. Zero is not a value the enum defines, and a component with an
            // undefined recurrence would fall through every branch of the calculator.
            migrationBuilder.AddColumn<int>(
                name: "Recurrence",
                table: "SalaryComponents",
                type: "int",
                nullable: false,
                defaultValue: 1);

            // 240, not 0 — this is a divisor. Zero would make the first overtime calculation
            // throw, and the settings row already exists so the column default is what it gets.
            migrationBuilder.AddColumn<decimal>(
                name: "OtHoursDivisor",
                table: "CompanySettings",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 240m);

            migrationBuilder.Sql(
                "UPDATE [CompanySettings] SET [OtHoursDivisor] = 240 WHERE [OtHoursDivisor] <= 0;");

            migrationBuilder.UpdateData(
                table: "CompanySettings",
                keyColumn: "Id",
                keyValue: 1,
                column: "OtHoursDivisor",
                value: 240m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BasedOnWorkingDays",
                table: "SalaryComponents");

            migrationBuilder.DropColumn(
                name: "IncludeInAllowanceOnlyNoPay",
                table: "SalaryComponents");

            migrationBuilder.DropColumn(
                name: "IncludeInGrossPay",
                table: "SalaryComponents");

            migrationBuilder.DropColumn(
                name: "Recurrence",
                table: "SalaryComponents");

            migrationBuilder.DropColumn(
                name: "OtHoursDivisor",
                table: "CompanySettings");

            // Mirrors the corrected Up: IncludeInOtRate was added here, so it is dropped
            // rather than renamed back into a column it never came from.
            migrationBuilder.DropColumn(
                name: "IncludeInOtRate",
                table: "SalaryComponents");

            migrationBuilder.RenameColumn(
                name: "IncludeInNoPay",
                table: "SalaryComponents",
                newName: "ProRateOnNoPay");

            migrationBuilder.AddColumn<bool>(
                name: "IsFixed",
                table: "SalaryComponents",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.RenameColumn(
                name: "IsRecurring",
                table: "PayslipLines",
                newName: "IsFixed");
        }
    }
}
