using AttendanceSystem.Domain.Entities;

namespace AttendanceSystem.Domain.Interfaces;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByUsernameAsync(string username);
    Task<User?> GetByEmailAsync(string email);
    Task<bool> IsUsernameTakenAsync(string username, int? excludeId = null);
    Task IncrementFailedLoginAsync(int userId);
    Task ResetFailedLoginAsync(int userId);
    Task LockUserAsync(int userId);
    Task UnlockUserAsync(int userId);
}

public interface IEmployeeRepository : IRepository<Employee>
{
    Task<Employee?> GetWithDetailsAsync(int id);
    Task<IEnumerable<Employee>> GetActiveEmployeesAsync();
    Task<bool> IsCodeTakenAsync(string code, int? excludeId = null);
    Task<string> GenerateNextCodeAsync();
    Task<IEnumerable<Employee>> SearchAsync(string keyword);
}

public interface IAttendanceRepository : IRepository<AttendanceLog>
{
    Task<AttendanceLog?> GetTodayAttendanceAsync(int employeeId, DateTime date);
    Task<IEnumerable<AttendanceLog>> GetByEmployeeAndDateRangeAsync(int employeeId, DateTime from, DateTime to);
    Task<IEnumerable<AttendanceLog>> GetByDateAsync(DateTime date);
    Task<int> GetPresentCountTodayAsync(DateTime date);
    Task<int> GetAbsentCountTodayAsync(DateTime date, int totalEmployees);
    Task<int> GetLateCountTodayAsync(DateTime date);
}

public interface ILeaveRepository : IRepository<LeaveRequest>
{
    Task<IEnumerable<LeaveRequest>> GetByEmployeeAsync(int employeeId);
    Task<IEnumerable<LeaveRequest>> GetPendingAsync();
    Task<int> GetUsedLeaveDaysAsync(int employeeId, int leaveTypeId, int year);
}

public interface IHolidayRepository : IRepository<Holiday>
{
    Task<bool> IsHolidayAsync(DateTime date);
    Task<IEnumerable<Holiday>> GetByYearAsync(int year);
}

public interface IAuditLogRepository
{
    Task AddAsync(AuditLog log);
    Task<IEnumerable<AuditLog>> GetRecentAsync(int count = 100);
    Task<IEnumerable<AuditLog>> GetByUserAsync(int userId);
    Task<IEnumerable<AuditLog>> GetByModuleAsync(string module);
}
