using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AttendanceSystem.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddScreenLockMinutes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ScreenLockMinutes",
                table: "CompanySettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "CompanySettings",
                keyColumn: "Id",
                keyValue: 1,
                column: "ScreenLockMinutes",
                value: 15);

            // The scaffolded update above only touches the seeded row. An installation whose
            // settings row has any other id would come out of this migration with 0, which
            // means locking silently disabled — the opposite of the intended default.
            migrationBuilder.Sql("UPDATE [CompanySettings] SET [ScreenLockMinutes] = 15 WHERE [ScreenLockMinutes] = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ScreenLockMinutes",
                table: "CompanySettings");
        }
    }
}
