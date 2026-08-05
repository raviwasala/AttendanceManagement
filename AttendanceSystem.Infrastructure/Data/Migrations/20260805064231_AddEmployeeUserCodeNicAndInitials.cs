using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AttendanceSystem.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeUserCodeNicAndInitials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NameWithInitials",
                table: "Employees",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Nic",
                table: "Employees",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserCode",
                table: "Employees",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_Nic",
                table: "Employees",
                column: "Nic");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_UserCode",
                table: "Employees",
                column: "UserCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Employees_Nic",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_UserCode",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "NameWithInitials",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "Nic",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "UserCode",
                table: "Employees");
        }
    }
}
