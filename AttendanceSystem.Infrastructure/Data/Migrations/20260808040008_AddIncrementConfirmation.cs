using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AttendanceSystem.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIncrementConfirmation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ConfirmedAt",
                table: "SalaryIncrements",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ConfirmedBy",
                table: "SalaryIncrements",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "SalaryIncrements",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "SalaryIncrements",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_SalaryIncrements_Status",
                table: "SalaryIncrements",
                column: "Status");

            // Rows that already exist were applied straight to the salary — the two-step
            // flow did not exist when they were written. The column default of Pending is
            // right for everything from now on and wrong for them: left as Pending they
            // would appear on the confirmation screen and, once confirmed, raise the same
            // employee a second time from an already-raised basic.
            migrationBuilder.Sql(@"
                UPDATE SalaryIncrements
                SET Status = 2,                 -- Confirmed
                    ConfirmedAt = CreatedAt,
                    ConfirmedBy = CreatedBy
                WHERE Status = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SalaryIncrements_Status",
                table: "SalaryIncrements");

            migrationBuilder.DropColumn(
                name: "ConfirmedAt",
                table: "SalaryIncrements");

            migrationBuilder.DropColumn(
                name: "ConfirmedBy",
                table: "SalaryIncrements");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "SalaryIncrements");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "SalaryIncrements");
        }
    }
}
