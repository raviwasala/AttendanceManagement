using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AttendanceSystem.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddApitTablesAndEmploymentCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ApitTaxBrackets_EffectiveFrom_SortOrder",
                table: "ApitTaxBrackets");

            migrationBuilder.AddColumn<string>(
                name: "CivilStatus",
                table: "Employees",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConfirmedDate",
                table: "Employees",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AdditionalTaxAmount",
                table: "EmployeePayrollInfos",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "ApitTaxTableId",
                table: "EmployeePayrollInfos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EmployeeEpfPercentOverride",
                table: "EmployeePayrollInfos",
                type: "decimal(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EmployerEpfPercentOverride",
                table: "EmployeePayrollInfos",
                type: "decimal(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EmployerEtfPercentOverride",
                table: "EmployeePayrollInfos",
                type: "decimal(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmploymentCategoryId",
                table: "EmployeePayrollInfos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EpfRegistrationBranchId",
                table: "EmployeePayrollInfos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EpfStatus",
                table: "EmployeePayrollInfos",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsTaxOnTax",
                table: "EmployeePayrollInfos",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "OtLimitHours",
                table: "EmployeePayrollInfos",
                type: "decimal(9,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "ApitTaxTableId",
                table: "ApitTaxBrackets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ApitTaxTables",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApitTaxTables", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmploymentCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsEpfEligible = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmploymentCategories", x => x.Id);
                });

            // ── Backfill ──────────────────────────────────────────────────────
            //
            // ApitTaxTableId was added to ApitTaxBrackets as required with a default of 0,
            // which is not a table any row can point at. The bands seeded by the previous
            // migration have to be adopted by a real table before the foreign key below is
            // created, or the migration fails on a database that already has them.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM [ApitTaxTables] WHERE [IsDeleted] = 0)
    INSERT INTO [ApitTaxTables] ([Name],[Code],[Description],[IsDefault],[IsActive],[IsDeleted],[CreatedAt])
    VALUES (N'Primary Employment', N'T1',
            N'Regular monthly APIT table for primary employment.', 1, 1, 0, SYSDATETIME());

DECLARE @defaultTable INT =
    (SELECT TOP 1 [Id] FROM [ApitTaxTables] WHERE [IsDefault] = 1 AND [IsDeleted] = 0 ORDER BY [Id]);

UPDATE [ApitTaxBrackets] SET [ApitTaxTableId] = @defaultTable WHERE [ApitTaxTableId] = 0;
");

            // Employment types. Seeded because payroll cannot classify anybody without them,
            // and these four cover the arrangements this system is used for. EPF eligibility
            // is the default for the category — it stays overridable per employee.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM [EmploymentCategories] WHERE [IsDeleted] = 0)
    INSERT INTO [EmploymentCategories] ([Name],[Code],[IsEpfEligible],[IsActive],[IsDeleted],[CreatedAt])
    VALUES (N'Permanent', N'PERM', 1, 1, 0, SYSDATETIME()),
           (N'Probation', N'PROB', 1, 1, 0, SYSDATETIME()),
           (N'Contract',  N'CONT', 1, 1, 0, SYSDATETIME()),
           (N'Casual',    N'CAS',  0, 1, 0, SYSDATETIME());
");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePayrollInfos_ApitTaxTableId",
                table: "EmployeePayrollInfos",
                column: "ApitTaxTableId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePayrollInfos_EmploymentCategoryId",
                table: "EmployeePayrollInfos",
                column: "EmploymentCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePayrollInfos_EpfRegistrationBranchId",
                table: "EmployeePayrollInfos",
                column: "EpfRegistrationBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_ApitTaxBrackets_ApitTaxTableId_EffectiveFrom_SortOrder",
                table: "ApitTaxBrackets",
                columns: new[] { "ApitTaxTableId", "EffectiveFrom", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ApitTaxTables_Code",
                table: "ApitTaxTables",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_EmploymentCategories_Code",
                table: "EmploymentCategories",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_ApitTaxBrackets_ApitTaxTables_ApitTaxTableId",
                table: "ApitTaxBrackets",
                column: "ApitTaxTableId",
                principalTable: "ApitTaxTables",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeePayrollInfos_ApitTaxTables_ApitTaxTableId",
                table: "EmployeePayrollInfos",
                column: "ApitTaxTableId",
                principalTable: "ApitTaxTables",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeePayrollInfos_Branches_EpfRegistrationBranchId",
                table: "EmployeePayrollInfos",
                column: "EpfRegistrationBranchId",
                principalTable: "Branches",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeePayrollInfos_EmploymentCategories_EmploymentCategoryId",
                table: "EmployeePayrollInfos",
                column: "EmploymentCategoryId",
                principalTable: "EmploymentCategories",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApitTaxBrackets_ApitTaxTables_ApitTaxTableId",
                table: "ApitTaxBrackets");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeePayrollInfos_ApitTaxTables_ApitTaxTableId",
                table: "EmployeePayrollInfos");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeePayrollInfos_Branches_EpfRegistrationBranchId",
                table: "EmployeePayrollInfos");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeePayrollInfos_EmploymentCategories_EmploymentCategoryId",
                table: "EmployeePayrollInfos");

            migrationBuilder.DropTable(
                name: "ApitTaxTables");

            migrationBuilder.DropTable(
                name: "EmploymentCategories");

            migrationBuilder.DropIndex(
                name: "IX_EmployeePayrollInfos_ApitTaxTableId",
                table: "EmployeePayrollInfos");

            migrationBuilder.DropIndex(
                name: "IX_EmployeePayrollInfos_EmploymentCategoryId",
                table: "EmployeePayrollInfos");

            migrationBuilder.DropIndex(
                name: "IX_EmployeePayrollInfos_EpfRegistrationBranchId",
                table: "EmployeePayrollInfos");

            migrationBuilder.DropIndex(
                name: "IX_ApitTaxBrackets_ApitTaxTableId_EffectiveFrom_SortOrder",
                table: "ApitTaxBrackets");

            migrationBuilder.DropColumn(
                name: "CivilStatus",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "ConfirmedDate",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "AdditionalTaxAmount",
                table: "EmployeePayrollInfos");

            migrationBuilder.DropColumn(
                name: "ApitTaxTableId",
                table: "EmployeePayrollInfos");

            migrationBuilder.DropColumn(
                name: "EmployeeEpfPercentOverride",
                table: "EmployeePayrollInfos");

            migrationBuilder.DropColumn(
                name: "EmployerEpfPercentOverride",
                table: "EmployeePayrollInfos");

            migrationBuilder.DropColumn(
                name: "EmployerEtfPercentOverride",
                table: "EmployeePayrollInfos");

            migrationBuilder.DropColumn(
                name: "EmploymentCategoryId",
                table: "EmployeePayrollInfos");

            migrationBuilder.DropColumn(
                name: "EpfRegistrationBranchId",
                table: "EmployeePayrollInfos");

            migrationBuilder.DropColumn(
                name: "EpfStatus",
                table: "EmployeePayrollInfos");

            migrationBuilder.DropColumn(
                name: "IsTaxOnTax",
                table: "EmployeePayrollInfos");

            migrationBuilder.DropColumn(
                name: "OtLimitHours",
                table: "EmployeePayrollInfos");

            migrationBuilder.DropColumn(
                name: "ApitTaxTableId",
                table: "ApitTaxBrackets");

            migrationBuilder.CreateIndex(
                name: "IX_ApitTaxBrackets_EffectiveFrom_SortOrder",
                table: "ApitTaxBrackets",
                columns: new[] { "EffectiveFrom", "SortOrder" });
        }
    }
}
