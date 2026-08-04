using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Common.Models;

namespace AttendanceSystem.Application.Interfaces;

/// <summary>Authentication service contract.</summary>
public interface IAuthService
{
    Task<Result<AuthResultDto>> LoginAsync(LoginDto dto);
    Task<Result> ChangePasswordAsync(int userId, ChangePasswordDto dto);
    Task LogoutAsync(int userId);
    Task<Result> RequestPasswordResetAsync(ForgotPasswordDto dto, string baseUrl);
    Task<Result> ResetPasswordWithTokenAsync(ResetPasswordWithTokenDto dto);
    Task<Result<AuthResultDto>> ValidateRememberTokenAsync(string username, string token);

    /// <summary>Invalidates the user's stored remember-me token (used on explicit sign-out).</summary>
    Task<Result> RevokeRememberTokenAsync(int userId);
}

/// <summary>User management service contract.</summary>
public interface IUserService
{
    Task<Result<IEnumerable<UserDto>>> GetAllAsync();
    Task<Result<UserDto>> GetByIdAsync(int id);
    Task<Result<UserDto>> CreateAsync(CreateUserDto dto);
    Task<Result> UpdateAsync(UpdateUserDto dto);
    Task<Result> DeleteAsync(int id, int deletedBy);
    Task<Result> ResetPasswordAsync(int userId, string newPassword, int resetBy);
    Task<Result> LockAsync(int userId);
    Task<Result> UnlockAsync(int userId);
}

/// <summary>Role service contract.</summary>
public interface IRoleService
{
    Task<Result<IEnumerable<RoleDto>>> GetAllAsync();
    Task<Result<RoleDto>> GetByIdAsync(int id);
    Task<Result<RoleDto>> SaveAsync(RoleDto dto);
    Task<Result> DeleteAsync(int id);
    Task<Result<IEnumerable<PermissionDto>>> GetPermissionsForRoleAsync(int roleId);
    Task<Result> SavePermissionsAsync(int roleId, IEnumerable<int> permissionIds);
}

/// <summary>Department service contract.</summary>
public interface IDepartmentService
{
    Task<Result<IEnumerable<DepartmentDto>>> GetAllAsync();
    Task<Result<DepartmentDto>> GetByIdAsync(int id);
    Task<Result<DepartmentDto>> SaveAsync(SaveDepartmentDto dto);
    Task<Result> DeleteAsync(int id, int deletedBy);
    Task<Result<IEnumerable<DepartmentDto>>> SearchAsync(string keyword);
}

/// <summary>Designation service contract.</summary>
public interface IDesignationService
{
    Task<Result<IEnumerable<DesignationDto>>> GetAllAsync();
    Task<Result<DesignationDto>> GetByIdAsync(int id);
    Task<Result<DesignationDto>> SaveAsync(SaveDesignationDto dto);
    Task<Result> DeleteAsync(int id, int deletedBy);
}

/// <summary>Branch service contract.</summary>
public interface IBranchService
{
    Task<Result<IEnumerable<BranchDto>>> GetAllAsync();
    Task<Result<BranchDto>> GetByIdAsync(int id);
    Task<Result<BranchDto>> SaveAsync(SaveBranchDto dto);
    Task<Result> DeleteAsync(int id, int deletedBy);
}

/// <summary>Employee service contract.</summary>
public interface IEmployeeService
{
    Task<Result<IEnumerable<EmployeeListItemDto>>> GetAllAsync();
    Task<Result<EmployeeDto>> GetByIdAsync(int id);
    Task<Result<EmployeeDto>> SaveAsync(SaveEmployeeDto dto);
    Task<Result> DeleteAsync(int id, int deletedBy);
    Task<Result<IEnumerable<EmployeeListItemDto>>> SearchAsync(string keyword);
    Task<Result> ToggleActiveAsync(int id, int modifiedBy);
}

/// <summary>Shift service contract.</summary>
public interface IShiftService
{
    Task<Result<IEnumerable<ShiftDto>>> GetAllAsync();
    Task<Result<ShiftDto>> GetByIdAsync(int id);
    Task<Result<ShiftDto>> SaveAsync(SaveShiftDto dto);
    Task<Result> DeleteAsync(int id, int deletedBy);
    Task<Result<IEnumerable<EmployeeShiftDto>>> GetEmployeeShiftsAsync();
    Task<Result> AssignShiftAsync(AssignShiftDto dto);
}

