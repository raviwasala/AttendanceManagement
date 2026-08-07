using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Common.Models;
using AttendanceSystem.Domain.Enums;

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

    /// <summary>
    /// Checks a password for an already-signed-in user, to release a locked screen.
    ///
    /// Separate from <see cref="LoginAsync"/> on purpose: signing in again would rotate the
    /// remember-me token, restamp LastLoginAt and re-read the permission set, none of which
    /// should happen because somebody stepped away from their desk. It also must not count
    /// toward the failed-attempt lockout in a way that locks the account out of a screen the
    /// user is already inside — a wrong password here is a typo, not an intrusion attempt.
    /// </summary>
    Task<Result> VerifyPasswordAsync(int userId, string password);
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

    /// <summary>Users who may decide leave and overtime for this department.</summary>
    Task<Result<IEnumerable<DepartmentApproverDto>>> GetApproversAsync(int departmentId);
    Task<Result> AddApproverAsync(SaveDepartmentApproverDto dto);
    Task<Result> RemoveApproverAsync(int approverId);
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
    /// <summary>
    /// Every active employee. Kept for the dropdowns that legitimately need the whole list;
    /// the list screen uses <see cref="GetPagedAsync"/>.
    /// </summary>
    Task<Result<IEnumerable<EmployeeListItemDto>>> GetAllAsync();

    Task<Result<PagedResult<EmployeeListItemDto>>> GetPagedAsync(
        string? search, int? departmentId, int? designationId, int? branchId,
        bool? isActive, PageRequest page);

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

    Task<Result<PagedResult<LeaveRequestDto>>> GetRequestsPagedAsync(
        string? search, LeaveStatus? status, int? departmentId, int? employeeId,
        DateTime? from, DateTime? to, PageRequest page);

    Task<Result<IEnumerable<LeaveRequestDto>>> GetByEmployeeAsync(int employeeId);
    Task<Result<IEnumerable<LeaveRequestDto>>> GetPendingAsync();
    Task<Result<LeaveRequestDto>> ApplyLeaveAsync(ApplyLeaveDto dto);
    Task<Result> ApproveRejectAsync(ApproveRejectLeaveDto dto, int actionBy);
    Task<Result> CancelAsync(int leaveRequestId, int cancelledBy);
    Task<Result<IEnumerable<LeaveBalanceDto>>> GetBalancesAsync(int employeeId);
}

/// <summary>
/// Which departments a user may approve requests for.
///
/// Shared by leave and overtime: both ask the same question, and two implementations of it
/// would eventually disagree about who may decide what.
/// </summary>
public interface IApprovalScopeService
{
    Task<Services.LeaveApprovalScope> GetForCurrentUserAsync();
    Task<Services.LeaveApprovalScope> GetForAsync(int userId);

    /// <summary>
    /// Which records the signed-in user may read. Built from the same department
    /// configuration as approval so the two cannot drift apart.
    /// </summary>
    Task<Services.DataScope> GetDataScopeAsync();
}

/// <summary>
/// Payroll master data — banks, grades, groups, sub-departments, components and the
/// statutory rate tables.
/// </summary>
public interface IPayrollSetupService
{
    Task<Result<IEnumerable<BankDto>>> GetBanksAsync();
    Task<Result> SaveBankAsync(SaveBankDto dto);
    Task<Result> DeleteBankAsync(int id);

    Task<Result<IEnumerable<BankBranchDto>>> GetBankBranchesAsync(int? bankId);
    Task<Result> SaveBankBranchAsync(SaveBankBranchDto dto);
    Task<Result> DeleteBankBranchAsync(int id);

    Task<Result<IEnumerable<SalaryGradeDto>>> GetGradesAsync();
    Task<Result> SaveGradeAsync(SaveSalaryGradeDto dto);
    Task<Result> DeleteGradeAsync(int id);

    Task<Result<IEnumerable<SalaryGroupDto>>> GetGroupsAsync();
    Task<Result> SaveGroupAsync(SaveSalaryGroupDto dto);
    Task<Result> DeleteGroupAsync(int id);

