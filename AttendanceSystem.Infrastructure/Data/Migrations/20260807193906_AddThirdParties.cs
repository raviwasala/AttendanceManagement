using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AttendanceSystem.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddThirdParties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ThirdParties",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SalaryComponentId = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThirdParties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ThirdParties_SalaryComponents_SalaryComponentId",
                        column: x => x.SalaryComponentId,
                        principalTable: "SalaryComponents",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ThirdParties_Code",
                table: "ThirdParties",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ThirdParties_SalaryComponentId",
                table: "ThirdParties",
                column: "SalaryComponentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ThirdParties");
        }
    }
}
