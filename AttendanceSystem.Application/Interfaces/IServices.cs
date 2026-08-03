using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Common.Models;

namespace AttendanceSystem.Application.Interfaces;

/// <summary>Authentication service contract.</summary>
public interface IAuthService
{
    Task<Result<UserDto>> LoginAsync(LoginDto dto);
    Task<Result> ChangePasswordAsync(int userId, ChangePasswordDto dto);
    Task LogoutAsync(int userId);
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
