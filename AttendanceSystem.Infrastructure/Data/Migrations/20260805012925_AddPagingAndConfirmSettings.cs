using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AttendanceSystem.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPagingAndConfirmSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Scaffolded defaults were false/0. 0 means "no paging" and false means "delete
            // without asking", so an existing row that UpdateData below does not touch would
            // silently get the worst of both. Backfill the intended defaults instead.
            migrationBuilder.AddColumn<bool>(
                name: "ConfirmBeforeDelete",
                table: "CompanySettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "DefaultPageSize",
                table: "CompanySettings",
                type: "int",
                nullable: false,
                defaultValue: 25);

            migrationBuilder.UpdateData(
                table: "CompanySettings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "ConfirmBeforeDelete", "DefaultPageSize" },
                values: new object[] { true, 25 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConfirmBeforeDelete",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "DefaultPageSize",
                table: "CompanySettings");
        }
    }
}
