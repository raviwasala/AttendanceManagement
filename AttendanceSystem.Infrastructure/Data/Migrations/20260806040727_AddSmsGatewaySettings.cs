using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AttendanceSystem.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSmsGatewaySettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SmsApiKeyEncrypted",
                table: "CompanySettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SmsApiUrl",
                table: "CompanySettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SmsAuthHeader",
                table: "CompanySettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SmsContentType",
                table: "CompanySettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "SmsEnabled",
                table: "CompanySettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SmsHttpMethod",
                table: "CompanySettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SmsProvider",
                table: "CompanySettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SmsRequestTemplate",
                table: "CompanySettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SmsSenderId",
                table: "CompanySettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "CompanySettings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "SmsApiKeyEncrypted", "SmsApiUrl", "SmsAuthHeader", "SmsContentType", "SmsEnabled", "SmsHttpMethod", "SmsProvider", "SmsRequestTemplate", "SmsSenderId" },
                values: new object[] { null, null, null, "application/json", false, "POST", null, null, null });

            // The column defaults are empty strings, which are not values the entity would
            // choose, and UpdateData above only reaches the seeded row. An empty method would
            // make SmsService treat every request as a POST regardless of what was configured.
            migrationBuilder.Sql(
                "UPDATE [CompanySettings] SET [SmsHttpMethod] = 'POST' WHERE [SmsHttpMethod] = '';");
            migrationBuilder.Sql(
                "UPDATE [CompanySettings] SET [SmsContentType] = 'application/json' WHERE [SmsContentType] = '';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SmsApiKeyEncrypted",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "SmsApiUrl",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "SmsAuthHeader",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "SmsContentType",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "SmsEnabled",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "SmsHttpMethod",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "SmsProvider",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "SmsRequestTemplate",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "SmsSenderId",
                table: "CompanySettings");
        }
    }
}
