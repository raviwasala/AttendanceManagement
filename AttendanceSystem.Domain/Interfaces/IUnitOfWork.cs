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

    /// <summary>Returns all RolePermission records for a given role (join table query).</summary>
    Task<IEnumerable<RolePermission>> GetRolePermissionsAsync(int roleId);

    /// <summary>Replaces all permissions for a role atomically. Changes are staged — call SaveChangesAsync to commit.</summary>
    Task SavePermissionsAsync(int roleId, IEnumerable<int> permissionIds);

    Task<int> SaveChangesAsync();
}