    Task<Result<IEnumerable<SubDepartmentDto>>> GetSubDepartmentsAsync(int? departmentId);
    Task<Result> SaveSubDepartmentAsync(SaveSubDepartmentDto dto);
    Task<Result> DeleteSubDepartmentAsync(int id);

    Task<Result<IEnumerable<SalaryComponentDto>>> GetComponentsAsync();
    Task<Result> SaveComponentAsync(SaveSalaryComponentDto dto);
    Task<Result> DeleteComponentAsync(int id);

    Task<Result<IEnumerable<EmploymentCategoryDto>>> GetCategoriesAsync();
    Task<Result> SaveCategoryAsync(SaveEmploymentCategoryDto dto);
    Task<Result> DeleteCategoryAsync(int id);

    Task<Result<IEnumerable<LoanTypeDto>>> GetLoanTypesAsync();
    Task<Result> SaveLoanTypeAsync(SaveLoanTypeDto dto);
    Task<Result> DeleteLoanTypeAsync(int id);

    Task<Result<BranchPayrollSettingsDto>> GetBranchSettingsAsync(int branchId);
    Task<Result> SaveBranchSettingsAsync(SaveBranchPayrollSettingsDto dto);

    Task<Result<IEnumerable<ThirdPartyDto>>> GetThirdPartiesAsync();
    Task<Result> SaveThirdPartyAsync(SaveThirdPartyDto dto);
    Task<Result> DeleteThirdPartyAsync(int id);

    Task<Result<IEnumerable<ApitTaxTableDto>>> GetApitTablesAsync();
    Task<Result> SaveApitTableAsync(SaveApitTaxTableDto dto);

    Task<Result<IEnumerable<EpfEtfRateDto>>> GetRatesAsync();
    Task<Result> SaveRateAsync(SaveEpfEtfRateDto dto);

    Task<Result<IEnumerable<ApitBracketDto>>> GetApitBracketsAsync();
    Task<Result> SaveApitBracketAsync(SaveApitBracketDto dto);
    Task<Result> DeleteApitBracketAsync(int id);
}

/// <summary>
/// One-off allowances and deductions for a payroll month, entered one code at a time
/// against many employees.
/// </summary>
public interface IMonthlyTransactionService
{
    /// <summary>
    /// Every employee this user may see, with whatever they have for this code this month.
    /// Employees with nothing entered come back with zero rather than being omitted — the
    /// grid is an entry sheet, so the people without a figure are the point.
    /// </summary>
    Task<Result<ItemWiseGridDto>> GetItemWiseAsync(
        int salaryComponentId, int yearMonth, int? departmentId, string? search);

    /// <summary>Saves the grid. Zero removes a row; only rows sent are touched.</summary>
    Task<Result<string>> SaveItemWiseAsync(SaveItemWiseDto dto);

    /// <summary>Totals per code for one month — what the payroll run will pick up.</summary>
    Task<Result<IEnumerable<ItemWiseGridDto>>> GetMonthSummaryAsync(int yearMonth);

    /// <summary>
    /// The same rows read the other way round: one employee's whole month, every code.
    /// Only codes that have a figure appear — this is a payslip working, not an entry sheet.
    /// </summary>
    Task<Result<EmployeeWiseGridDto>> GetEmployeeWiseAsync(int employeeId, int yearMonth);

    /// <summary>Replaces the employee's month. A code dropped from the grid is removed.</summary>
    Task<Result<string>> SaveEmployeeWiseAsync(SaveEmployeeWiseDto dto);
}

/// <summary>
/// One employee's payroll record — statutory numbers, grade, bank account, and the
/// component values that differ from the defaults.
/// </summary>
public interface IEmployeePayrollService
{
    /// <summary>Every employee this user may see, with their payroll setup and readiness.</summary>
    Task<Result<IEnumerable<EmployeePayrollListItemDto>>> GetListAsync(
        string? search, int? departmentId, bool? readyOnly);

    /// <summary>Bank details for every visible employee, for the maintenance grid.</summary>
    Task<Result<IEnumerable<EmployeeBankRowDto>>> GetBankRowsAsync(
        string? search, int? departmentId, bool? incompleteOnly);
    Task<Result> SaveBankRowAsync(SaveEmployeeBankRowDto dto);

