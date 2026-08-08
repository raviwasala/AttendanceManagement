using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AttendanceSystem.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTaxTableType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TableType",
                table: "ApitTaxTables",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_ApitTaxTables_TableType_IsDefault",
                table: "ApitTaxTables",
                columns: new[] { "TableType", "IsDefault" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ApitTaxTables_TableType_IsDefault",
                table: "ApitTaxTables");

            migrationBuilder.DropColumn(
                name: "TableType",
                table: "ApitTaxTables");
        }
    }
}
