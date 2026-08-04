using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AttendanceSystem.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFingerprintDevices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: false),
                    Port = table.Column<int>(type: "int", nullable: false),
                    CommKey = table.Column<int>(type: "int", nullable: true),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    AutoSyncEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LastPunchTimeSynced = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSyncStartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSuccessfulSyncAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConsecutiveFailures = table.Column<int>(type: "int", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Devices_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DevicePunches",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeviceId = table.Column<int>(type: "int", nullable: false),
                    DeviceUserId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PunchTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VerifyMode = table.Column<int>(type: "int", nullable: false),
                    InOutMode = table.Column<int>(type: "int", nullable: false),
                    DownloadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: true),
                    IsProcessed = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DevicePunches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DevicePunches_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DevicePunches_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "DeviceSyncLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeviceId = table.Column<int>(type: "int", nullable: false),
                    Trigger = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Outcome = table.Column<int>(type: "int", nullable: false),
                    PunchesRead = table.Column<int>(type: "int", nullable: false),
                    PunchesInserted = table.Column<int>(type: "int", nullable: false),
                    PunchesUnmapped = table.Column<int>(type: "int", nullable: false),
                    AttendanceRecordsAffected = table.Column<int>(type: "int", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TriggeredByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceSyncLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceSyncLogs_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeviceUserMappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeviceId = table.Column<int>(type: "int", nullable: false),
                    DeviceUserId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    DeviceUserName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceUserMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceUserMappings_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DeviceUserMappings_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // ── Devices permissions ───────────────────────────────────────────
            //
            // The scaffolded version of this step inserted Permissions with hard-coded ids
            // 52-56, taken from the position of Devices in PermissionCatalogue. That is only
            // correct for a database whose permission rows came from the seed.
            //
            // It is wrong for any database that was baselined (migration history recorded
            // without the seed actually running) and then had permissions added by hand —
            // their ids diverge, and a fixed-id insert fails on a primary key violation,
            // which would make Migrate() throw on startup and take the whole app down.
            //
            // Matching on (Module, Action) and letting IDENTITY assign the id is correct in
            // both cases, and is idempotent if this migration is ever re-run.
            migrationBuilder.Sql(@"
                INSERT INTO Permissions (Module, Action, DisplayName, IsDeleted, CreatedAt)
                SELECT v.Module, v.Action, v.DisplayName, 0, '2024-01-01T00:00:00'
                FROM (VALUES
                        ('Devices', 'View',   'View Devices'),
                        ('Devices', 'Create', 'Create Devices'),
                        ('Devices', 'Edit',   'Edit Devices'),
                        ('Devices', 'Delete', 'Delete Devices'),
                        ('Devices', 'Sync',   'Sync Devices')
                     ) AS v (Module, Action, DisplayName)
                WHERE NOT EXISTS (
                    SELECT 1 FROM Permissions p
                    WHERE p.Module = v.Module AND p.Action = v.Action);");

            // Grant to Administrator by role name rather than id 1, for the same reason.
            // Other roles are granted through the Roles screen — hardware configuration is
            // deliberately not handed out by default.
            migrationBuilder.Sql(@"
                INSERT INTO RolePermissions (RoleId, PermissionId)
                SELECT r.Id, p.Id
                FROM Roles r
                CROSS JOIN Permissions p
                WHERE p.Module = 'Devices'
                  AND r.Name = 'Administrator'
                  AND NOT EXISTS (
                      SELECT 1 FROM RolePermissions x
                      WHERE x.RoleId = r.Id AND x.PermissionId = p.Id);");

            migrationBuilder.CreateIndex(
                name: "IX_DevicePunches_DeviceId_DeviceUserId_PunchTime",
                table: "DevicePunches",
                columns: new[] { "DeviceId", "DeviceUserId", "PunchTime" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DevicePunches_EmployeeId",
                table: "DevicePunches",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_DevicePunches_IsProcessed_PunchTime",
                table: "DevicePunches",
                columns: new[] { "IsProcessed", "PunchTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Devices_BranchId",
                table: "Devices",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_IpAddress_Port",
                table: "Devices",
                columns: new[] { "IpAddress", "Port" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceSyncLogs_DeviceId_StartedAt",
                table: "DeviceSyncLogs",
                columns: new[] { "DeviceId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceUserMappings_DeviceId_DeviceUserId",
                table: "DeviceUserMappings",
                columns: new[] { "DeviceId", "DeviceUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceUserMappings_EmployeeId",
                table: "DeviceUserMappings",
                column: "EmployeeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DevicePunches");

            migrationBuilder.DropTable(
                name: "DeviceSyncLogs");

            migrationBuilder.DropTable(
                name: "DeviceUserMappings");

            migrationBuilder.DropTable(
                name: "Devices");

            // Matched by module, not by id — the scaffolded version deleted ids 52-56, which
            // on a database with diverged permission ids would silently delete whichever
            // unrelated permissions happened to occupy those rows. On this system id 52 is
            // Import.Create, so a rollback would have removed access to the Import page.
            migrationBuilder.Sql(@"
                DELETE rp
                FROM RolePermissions rp
                JOIN Permissions p ON p.Id = rp.PermissionId
                WHERE p.Module = 'Devices';");

            migrationBuilder.Sql(@"DELETE FROM Permissions WHERE Module = 'Devices';");
        }
    }
}
