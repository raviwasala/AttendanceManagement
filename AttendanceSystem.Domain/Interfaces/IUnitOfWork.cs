using AttendanceSystem.Domain.Entities;

namespace AttendanceSystem.Domain.Interfaces;

/// <summary>Unit of Work pattern contract.</summary>
public interface IUnitOfWork : IDisposable, IAsyncDisposable
{
    IUserRepository Users { get; }
    IEmployeeRepository Employees { get; }
    IAttendanceRepository Attendance { get; }
    ILeaveRepository Leaves { get; }
    IHolidayRepository Holidays { get; }
    IAuditLogRepository AuditLogs { get; }

    // Generic repositories for lookup entities
    IRepository<Department> Departments { get; }

    /// <summary>Users named as leave approvers for a department. See <c>LeaveApprovalScope</c>.</summary>
    IRepository<DepartmentApprover> DepartmentApprovers { get; }
    IRepository<Designation> Designations { get; }
    IRepository<Branch> Branches { get; }
    IRepository<Shift> Shifts { get; }
    IRepository<EmployeeShift> EmployeeShifts { get; }
    IRepository<LeaveType> LeaveTypes { get; }
    IRepository<Role> Roles { get; }
    IRepository<Permission> Permissions { get; }
    IRepository<AttendanceSummary> AttendanceSummaries { get; }
    IRepository<CompanySettings> CompanySettings { get; }

    // Fingerprint device integration
    IRepository<Device> Devices { get; }
    IRepository<DeviceUserMapping> DeviceUserMappings { get; }

    // Overtime
    IRepository<OvertimeRule> OvertimeRules { get; }
    IOvertimeRecordRepository OvertimeRecords { get; }

    // ── Payroll ────────────────────────────────────────────────────────────────
    IRepository<Bank> Banks { get; }
    IRepository<BankBranch> BankBranches { get; }
    IRepository<SalaryGrade> SalaryGrades { get; }
    IRepository<SalaryGroup> SalaryGroups { get; }
    IRepository<SubDepartment> SubDepartments { get; }
    IRepository<SalaryComponent> SalaryComponents { get; }
    IRepository<EmployeeSalaryComponent> EmployeeSalaryComponents { get; }
    IRepository<MonthlyTransaction> MonthlyTransactions { get; }
    IRepository<SalaryIncrement> SalaryIncrements { get; }
    IRepository<EmployeePayrollInfo> EmployeePayrollInfos { get; }
    IRepository<EpfEtfRate> EpfEtfRates { get; }
    IRepository<ApitTaxTable> ApitTaxTables { get; }
    IRepository<ApitTaxBracket> ApitTaxBrackets { get; }
    IRepository<EmploymentCategory> EmploymentCategories { get; }
    IRepository<LoanType> LoanTypes { get; }
    IRepository<ThirdParty> ThirdParties { get; }
    IRepository<BranchPayrollSettings> BranchPayrollSettings { get; }
    IRepository<EpfAdjustment> EpfAdjustments { get; }
    IRepository<EmployeeLeaveEntitlement> EmployeeLeaveEntitlements { get; }
    IRepository<EmployeeLoan> EmployeeLoans { get; }
    IRepository<LoanGuarantor> LoanGuarantors { get; }
    IRepository<LoanTransaction> LoanTransactions { get; }
    IRepository<PayrollPeriod> PayrollPeriods { get; }
    IRepository<Payslip> Payslips { get; }
    IRepository<PayslipLine> PayslipLines { get; }

    /// <summary>Returns all RolePermission records for a given role (join table query).</summary>
    Task<IEnumerable<RolePermission>> GetRolePermissionsAsync(int roleId);

    /// <summary>Replaces all permissions for a role atomically. Changes are staged — call SaveChangesAsync to commit.</summary>
    Task SavePermissionsAsync(int roleId, IEnumerable<int> permissionIds);

    Task<int> SaveChangesAsync();
}