    /// <summary>Sets one component to the same value for many employees.</summary>
    Task<Result<BulkAssignResultDto>> BulkAssignComponentAsync(BulkAssignComponentDto dto);

    /// <summary>Sets one component to the same amount across a scope rather than a chosen list.</summary>
    Task<Result<CommonValueResultDto>> ApplyCommonValueAsync(CommonValueEntryDto dto);
    Task<Result<int>> CountForScopeAsync(int componentId, CommonValueScope scope);

    /// <summary>One employee's scheduled allowances and deductions, with their month ranges.</summary>
    Task<Result<IEnumerable<TransactionScheduleRowDto>>> GetScheduleAsync(int employeeId);
    Task<Result> SaveScheduleRowAsync(SaveTransactionScheduleRowDto dto);
    Task<Result> DeleteScheduleRowAsync(int id);

    Task<Result<IEnumerable<EpfAdjustmentDto>>> GetEpfAdjustmentsAsync(int? year, int? month);
    Task<Result> SaveEpfAdjustmentAsync(SaveEpfAdjustmentDto dto);
    Task<Result> DeleteEpfAdjustmentAsync(int id);

    /// <summary>Everyone not being paid — suspended, resigned or inactive.</summary>
    Task<Result<IEnumerable<NonEffectiveEmployeeDto>>> GetNonEffectiveAsync();
    Task<Result> SuspendAsync(SuspendEmployeeDto dto);

    Task<Result> ChangeEmployeeCodeAsync(ChangeEmployeeCodeDto dto);

    Task<Result<EmployeePayrollInfoDto>> GetAsync(int employeeId);
    Task<Result> SaveAsync(SaveEmployeePayrollInfoDto dto);

    /// <summary>Sets one employee's basic salary directly — the Salary Details screen.</summary>
    Task<Result> SaveSalaryAsync(SaveEmployeeSalaryDto dto);

    Task<Result<IEnumerable<EmployeeComponentDto>>> GetComponentsAsync(int employeeId);
    Task<Result> SaveComponentAsync(SaveEmployeeComponentDto dto);
}

/// <summary>Staff loans — granting, recovery and early settlement.</summary>
public interface ILoanService
{
    /// <summary>Preview only. Nothing is stored, so the figures are visible before granting.</summary>
    Result<LoanScheduleDto> PreviewSchedule(decimal amount, decimal rate, int months,
                                            LoanInterestType interestType);

    Task<Result<IEnumerable<EmployeeLoanDto>>> GetLoansAsync(int? employeeId, LoanStatus? status);
    Task<Result> SaveLoanAsync(SaveEmployeeLoanDto dto);

    Task<Result<IEnumerable<LoanTransactionDto>>> GetTransactionsAsync(int loanId);
    Task<Result> SettleAsync(LoanSettlementDto dto);
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
    Task<Result<IEnumerable<AuditLogDto>>> GetByModuleAsync(string module, int count = 100);

    /// <summary>One page of the trail. The search is applied in SQL, not in the browser.</summary>
    Task<Result<PagedResult<AuditLogDto>>> GetPagedAsync(
        string? module, string? search, PageRequest page);
}

/// <summary>
/// Employee self-service — the signed-in employee's own attendance and leave.
///
/// No method takes an employee id: the employee is always resolved from the signed-in user's
/// linked record. Accepting an id would let any employee read a colleague's attendance by
/// changing a number in the URL.
/// </summary>
public interface ISelfServiceService
{
    Task<Result<MyProfileDto>> GetMyProfileAsync();
    Task<Result<MyAttendanceDto>> GetMyAttendanceAsync(int year, int month);
    Task<Result<MyLeaveDto>> GetMyLeaveAsync();

    /// <summary>
    /// Applies for leave as the signed-in employee.
    ///
    /// Takes no employee id. <see cref="ApplyLeaveDto"/> carries one, and the Employee role
    /// holds Leave.Create — so an employee posting to the admin endpoint could book leave in a
    /// colleague's name. Self-service resolves the employee from the session instead, which is
    /// the only value the caller cannot influence.
    /// </summary>
    Task<Result<LeaveRequestDto>> ApplyForMyLeaveAsync(ApplyMyLeaveDto dto);

