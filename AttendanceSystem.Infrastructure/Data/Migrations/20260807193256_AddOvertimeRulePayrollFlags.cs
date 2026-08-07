using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AttendanceSystem.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOvertimeRulePayrollFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "OvertimeRules",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            // True, matching the entity. EF defaults every new bool to false, which here would
            // make all existing overtime non-taxable and drop it out of gross pay — a change
            // nobody asked for and one that shows up only as a wrong payslip.
            migrationBuilder.AddColumn<bool>(
                name: "IncludeInGrossPay",
                table: "OvertimeRules",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsApitLiable",
                table: "OvertimeRules",
                type: "bit",
                nullable: false,
                defaultValue: true);

            // False is correct here: overtime is customarily outside EPF, and turning it on
            // for everyone would raise every contribution silently.
            migrationBuilder.AddColumn<bool>(
                name: "IsEpfLiable",
                table: "OvertimeRules",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // The column default only applies to rows added afterwards, so existing rules are
            // brought into line explicitly.
            migrationBuilder.Sql(
                "UPDATE [OvertimeRules] SET [IncludeInGrossPay] = 1, [IsApitLiable] = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Code",
                table: "OvertimeRules");

            migrationBuilder.DropColumn(
                name: "IncludeInGrossPay",
                table: "OvertimeRules");

            migrationBuilder.DropColumn(
                name: "IsApitLiable",
                table: "OvertimeRules");

            migrationBuilder.DropColumn(
                name: "IsEpfLiable",
                table: "OvertimeRules");
        }
    }
}
