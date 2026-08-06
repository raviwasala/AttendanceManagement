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