    /// <summary>
    /// Cancels one of the signed-in employee's own requests. Refuses a request belonging to
    /// anyone else, so an id guessed from another user's page achieves nothing.
    /// </summary>
    Task<Result> CancelMyLeaveAsync(int leaveRequestId);
}

/// <summary>
/// Builds the header notification list from live data, filtered by what the signed-in user
/// is permitted to act on.
/// </summary>
public interface INotificationService
{
    Task<Result<NotificationsDto>> GetAsync();
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
        DateTime fromDate, DateTime toDate, int? employeeId = null, int? departmentId = null,
        PageRequest? page = null, string? rowFilter = null);

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
    Task<Result<ShiftRosterDto>> GetMonthlyRosterAsync(int year, int month, int? departmentId = null,
        string? search = null, int? employeeId = null, int? shiftId = null, PageRequest? page = null);

    /// <summary>Sets one day's shift, or clears the override when ShiftId is null.</summary>
    Task<Result> SetDayAsync(SetRosterDayDto dto);

    /// <summary>Applies one shift across a date range, committed as a single unit.</summary>
    Task<Result> SetRangeAsync(SetRosterRangeDto dto);
}

/// <summary>
/// Overtime management: the policy that values extra hours, the queue that approves them,
/// and the register and summary that report on them.
///
/// Claims are *derived* from attendance rather than entered against it. AttendanceLog already
/// carries OvertimeMinutes computed by <see cref="Services.AttendanceCalculator"/>; generation
/// turns those minutes into claims by applying the matching rule. That keeps one calculation of
/// how long somebody worked, and makes regenerating a corrected day safe.
/// </summary>
public interface IOvertimeService
{
    // ── Rules ────────────────────────────────────────────────────────────────
    Task<Result<IEnumerable<OvertimeRuleDto>>> GetRulesAsync();
    Task<Result<OvertimeRuleDto>> GetRuleByIdAsync(int id);
    Task<Result<OvertimeRuleDto>> SaveRuleAsync(SaveOvertimeRuleDto dto);
    Task<Result> DeleteRuleAsync(int id);

    // ── Claims ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds or refreshes claims for a date range from the attendance already recorded.
    /// Rows already approved or rejected are left alone, so re-running is safe.
    /// </summary>
    Task<Result<GenerateOvertimeResultDto>> GenerateAsync(GenerateOvertimeDto dto);

    /// <summary>
    /// Claims in a range. Pass a page to fetch one; omit it to get the whole range, which is
    /// what the summary needs in order to aggregate per employee.
    /// </summary>
    Task<Result<OvertimeRegisterDto>> GetRegisterAsync(DateTime from, DateTime to,
        int? employeeId = null, int? departmentId = null, OvertimeStatus? status = null,
        PageRequest? page = null);

    /// <summary>Approves or rejects one or many claims in a single transaction.</summary>
    Task<Result<int>> DecideAsync(OvertimeDecisionDto dto);

    Task<Result<OvertimeSummaryDto>> GetSummaryAsync(DateTime from, DateTime to,
        int? departmentId = null, int? employeeId = null);
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

    /// <summary>
    /// Import punches from a device export: CSV, Excel, or an Access .mdb/.accdb.
    /// The file's extension selects the reader.
    /// </summary>
    Task<BiometricImportResultDto> ImportFromFileAsync(string filePath, DateTime fromDate, DateTime toDate);

    /// <summary>Parse raw punch rows and return preview without saving. Same formats as above.</summary>
    Task<List<BiometricPunchDto>> PreviewFileAsync(string filePath, DateTime? fromDate = null, DateTime? toDate = null);

    /// <summary>Read raw punch rows from an Access database without saving them.</summary>
    Task<List<BiometricPunchDto>> PreviewAccessFileAsync(string mdbFilePath, DateTime fromDate, DateTime toDate);

    /// <summary>Process and import user-edited biometric punches into attendance logs.</summary>
    Task<BiometricImportResultDto> ProcessEditedPunchesAsync(List<BiometricPunchDto> punches);
}

