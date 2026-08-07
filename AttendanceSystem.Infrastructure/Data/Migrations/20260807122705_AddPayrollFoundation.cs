using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AttendanceSystem.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EpfEmployerNumber",
                table: "Branches",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EtfEmployerNumber",
                table: "Branches",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ApitTaxBrackets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FromAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ToAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Rate = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    Relief = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApitTaxBrackets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Banks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Banks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EpfEtfRates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EmployeeEpfPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    EmployerEpfPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    EmployerEtfPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EpfEtfRates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PayrollPeriods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Month = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    EmployeeEpfPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    EmployerEpfPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    EmployerEtfPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProcessedBy = table.Column<int>(type: "int", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedBy = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollPeriods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollPeriods_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SalaryComponents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ComponentType = table.Column<int>(type: "int", nullable: false),
                    IsFixed = table.Column<bool>(type: "bit", nullable: false),
                    IsEpfLiable = table.Column<bool>(type: "bit", nullable: false),
                    IsApitLiable = table.Column<bool>(type: "bit", nullable: false),
                    CalculationType = table.Column<int>(type: "int", nullable: false),
                    DefaultValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ProRateOnNoPay = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalaryComponents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SalaryGrades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BasicSalary = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalaryGrades", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SalaryGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalaryGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubDepartments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DepartmentId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubDepartments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubDepartments_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "BankBranches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BankId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankBranches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankBranches_Banks_BankId",
                        column: x => x.BankId,
                        principalTable: "Banks",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Payslips",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PayrollPeriodId = table.Column<int>(type: "int", nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    WorkingDays = table.Column<int>(type: "int", nullable: false),
                    PresentDays = table.Column<int>(type: "int", nullable: false),
                    LeaveDays = table.Column<int>(type: "int", nullable: false),
                    NoPayDays = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OvertimeHours = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BasicSalary = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NoPayDeduction = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalFixedAllowances = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalVariableAllowances = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OvertimeAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GrossPay = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EpfLiableEarnings = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EmployeeEpf = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EmployerEpf = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EmployerEtf = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ApitLiableEarnings = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Apit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalOtherDeductions = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalDeductions = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NetPay = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CostToCompany = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BankName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    BankBranchName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    AccountNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    EpfNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payslips", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payslips_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Payslips_PayrollPeriods_PayrollPeriodId",
                        column: x => x.PayrollPeriodId,
                        principalTable: "PayrollPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeSalaryComponents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    SalaryComponentId = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeSalaryComponents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeSalaryComponents_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EmployeeSalaryComponents_SalaryComponents_SalaryComponentId",
                        column: x => x.SalaryComponentId,
                        principalTable: "SalaryComponents",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EmployeePayrollInfos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    EpfNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    EtfNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    IsEpfMember = table.Column<bool>(type: "bit", nullable: false),
                    IsEtfMember = table.Column<bool>(type: "bit", nullable: false),
                    IsApitApplicable = table.Column<bool>(type: "bit", nullable: false),
                    SalaryGradeId = table.Column<int>(type: "int", nullable: true),
                    SalaryGroupId = table.Column<int>(type: "int", nullable: true),
                    SubDepartmentId = table.Column<int>(type: "int", nullable: true),
                    BankBranchId = table.Column<int>(type: "int", nullable: true),
                    AccountNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    AccountName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    IsBankTransfer = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeePayrollInfos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeePayrollInfos_BankBranches_BankBranchId",
                        column: x => x.BankBranchId,
                        principalTable: "BankBranches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EmployeePayrollInfos_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EmployeePayrollInfos_SalaryGrades_SalaryGradeId",
                        column: x => x.SalaryGradeId,
                        principalTable: "SalaryGrades",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EmployeePayrollInfos_SalaryGroups_SalaryGroupId",
                        column: x => x.SalaryGroupId,
                        principalTable: "SalaryGroups",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EmployeePayrollInfos_SubDepartments_SubDepartmentId",
                        column: x => x.SubDepartmentId,
                        principalTable: "SubDepartments",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PayslipLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PayslipId = table.Column<int>(type: "int", nullable: false),
                    SalaryComponentId = table.Column<int>(type: "int", nullable: true),
                    ComponentName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ComponentCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ComponentType = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsFixed = table.Column<bool>(type: "bit", nullable: false),
                    IsEpfLiable = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayslipLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayslipLines_Payslips_PayslipId",
                        column: x => x.PayslipId,
                        principalTable: "Payslips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PayslipLines_SalaryComponents_SalaryComponentId",
                        column: x => x.SalaryComponentId,
                        principalTable: "SalaryComponents",
                        principalColumn: "Id");
                });

            migrationBuilder.UpdateData(
                table: "Branches",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "EpfEmployerNumber", "EtfEmployerNumber" },
                values: new object[] { null, null });

            migrationBuilder.CreateIndex(
                name: "IX_ApitTaxBrackets_EffectiveFrom_SortOrder",
                table: "ApitTaxBrackets",
                columns: new[] { "EffectiveFrom", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_BankBranches_BankId_Code",
                table: "BankBranches",
                columns: new[] { "BankId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Banks_Code",
                table: "Banks",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePayrollInfos_BankBranchId",
                table: "EmployeePayrollInfos",
                column: "BankBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePayrollInfos_EmployeeId",
                table: "EmployeePayrollInfos",
                column: "EmployeeId",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePayrollInfos_SalaryGradeId",
                table: "EmployeePayrollInfos",
                column: "SalaryGradeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePayrollInfos_SalaryGroupId",
                table: "EmployeePayrollInfos",
                column: "SalaryGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePayrollInfos_SubDepartmentId",
                table: "EmployeePayrollInfos",
                column: "SubDepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeSalaryComponents_EmployeeId_SalaryComponentId_EffectiveFrom",
                table: "EmployeeSalaryComponents",
                columns: new[] { "EmployeeId", "SalaryComponentId", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeSalaryComponents_SalaryComponentId",
                table: "EmployeeSalaryComponents",
                column: "SalaryComponentId");

            migrationBuilder.CreateIndex(
                name: "IX_EpfEtfRates_EffectiveFrom",
                table: "EpfEtfRates",
                column: "EffectiveFrom");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPeriods_BranchId",
                table: "PayrollPeriods",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPeriods_Year_Month_BranchId",
                table: "PayrollPeriods",
                columns: new[] { "Year", "Month", "BranchId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PayslipLines_PayslipId",
                table: "PayslipLines",
                column: "PayslipId");

            migrationBuilder.CreateIndex(
                name: "IX_PayslipLines_SalaryComponentId",
                table: "PayslipLines",
                column: "SalaryComponentId");

            migrationBuilder.CreateIndex(
                name: "IX_Payslips_EmployeeId",
                table: "Payslips",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Payslips_PayrollPeriodId_EmployeeId",
                table: "Payslips",
                columns: new[] { "PayrollPeriodId", "EmployeeId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryComponents_Code",
                table: "SalaryComponents",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryGrades_Code",
                table: "SalaryGrades",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_SubDepartments_DepartmentId_Name",
                table: "SubDepartments",
                columns: new[] { "DepartmentId", "Name" },
                unique: true,
                filter: "[IsDeleted] = 0");

            // ── Permissions ───────────────────────────────────────────────────
            //
            // Inserted by lookup with identity ids, and deliberately NOT added to
            // PermissionCatalogue. That catalogue assigns ids by list position through
            // HasData; this database's permission ids came from a different seeder and are
            // offset from those positions, so a positional insert collides with an existing
            // row — which is exactly how the previous payroll migration failed at startup.
            //
            // Doing it here covers both cases: migrations run on a new database too, so the
            // rows exist either way, and there is one place they come from.
            migrationBuilder.Sql(@"
MERGE [Permissions] AS target
USING (VALUES
    ('Payroll','View','View Payroll'),
    ('Payroll','Create','Create Payroll'),
    ('Payroll','Edit','Edit Payroll'),
    ('Payroll','Delete','Delete Payroll'),
    ('Payroll','Approve','Approve Payroll'),
    ('Payroll','Export','Export Payroll'),
    ('PayrollSetup','View','View PayrollSetup'),
    ('PayrollSetup','Create','Create PayrollSetup'),
    ('PayrollSetup','Edit','Edit PayrollSetup'),
    ('PayrollSetup','Delete','Delete PayrollSetup')
) AS source ([Module],[Action],[DisplayName])
    ON target.[Module] = source.[Module] AND target.[Action] = source.[Action]
WHEN NOT MATCHED THEN
    INSERT ([Module],[Action],[DisplayName],[IsDeleted],[CreatedAt])
    VALUES (source.[Module], source.[Action], source.[DisplayName], 0, SYSDATETIME());

-- Granted to Administrator (role 1) only. Every other role is configured on the Roles
-- screen: guessing who may see salaries is not a decision a migration should make.
INSERT INTO [RolePermissions] ([RoleId],[PermissionId])
SELECT 1, p.[Id]
FROM [Permissions] p
WHERE p.[Module] IN ('Payroll','PayrollSetup')
  AND NOT EXISTS (SELECT 1 FROM [RolePermissions] rp
                  WHERE rp.[RoleId] = 1 AND rp.[PermissionId] = p.[Id]);
");

            // ── Statutory defaults ────────────────────────────────────────────
            //
            // The rates in force since well before this system existed. Seeded so a fresh
            // install calculates correctly rather than producing zero contributions until
            // somebody notices; superseded by adding a row with a later EffectiveFrom.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM [EpfEtfRates] WHERE [IsDeleted] = 0)
    INSERT INTO [EpfEtfRates]
        ([EffectiveFrom],[EmployeeEpfPercent],[EmployerEpfPercent],[EmployerEtfPercent],[Notes],[IsDeleted],[CreatedAt])
    VALUES ('2020-01-01', 8.00, 12.00, 3.00, 'Statutory minimum rates', 0, SYSDATETIME());
");

            // APIT monthly table. Held as data because the bands change with each budget —
            // a payslip reissued for an earlier month must use the table of its own month.
            // Verify these against the current IRD gazette before the first live run.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM [ApitTaxBrackets] WHERE [IsDeleted] = 0)
    INSERT INTO [ApitTaxBrackets]
        ([EffectiveFrom],[FromAmount],[ToAmount],[Rate],[Relief],[SortOrder],[IsDeleted],[CreatedAt])
    VALUES
        ('2025-04-01',      0.00, 150000.00,  0.00,      0.00, 1, 0, SYSDATETIME()),
        ('2025-04-01', 150000.00, 233333.00,  6.00,   9000.00, 2, 0, SYSDATETIME()),
        ('2025-04-01', 233333.00, 275000.00, 18.00,  37000.00, 3, 0, SYSDATETIME()),
        ('2025-04-01', 275000.00, 316667.00, 24.00,  53500.00, 4, 0, SYSDATETIME()),
        ('2025-04-01', 316667.00, 358333.00, 30.00,  72500.00, 5, 0, SYSDATETIME()),
        ('2025-04-01', 358333.00,      NULL, 36.00,  94000.00, 6, 0, SYSDATETIME());
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApitTaxBrackets");

            migrationBuilder.DropTable(
                name: "EmployeePayrollInfos");

            migrationBuilder.DropTable(
                name: "EmployeeSalaryComponents");

            migrationBuilder.DropTable(
                name: "EpfEtfRates");

            migrationBuilder.DropTable(
                name: "PayslipLines");

            migrationBuilder.DropTable(
                name: "BankBranches");

            migrationBuilder.DropTable(
                name: "SalaryGrades");

            migrationBuilder.DropTable(
                name: "SalaryGroups");

            migrationBuilder.DropTable(
                name: "SubDepartments");

            migrationBuilder.DropTable(
                name: "Payslips");

            migrationBuilder.DropTable(
                name: "SalaryComponents");

            migrationBuilder.DropTable(
                name: "Banks");

            migrationBuilder.DropTable(
                name: "PayrollPeriods");

            migrationBuilder.DropColumn(
                name: "EpfEmployerNumber",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "EtfEmployerNumber",
                table: "Branches");
        }
    }
}
