using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AttendanceSystem.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserApprovalScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1 = CompanyWide. Scaffolded as 0, which is not a value the enum defines —
            // every existing user approved for every department before this column existed,
            // so they are backfilled to say exactly that. Narrowing anyone is a deliberate
            // act on the Users screen, never a side effect of upgrading.
            migrationBuilder.AddColumn<int>(
                name: "ApprovalScope",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql("UPDATE [Users] SET [ApprovalScope] = 1 WHERE [ApprovalScope] = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApprovalScope",
                table: "Users");
        }
    }
}