/// <summary>Attendance service contract.</summary>
public interface IAttendanceService
{
    Task<Result<AttendanceLogDto>> CheckInAsync(CheckInDto dto);
    Task<Result<AttendanceLogDto>> CheckOutAsync(CheckOutDto dto);
    Task<Result> EditAsync(EditAttendanceDto dto, int modifiedBy);
    Task<Result> DeleteAsync(int id, int deletedBy);
    Task<Result<IEnumerable<AttendanceLogDto>>> GetTodayAsync();
    Task<Result<IEnumerable<AttendanceLogDto>>> GetByEmployeeAndDateRangeAsync(int employeeId, DateTime from, DateTime to);
    Task<Result<IEnumerable<AttendanceSummaryDto>>> GetMonthlySummaryAsync(int month, int year);
    Task<Result<DashboardStatsDto>> GetDashboardStatsAsync();
}

/// <summary>Leave service contract.</summary>
public interface ILeaveService
{
    Task<Result<IEnumerable<LeaveTypeDto>>> GetLeaveTypesAsync();
    Task<Result<LeaveTypeDto>> SaveLeaveTypeAsync(SaveLeaveTypeDto dto);
    Task<Result> DeleteLeaveTypeAsync(int id);
    Task<Result<IEnumerable<LeaveRequestDto>>> GetAllRequestsAsync();
    Task<Result<IEnumerable<LeaveRequestDto>>> GetByEmployeeAsync(int employeeId);
    Task<Result<IEnumerable<LeaveRequestDto>>> GetPendingAsync();
    Task<Result<LeaveRequestDto>> ApplyLeaveAsync(ApplyLeaveDto dto);
    Task<Result> ApproveRejectAsync(ApproveRejectLeaveDto dto, int actionBy);
    Task<Result> CancelAsync(int leaveRequestId, int cancelledBy);
    Task<Result<IEnumerable<LeaveBalanceDto>>> GetBalancesAsync(int employeeId);
}

/// <summary>Holiday service contract.</summary>
public interface IHolidayService
{
    Task<Result<IEnumerable<HolidayDto>>> GetAllAsync();
    Task<Result<IEnumerable<HolidayDto>>> GetByYearAsync(int year);
    Task<Result<HolidayDto>> SaveAsync(SaveHolidayDto dto);
    Task<Result> DeleteAsync(int id, int deletedBy);
}

/// <summary>Settings service contract.</summary>
public interface ISettingsService
{
    Task<Result<CompanySettingsDto>> GetAsync();
    Task<Result> SaveAsync(CompanySettingsDto dto, int modifiedBy);
}

/// <summary>Audit log service contract.</summary>
public interface IAuditService
{
    Task LogAsync(string module, string action, int? userId = null,
        string? entityName = null, int? entityId = null,
        string? oldValues = null, string? newValues = null);
    Task<Result<IEnumerable<AuditLogDto>>> GetRecentAsync(int count = 100);
    Task<Result<IEnumerable<AuditLogDto>>> GetByModuleAsync(string module);
}

/// <summary>
/// Daily attendance review: rostered shift next to what the device recorded, with the
/// in/out times correctable. Corrections re-derive late, early-leave, hours and status
/// from the shift in force on that date.
/// </summary>
public interface IAttendanceReviewService
{
    /// <summary>Convenience wrapper for a single date, all employees.</summary>
    Task<Result<AttendanceReviewDto>> GetDailyReviewAsync(DateTime date, int? departmentId = null);

    /// <summary>
    /// Review across a date range. Serves both "everyone today" and "one employee this
    /// month" — pass <paramref name="employeeId"/> to focus on one person.
    /// </summary>
    Task<Result<AttendanceReviewDto>> GetReviewAsync(
        DateTime fromDate, DateTime toDate, int? employeeId = null, int? departmentId = null);

    /// <summary>
    /// Creates, updates or clears one employee's entry for one date, then recalculates.
    /// Returns the refreshed row so the grid can update without a full reload.
    /// </summary>
    Task<Result<AttendanceReviewRowDto>> SaveEntryAsync(SaveAttendanceEntryDto dto);
}

/// <summary>
/// Monthly shift roster — which shift each employee works on each day, and per-day changes.
///
/// Built on the existing EmployeeShift assignments rather than a new table: a single-day
/// assignment (EffectiveFrom == EffectiveTo) outranks a longer-running one on that date,
/// which is exactly how AttendanceService already resolves shifts.
/// </summary>
public interface IShiftRosterService
{
    Task<Result<ShiftRosterDto>> GetMonthlyRosterAsync(int year, int month, int? departmentId = null);