/// <summary>
/// Getting data out of the system and back in: dataset exports, a whole-database backup
/// archive, and restoring from one.
///
/// The backup is a ZIP of CSVs rather than a SQL .bak on purpose. A .bak is exact, but
/// RESTORE DATABASE needs exclusive access — it would have to terminate the very connection
/// serving the request, so the application would take itself down mid-restore and a failure
/// would leave no running app and an unusable database. A logical archive restores inside a
/// transaction, can be moved to a different server, and cannot destroy a live database.
/// </summary>
public interface IDataTransferService
{
    /// <summary>One dataset as CSV. <paramref name="from"/>/<paramref name="to"/> are
    /// ignored by datasets that are not date-ranged.</summary>
    Task<Result<ExportFileDto>> ExportAsync(ExportDataset dataset, DateTime? from = null, DateTime? to = null);

    /// <summary>Every table as a ZIP of CSVs, with a manifest.</summary>
    Task<Result<ExportFileDto>> CreateBackupAsync();

    /// <summary>
    /// A real SQL Server backup (<c>BACKUP DATABASE</c>), written by SQL Server itself.
    ///
    /// Byte-exact and restorable by a DBA, which the logical archive is not — but it is
    /// written to the <em>database server's</em> filesystem, so it can only be handed to the
    /// browser when SQL Server and the web host are the same machine. Taken WITH COPY_ONLY so
    /// it does not disturb the log chain of whatever scheduled backup routine already exists.
    /// </summary>
    Task<Result<SqlBackupDto>> CreateSqlBackupAsync();

    /// <summary>Reads an archive and reports what restoring it would change. Writes nothing.</summary>
    Task<Result<RestorePreviewDto>> PreviewRestoreAsync(byte[] archive);

    /// <summary>
    /// Replaces the contents of the listed tables from the archive, in one transaction.
    /// <paramref name="tables"/> null or empty means every recognised table in the archive.
    /// </summary>
    Task<Result<RestoreResultDto>> RestoreAsync(byte[] archive, IEnumerable<string>? tables = null);
}

/// <summary>
/// An employee's life beyond their current details: transfers, status changes, resignation,
/// documents, and the profile that shows them together.
///
/// Separate from <c>IEmployeeService</c>, which edits the row as it stands now. These
/// operations are the ones that must leave a trace — the employee row is a snapshot, and
/// overwriting a department silently rewrites every past report that groups by it.
/// </summary>
public interface IEmployeeLifecycleService
{
    Task<Result<EmployeeProfileDto>> GetProfileAsync(int employeeId);
    Task<Result<IEnumerable<EmployeeHistoryDto>>> GetHistoryAsync(int employeeId);

    /// <summary>Moves an employee and records where they came from.</summary>
    Task<Result> TransferAsync(TransferEmployeeDto dto);

    /// <summary>Changes status with a reason and an effective date, and records both.</summary>
    Task<Result> ChangeStatusAsync(ChangeEmployeeStatusDto dto);

    /// <summary>Records a resignation or termination and sets the last working day.</summary>
    Task<Result> ResignAsync(ResignEmployeeDto dto);

    /// <summary>Undoes a resignation for someone who came back, keeping the original record.</summary>
    Task<Result> RejoinAsync(int employeeId, DateTime effectiveDate, string reason);

    // ── Documents ─────────────────────────────────────────────────────────────

    Task<Result<IEnumerable<EmployeeDocumentDto>>> GetDocumentsAsync(int employeeId);

    Task<Result<EmployeeDocumentDto>> UploadDocumentAsync(
        int employeeId, EmployeeDocumentType type, string title, string fileName,
        string contentType, byte[] content, DateTime? expiryDate, string? notes);

    /// <summary>The bytes, for a download. Separate from the list so listing stays cheap.</summary>
    Task<Result<ExportFileDto>> DownloadDocumentAsync(int documentId);

    Task<Result> DeleteDocumentAsync(int documentId);
}

