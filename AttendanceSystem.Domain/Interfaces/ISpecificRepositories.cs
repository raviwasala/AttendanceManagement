using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Domain.Enums;

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

    /// <summary>
    /// One page of employees with their lookups included, filtered in SQL.
    ///
    /// isActive is nullable on purpose. The list screen offers Active / Inactive / All, and the
    /// old GetActiveEmployeesAsync could only ever answer the first — which is why choosing
    /// "Inactive" returned nothing at all.
    /// </summary>
    Task<(IEnumerable<Employee> Items, int TotalCount)> GetPagedAsync(
        string? search, int? departmentId, int? designationId, int? branchId,
        bool? isActive, int skip, int take);
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

    /// <summary>
    /// One page of requests, newest first, with employee, department and leave type included.
    ///
    /// The generic GetAllAsync this replaced included none of those navigations, so every
    /// request came back with a blank employee name and department.
    /// </summary>
    Task<(IEnumerable<LeaveRequest> Items, int TotalCount)> GetPagedAsync(
        string? search, LeaveStatus? status, int? departmentId, int? employeeId,
        DateTime? from, DateTime? to, int skip, int take);
}

/// <summary>Totals for a filtered set of overtime claims, computed in SQL over the whole range.</summary>
public record OvertimeTotals(
    int TotalCount,
    int PendingCount,
    int ApprovedCount,
    int RejectedCount,
    int ClaimedMinutes,
    int ApprovedMinutes,
    decimal WeightedHours,
    /// <summary>What the claimed minutes would be worth if every one were approved.</summary>
    decimal ClaimedWeightedHours);

public interface IOvertimeRecordRepository : IRepository<OvertimeRecord>
{
    /// <summary>
    /// One page of claims plus the totals for the entire filtered range.
    ///
    /// The two are separate on purpose: the header tiles must describe the whole range, not
    /// whatever happens to be on page 3. Both run against the same predicate so they cannot
    /// disagree, and neither pulls rows the caller will not use.
    /// </summary>
    Task<(IEnumerable<OvertimeRecord> Items, OvertimeTotals Totals)> GetRegisterPageAsync(
        DateTime from, DateTime to, int? employeeId, IReadOnlyCollection<int>? employeeIds,
        OvertimeStatus? status, int skip, int take);
}

public interface IHolidayRepository : IRepository<Holiday>
{
    Task<bool> IsHolidayAsync(DateTime date);
    Task<IEnumerable<Holiday>> GetByYearAsync(int year);
}

/// <summary>
/// Audit log repository — standalone interface because AuditLog is an append-only entity
/// that does not inherit BaseEntity (no soft-delete, no ModifiedAt).
/// Writes are staged via AddAsync and must be committed by the Unit of Work.
/// </summary>
public interface IAuditLogRepository
{
    Task AddAsync(AuditLog log);
    Task<IEnumerable<AuditLog>> GetRecentAsync(int count = 100);
    Task<IEnumerable<AuditLog>> GetByUserAsync(int userId);
    Task<IEnumerable<AuditLog>> GetByModuleAsync(string module, int count = 100);

    /// <summary>
    /// One page of the trail, newest first, with the matching total.
    ///
    /// This table only ever grows, so it is the one list that must never be fetched whole —
    /// the search term is applied in SQL rather than in the browser for the same reason.
    /// </summary>
    Task<(IEnumerable<AuditLog> Items, int TotalCount)> GetPagedAsync(
        string? module, string? search, int skip, int take);
}