    /// <summary>Sets one day's shift, or clears the override when ShiftId is null.</summary>
    Task<Result> SetDayAsync(SetRosterDayDto dto);

    /// <summary>Applies one shift across a date range, committed as a single unit.</summary>
    Task<Result> SetRangeAsync(SetRosterRangeDto dto);
}

/// <summary>
/// Fingerprint device registry: CRUD, branch assignment, connectivity and status.
///
/// Deliberately excludes synchronisation — that belongs to IDeviceSyncService (phase 4), so
/// this stays a management surface with no dependency on the wire protocol beyond a ping.
/// </summary>
public interface IDeviceService
{
    Task<Result<IEnumerable<DeviceDto>>> GetAllAsync();
    Task<Result<DeviceDto>> GetByIdAsync(int id);
    Task<Result<IEnumerable<DeviceDto>>> GetByBranchAsync(int branchId);
    Task<Result<DeviceDto>> SaveAsync(SaveDeviceDto dto);
    Task<Result> DeleteAsync(int id);

    /// <summary>Contacts the device now and records the outcome against its status.</summary>
    Task<Result<DeviceTestResultDto>> TestConnectionAsync(int id, CancellationToken ct = default);
}

/// <summary>
/// Read-only dashboard analytics. Separate from <see cref="IAttendanceService"/> because these
/// are aggregate read models over several entities, with no write side.
/// </summary>
public interface IAnalyticsService
{
    /// <summary>Daily attendance breakdown for the last <paramref name="days"/> days, ending today.</summary>
    Task<Result<AttendanceTrendDto>> GetAttendanceTrendAsync(int days = 7);

    /// <summary>Lateness and early-leave analysis over a date range.</summary>
    Task<Result<PunctualityDto>> GetPunctualityAsync(DateTime from, DateTime to, int topCount = 10);

    /// <summary>Pending approvals, entitlement utilisation and upcoming absences.</summary>
    Task<Result<LeaveOverviewDto>> GetLeaveOverviewAsync();

    /// <summary>Data-quality signals that cause silently wrong attendance.</summary>
    Task<Result<OperationsHealthDto>> GetOperationsHealthAsync(DateTime from, DateTime to);
}

/// <summary>Reporting service contract.</summary>
public interface IReportService
{
    Task<IEnumerable<AttendanceLogDto>> GetDailyAttendanceReportAsync(DateTime date);
    Task<IEnumerable<AttendanceSummaryDto>> GetMonthlyAttendanceReportAsync(int month, int year, int? departmentId = null);
    Task<IEnumerable<AttendanceLogDto>> GetLateReportAsync(DateTime from, DateTime to, int? departmentId = null);
    Task<IEnumerable<AttendanceLogDto>> GetAbsentReportAsync(DateTime from, DateTime to, int? departmentId = null);
    Task<IEnumerable<LeaveRequestDto>> GetLeaveReportAsync(DateTime from, DateTime to, int? departmentId = null);
    Task<IEnumerable<EmployeeListItemDto>> GetEmployeeListReportAsync(int? departmentId = null);
}

/// <summary>Biometric device data import service contract.</summary>
public interface IBiometricImportService
{
    /// <summary>Read the biometric device's Enroll table without changing the device database.</summary>
    Task<System.Data.DataTable> ReadEnrollTableAsync(string mdbFilePath);

    /// <summary>Import punches directly from a ZKTeco/Access MDB file or ODBC source.</summary>
    Task<BiometricImportResultDto> ImportFromAccessFileAsync(string mdbFilePath, DateTime fromDate, DateTime toDate);

    /// <summary>Import punches from a CSV or Excel file exported from the device software.</summary>
    Task<BiometricImportResultDto> ImportFromFileAsync(string filePath, DateTime fromDate, DateTime toDate);

    /// <summary>Parse raw punch rows and return preview without saving.</summary>
    Task<List<BiometricPunchDto>> PreviewFileAsync(string filePath, DateTime? fromDate = null, DateTime? toDate = null);

    /// <summary>Read raw punch rows from an Access database without saving them.</summary>
    Task<List<BiometricPunchDto>> PreviewAccessFileAsync(string mdbFilePath, DateTime fromDate, DateTime toDate);

    /// <summary>Process and import user-edited biometric punches into attendance logs.</summary>
    Task<BiometricImportResultDto> ProcessEditedPunchesAsync(List<BiometricPunchDto> punches);
}