/// <summary>
/// Closing an attendance period, and recalculating one.
///
/// These belong together because they are the two halves of keeping attendance trustworthy
/// after the fact: a lock stops a paid month from changing, and reprocessing fixes an unpaid
/// one when the shift settings it was calculated against turn out to be wrong.
/// </summary>
/// <summary>
/// Closing a month and handing it to payroll — the readiness checks, the close itself, and
/// the single combined export.
/// </summary>
public interface IMonthEndService
{
    /// <summary>Whether the month can be closed, and what is in the way.</summary>
    Task<Result<MonthEndStatusDto>> GetStatusAsync(int month, int year);

    /// <summary>Closes the month by locking it. Refuses while any blocking check fails.</summary>
    Task<Result> CloseMonthAsync(CloseMonthDto dto);

    /// <summary>Attendance and approved overtime as one row per employee.</summary>
    Task<Result<PayrollExportDto>> GetPayrollAsync(int month, int year);
}

public interface IAttendanceLockService
{
    Task<Result<IEnumerable<AttendancePeriodLockDto>>> GetLocksAsync();

    Task<Result> LockPeriodAsync(LockPeriodDto dto);
    Task<Result> UnlockPeriodAsync(UnlockPeriodDto dto);

    /// <summary>
    /// Whether a date is closed for a branch. Called on every attendance save and once per
    /// imported day, so implementations should cache rather than query per call.
    /// </summary>
    Task<bool> IsLockedAsync(DateTime date, int? branchId = null);

    /// <summary>The lock covering a date, for an error message that says which period and why.</summary>
    Task<AttendancePeriodLockDto?> GetLockForAsync(DateTime date, int? branchId = null);

    /// <summary>
    /// Recalculates stored attendance from the times already recorded against the shift
    /// settings in force now.
    ///
    /// This is what makes a corrected shift take effect. Fixing grace minutes or a break
    /// after a month has been imported changes nothing on its own — every row keeps the
    /// figures derived from the old settings until each is re-saved by hand.
    /// </summary>
    Task<Result<ReprocessResultDto>> ReprocessAsync(ReprocessRequestDto dto);
}

/// <summary>
/// Which dashboard widgets a user sees.
///
/// Every method filters the catalogue by what the signed-in user is permitted to load. A
/// widget they cannot see is never offered and never returned — and the underlying data
/// endpoints keep their own permission checks, so a tampered preference produces an empty
/// widget rather than somebody else's data.
/// </summary>
public interface IDashboardPreferenceService
{
    /// <summary>The catalogue as it applies to the signed-in user, with current visibility.</summary>
    Task<Result<IEnumerable<DashboardWidgetDto>>> GetMyWidgetsAsync();

    Task<Result> SaveMyPreferencesAsync(SaveDashboardPreferencesDto dto);

    /// <summary>Removes this user's choices so they follow the company default again.</summary>
    Task<Result> ResetMineAsync();

    /// <summary>The company default new users start from.</summary>
    Task<Result<IEnumerable<DashboardWidgetDto>>> GetCompanyDefaultAsync();

    Task<Result> SaveCompanyDefaultAsync(SaveDashboardPreferencesDto dto);

    // ── Custom tiles ──────────────────────────────────────────────────────────

    /// <summary>Metrics this user may build a tile from — filtered by permission.</summary>
    Result<IEnumerable<DashboardMetricDto>> GetMetrics();

    /// <summary>The user's own tiles, each with its number already computed.</summary>
    Task<Result<IEnumerable<DashboardTileDto>>> GetMyTilesAsync();

    Task<Result<DashboardTileDto>> SaveTileAsync(SaveDashboardTileDto dto);
    Task<Result> DeleteTileAsync(int tileId);
}

/// <summary>Bulk creation and update of employee records from a CSV or Excel file.</summary>
public interface IEmployeeImportService
{
    /// <summary>A blank file with the expected header row, for someone starting from nothing.</summary>
    Result<ExportFileDto> GetTemplate();

    /// <summary>Parses and validates without writing, so problems are visible first.</summary>
    Task<Result<EmployeeImportPreviewDto>> PreviewAsync(Stream file, string fileName);

    /// <summary>Applies rows the operator accepted. Invalid rows are never written.</summary>
    Task<Result<EmployeeImportResultDto>> ImportAsync(List<EmployeeImportRowDto> rows);
}
