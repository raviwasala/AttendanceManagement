using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Domain.Interfaces;
using AttendanceSystem.Infrastructure.Data;
using AttendanceSystem.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;


namespace AttendanceSystem.Infrastructure.UnitOfWork;

/// <summary>Unit of Work implementation — wraps all repositories in one transaction scope.</summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly AttendanceDbContext _context;

    private IUserRepository? _users;
    private IEmployeeRepository? _employees;
    private IAttendanceRepository? _attendance;
    private ILeaveRepository? _leaves;
    private IHolidayRepository? _holidays;
    private IAuditLogRepository? _auditLogs;

    private IRepository<Department>? _departments;
    private IRepository<DepartmentApprover>? _departmentApprovers;
    private IRepository<Designation>? _designations;

    // Payroll. Lazily constructed like the rest, so a request that never touches payroll
    // pays nothing for it.
    private IRepository<Bank>? _banks;
    private IRepository<BankBranch>? _bankBranches;
    private IRepository<SalaryGrade>? _salaryGrades;
    private IRepository<SalaryGroup>? _salaryGroups;
    private IRepository<SubDepartment>? _subDepartments;
    private IRepository<SalaryComponent>? _salaryComponents;
    private IRepository<EmployeeSalaryComponent>? _employeeSalaryComponents;
    private IRepository<MonthlyTransaction>? _monthlyTransactions;
    private IRepository<EmployeePayrollInfo>? _employeePayrollInfos;
    private IRepository<EpfEtfRate>? _epfEtfRates;
    private IRepository<ApitTaxBracket>? _apitTaxBrackets;
    private IRepository<PayrollPeriod>? _payrollPeriods;
    private IRepository<Payslip>? _payslips;
    private IRepository<PayslipLine>? _payslipLines;

    public IRepository<Bank> Banks => _banks ??= new Repository<Bank>(_context);
    public IRepository<BankBranch> BankBranches => _bankBranches ??= new Repository<BankBranch>(_context);
    public IRepository<SalaryGrade> SalaryGrades => _salaryGrades ??= new Repository<SalaryGrade>(_context);
    public IRepository<SalaryGroup> SalaryGroups => _salaryGroups ??= new Repository<SalaryGroup>(_context);
    public IRepository<SubDepartment> SubDepartments => _subDepartments ??= new Repository<SubDepartment>(_context);
    public IRepository<SalaryComponent> SalaryComponents => _salaryComponents ??= new Repository<SalaryComponent>(_context);
    public IRepository<EmployeeSalaryComponent> EmployeeSalaryComponents => _employeeSalaryComponents ??= new Repository<EmployeeSalaryComponent>(_context);
    public IRepository<MonthlyTransaction> MonthlyTransactions => _monthlyTransactions ??= new Repository<MonthlyTransaction>(_context);
    public IRepository<EmployeePayrollInfo> EmployeePayrollInfos => _employeePayrollInfos ??= new Repository<EmployeePayrollInfo>(_context);
    public IRepository<EpfEtfRate> EpfEtfRates => _epfEtfRates ??= new Repository<EpfEtfRate>(_context);
    public IRepository<ApitTaxBracket> ApitTaxBrackets => _apitTaxBrackets ??= new Repository<ApitTaxBracket>(_context);

    private IRepository<ApitTaxTable>? _apitTaxTables;
    private IRepository<EmploymentCategory>? _employmentCategories;
    public IRepository<ApitTaxTable> ApitTaxTables => _apitTaxTables ??= new Repository<ApitTaxTable>(_context);
    public IRepository<EmploymentCategory> EmploymentCategories => _employmentCategories ??= new Repository<EmploymentCategory>(_context);

    private IRepository<LoanType>? _loanTypes;
    public IRepository<LoanType> LoanTypes => _loanTypes ??= new Repository<LoanType>(_context);

    private IRepository<ThirdParty>? _thirdParties;
    public IRepository<ThirdParty> ThirdParties => _thirdParties ??= new Repository<ThirdParty>(_context);

    private IRepository<BranchPayrollSettings>? _branchPayrollSettings;
    public IRepository<BranchPayrollSettings> BranchPayrollSettings => _branchPayrollSettings ??= new Repository<BranchPayrollSettings>(_context);

    private IRepository<EpfAdjustment>? _epfAdjustments;
    private IRepository<EmployeeLeaveEntitlement>? _employeeLeaveEntitlements;
    public IRepository<EpfAdjustment> EpfAdjustments => _epfAdjustments ??= new Repository<EpfAdjustment>(_context);
    public IRepository<EmployeeLeaveEntitlement> EmployeeLeaveEntitlements => _employeeLeaveEntitlements ??= new Repository<EmployeeLeaveEntitlement>(_context);

    private IRepository<EmployeeLoan>? _employeeLoans;
    private IRepository<LoanGuarantor>? _loanGuarantors;
    private IRepository<LoanTransaction>? _loanTransactions;
    public IRepository<EmployeeLoan> EmployeeLoans => _employeeLoans ??= new Repository<EmployeeLoan>(_context);
    public IRepository<LoanGuarantor> LoanGuarantors => _loanGuarantors ??= new Repository<LoanGuarantor>(_context);
    public IRepository<LoanTransaction> LoanTransactions => _loanTransactions ??= new Repository<LoanTransaction>(_context);
    public IRepository<PayrollPeriod> PayrollPeriods => _payrollPeriods ??= new Repository<PayrollPeriod>(_context);
    public IRepository<Payslip> Payslips => _payslips ??= new Repository<Payslip>(_context);
    public IRepository<PayslipLine> PayslipLines => _payslipLines ??= new Repository<PayslipLine>(_context);
    private IRepository<Branch>? _branches;
    private IRepository<Shift>? _shifts;
    private IRepository<EmployeeShift>? _employeeShifts;
    private IRepository<LeaveType>? _leaveTypes;
    private IRepository<Role>? _roles;
    private IRepository<Permission>? _permissions;
    private IRepository<AttendanceSummary>? _attendanceSummaries;
    private IRepository<CompanySettings>? _companySettings;
    private IRepository<Device>? _devices;
    private IRepository<DeviceUserMapping>? _deviceUserMappings;
    private IRepository<OvertimeRule>? _overtimeRules;
    private IOvertimeRecordRepository? _overtimeRecords;

    public UnitOfWork(AttendanceDbContext context) => _context = context;

    public IUserRepository Users         => _users         ??= new UserRepository(_context);
    public IEmployeeRepository Employees => _employees     ??= new EmployeeRepository(_context);
    public IAttendanceRepository Attendance => _attendance ??= new AttendanceRepository(_context);
    public ILeaveRepository Leaves       => _leaves        ??= new LeaveRepository(_context);
    public IHolidayRepository Holidays   => _holidays      ??= new HolidayRepository(_context);
    public IAuditLogRepository AuditLogs => _auditLogs     ??= new AuditLogRepository(_context);

    public IRepository<Department>       Departments       => _departments       ??= new Repository<Department>(_context);
    public IRepository<DepartmentApprover> DepartmentApprovers => _departmentApprovers ??= new Repository<DepartmentApprover>(_context);
    public IRepository<Designation>      Designations      => _designations      ??= new Repository<Designation>(_context);
    public IRepository<Branch>           Branches          => _branches          ??= new Repository<Branch>(_context);
    public IRepository<Shift>            Shifts            => _shifts            ??= new Repository<Shift>(_context);
    public IRepository<EmployeeShift>    EmployeeShifts    => _employeeShifts    ??= new Repository<EmployeeShift>(_context);
    public IRepository<LeaveType>        LeaveTypes        => _leaveTypes        ??= new Repository<LeaveType>(_context);
    public IRepository<Role>             Roles             => _roles             ??= new Repository<Role>(_context);
    public IRepository<Permission>       Permissions       => _permissions       ??= new Repository<Permission>(_context);
    public IRepository<AttendanceSummary> AttendanceSummaries => _attendanceSummaries ??= new Repository<AttendanceSummary>(_context);
    public IRepository<CompanySettings>  CompanySettings   => _companySettings   ??= new Repository<CompanySettings>(_context);
    public IRepository<Device>           Devices           => _devices           ??= new Repository<Device>(_context);
    public IRepository<DeviceUserMapping> DeviceUserMappings => _deviceUserMappings ??= new Repository<DeviceUserMapping>(_context);
    public IRepository<OvertimeRule>     OvertimeRules     => _overtimeRules     ??= new Repository<OvertimeRule>(_context);
    public IOvertimeRecordRepository     OvertimeRecords   => _overtimeRecords   ??= new OvertimeRecordRepository(_context);

    public async Task<IEnumerable<RolePermission>> GetRolePermissionsAsync(int roleId) =>
        await _context.RolePermissions.Where(rp => rp.RoleId == roleId).ToListAsync();

    /// <summary>
    /// Stages permission replacements for a role.
    /// Does NOT call SaveChangesAsync — the caller must call SaveChangesAsync to commit atomically.
    /// </summary>
    public Task SavePermissionsAsync(int roleId, IEnumerable<int> permissionIds)
    {
        var existing = _context.RolePermissions.Where(rp => rp.RoleId == roleId);
        _context.RolePermissions.RemoveRange(existing);
        foreach (var pid in permissionIds)
            _context.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = pid });
        return Task.CompletedTask;
    }

    public async Task<int> SaveChangesAsync() => await _context.SaveChangesAsync();

    public void Dispose() => _context.Dispose();

    public ValueTask DisposeAsync() => _context.DisposeAsync();
}
