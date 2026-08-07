using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AttendanceSystem.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionHours : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Hours",
                table: "MonthlyTransactions",
                type: "decimal(9,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Hours",
                table: "MonthlyTransactions");
        }
    }
}
