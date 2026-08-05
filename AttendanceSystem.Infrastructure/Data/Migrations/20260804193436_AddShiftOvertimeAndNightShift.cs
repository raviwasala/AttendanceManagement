using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AttendanceSystem.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddShiftOvertimeAndNightShift : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BreakMinutes",
                table: "Shifts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "GraceOutMinutes",
                table: "Shifts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsNightShift",
                table: "Shifts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsOtEnabled",
                table: "Shifts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "OtCountsFromShiftEnd",
                table: "Shifts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "OtStartAfterMinutes",
                table: "Shifts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ShiftCode",
                table: "Shifts",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "StandardWorkingHours",
                table: "Shifts",
                type: "float(5)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "GrossHours",
                table: "AttendanceLogs",
                type: "float(6)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OvertimeMinutes",
                table: "AttendanceLogs",
                type: "int",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Shifts",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "BreakMinutes", "GraceOutMinutes", "IsNightShift", "IsOtEnabled", "OtCountsFromShiftEnd", "OtStartAfterMinutes", "ShiftCode", "StandardWorkingHours" },
                values: new object[] { 0, 0, false, true, true, 0, null, 0.0 });

            // Bring shifts that already exist in line with how a newly created shift behaves.
            //
            // The scaffolded AddColumn defaults are false, but the entity defaults are true —
            // which would leave the seeded shift with overtime enabled and every other shift
            // with it disabled, for no reason a user could discover.
            migrationBuilder.Sql(@"
                UPDATE Shifts
                SET IsOtEnabled = 1,
                    OtCountsFromShiftEnd = 1
                WHERE IsDeleted = 0;");

            // Flag shifts whose clock times already cross midnight (22:00 -> 06:30). Without
            // this they keep computing negative working hours, and the shift screen would
            // refuse to save them until someone ticked the box by hand.
            migrationBuilder.Sql(@"
                UPDATE Shifts
                SET IsNightShift = 1
                WHERE EndTime <= StartTime
                  AND IsDeleted = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BreakMinutes",
                table: "Shifts");

            migrationBuilder.DropColumn(
                name: "GraceOutMinutes",
                table: "Shifts");

            migrationBuilder.DropColumn(
                name: "IsNightShift",
                table: "Shifts");

            migrationBuilder.DropColumn(
                name: "IsOtEnabled",
                table: "Shifts");

            migrationBuilder.DropColumn(
                name: "OtCountsFromShiftEnd",
                table: "Shifts");

            migrationBuilder.DropColumn(
                name: "OtStartAfterMinutes",
                table: "Shifts");

            migrationBuilder.DropColumn(
                name: "ShiftCode",
                table: "Shifts");

            migrationBuilder.DropColumn(
                name: "StandardWorkingHours",
                table: "Shifts");

            migrationBuilder.DropColumn(
                name: "GrossHours",
                table: "AttendanceLogs");

            migrationBuilder.DropColumn(
                name: "OvertimeMinutes",
                table: "AttendanceLogs");
        }
    }
}
