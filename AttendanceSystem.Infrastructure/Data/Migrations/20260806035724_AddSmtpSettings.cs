using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AttendanceSystem.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSmtpSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SmtpEnableSsl",
                table: "CompanySettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SmtpEnabled",
                table: "CompanySettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SmtpFromAddress",
                table: "CompanySettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SmtpFromName",
                table: "CompanySettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SmtpHost",
                table: "CompanySettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SmtpPasswordEncrypted",
                table: "CompanySettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SmtpPort",
                table: "CompanySettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SmtpUsername",
                table: "CompanySettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "CompanySettings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "SmtpEnableSsl", "SmtpEnabled", "SmtpFromAddress", "SmtpFromName", "SmtpHost", "SmtpPasswordEncrypted", "SmtpPort", "SmtpUsername" },
                values: new object[] { true, false, null, null, null, null, 587, null });

            // The UpdateData above only reaches the seeded row. The column defaults are 0 and
            // false — neither is a value the entity would ever choose — so any settings row
            // with a different id would come out with port 0 and SSL off, and read as
            // deliberately configured rather than never set.
            migrationBuilder.Sql(
                "UPDATE [CompanySettings] SET [SmtpPort] = 587, [SmtpEnableSsl] = 1 WHERE [SmtpPort] = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SmtpEnableSsl",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "SmtpEnabled",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "SmtpFromAddress",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "SmtpFromName",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "SmtpHost",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "SmtpPasswordEncrypted",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "SmtpPort",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "SmtpUsername",
                table: "CompanySettings");
        }
    }
}
