using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Domain.Enums;
using AttendanceSystem.Infrastructure.Dapper;

namespace AttendanceSystem.Infrastructure.Services;

/// <summary>Dapper-powered reporting service — lives in Infrastructure where EF/Dapper are available.</summary>
public class ReportService : IReportService
{
    private readonly DapperContext _dapper;
    public ReportService(DapperContext dapper) { _dapper = dapper; }

    public async Task<IEnumerable<AttendanceLogDto>> GetDailyAttendanceReportAsync(DateTime date) =>
        await _dapper.QueryAsync<AttendanceLogDto>(@"
            SELECT a.Id, a.EmployeeId, e.EmployeeCode, CONCAT(e.FirstName,' ',e.LastName) AS EmployeeName,
                   d.Name AS Department, a.AttendanceDate, a.CheckIn, a.CheckOut,
                   a.Status, a.IsLate, a.IsEarlyLeave, a.LateMinutes, a.EarlyLeaveMinutes,
                   a.WorkingHours, a.Remarks, a.IsManual
            FROM AttendanceLogs a
            INNER JOIN Employees e ON e.Id = a.EmployeeId
            INNER JOIN Departments d ON d.Id = e.DepartmentId
            WHERE a.AttendanceDate = @Date AND a.IsDeleted = 0
            ORDER BY e.FirstName", new { Date = date.Date });

    public async Task<IEnumerable<AttendanceSummaryDto>> GetMonthlyAttendanceReportAsync(
        int month, int year, int? departmentId = null)
    {
        // Aggregated from AttendanceLogs rather than read from AttendanceSummaries.
        //
        // AttendanceSummaries is written by nothing in the system — the only rows in it are
        // the seed data — so this report returned nothing for every real month, no matter how
        // much attendance had been recorded. Computing it from the logs is also always current,
        // where a stored summary would need a nightly job and could go stale between runs.
        //
        // Employees are LEFT JOINed so somebody with no punches at all still appears, with
        // zeros, instead of vanishing from the report.
        var sql = @"
            SELECT e.Id AS EmployeeId, e.EmployeeCode,
                   CONCAT(e.FirstName,' ',e.LastName) AS EmployeeName,
                   d.Name AS Department, @Month AS Month, @Year AS Year,
                   COUNT(a.Id)                                                   AS TotalDays,
                   SUM(CASE WHEN a.Status IN (1,3) THEN 1 ELSE 0 END)            AS PresentDays,
                   SUM(CASE WHEN a.Status = 2      THEN 1 ELSE 0 END)            AS AbsentDays,
                   SUM(CASE WHEN a.IsLate = 1      THEN 1 ELSE 0 END)            AS LateDays,
                   SUM(CASE WHEN a.Status = 5      THEN 1 ELSE 0 END)            AS LeaveDays,
                   SUM(CASE WHEN a.Status IN (6,7) THEN 1 ELSE 0 END)            AS HolidayDays,
                   ISNULL(SUM(a.WorkingHours), 0)                                AS TotalWorkingHours,
                   ISNULL(MAX(sh.AllowedLateDaysPerMonth), 0)                    AS LateAllowance
            FROM Employees e
            INNER JOIN Departments d ON d.Id = e.DepartmentId
            LEFT JOIN AttendanceLogs a
                   ON a.EmployeeId = e.Id
                  AND a.IsDeleted = 0
                  AND MONTH(a.AttendanceDate) = @Month
                  AND YEAR(a.AttendanceDate)  = @Year
            -- Late allowance comes from the shift in force during the month. CROSS APPLY takes
            -- the latest assignment covering it, matching how shifts resolve everywhere else.
            OUTER APPLY (
                SELECT TOP 1 s.AllowedLateDaysPerMonth
                FROM EmployeeShifts es
                INNER JOIN Shifts s ON s.Id = es.ShiftId
                WHERE es.EmployeeId = e.Id AND es.IsDeleted = 0
                  AND es.EffectiveFrom <= EOMONTH(DATEFROMPARTS(@Year, @Month, 1))
                  AND (es.EffectiveTo IS NULL OR es.EffectiveTo >= DATEFROMPARTS(@Year, @Month, 1))
                ORDER BY es.EffectiveFrom DESC
            ) sh
            WHERE e.IsDeleted = 0 AND e.IsActive = 1
              AND (@DeptId IS NULL OR e.DepartmentId = @DeptId)
            GROUP BY e.Id, e.EmployeeCode, e.FirstName, e.LastName, d.Name
            ORDER BY e.FirstName";
        return await _dapper.QueryAsync<AttendanceSummaryDto>(sql,
            new { Month = month, Year = year, DeptId = departmentId });
    }

    public async Task<IEnumerable<AttendanceLogDto>> GetLateReportAsync(
        DateTime from, DateTime to, int? departmentId = null)
    {
        var sql = @"
            SELECT a.Id, a.EmployeeId, e.EmployeeCode,
                   CONCAT(e.FirstName,' ',e.LastName) AS EmployeeName,
                   d.Name AS Department, a.AttendanceDate, a.CheckIn, a.CheckOut,
                   a.Status, a.IsLate, a.LateMinutes, a.WorkingHours, a.Remarks
            FROM AttendanceLogs a
            INNER JOIN Employees e ON e.Id = a.EmployeeId
            INNER JOIN Departments d ON d.Id = e.DepartmentId
            WHERE a.IsLate = 1 AND a.AttendanceDate BETWEEN @From AND @To
              AND a.IsDeleted = 0
              AND (@DeptId IS NULL OR e.DepartmentId = @DeptId)
            ORDER BY a.AttendanceDate, e.FirstName";
        return await _dapper.QueryAsync<AttendanceLogDto>(sql,
            new { From = from.Date, To = to.Date, DeptId = departmentId });
    }

    public async Task<IEnumerable<AttendanceLogDto>> GetAbsentReportAsync(
        DateTime from, DateTime to, int? departmentId = null)
    {
        var sql = @"
            SELECT a.Id, a.EmployeeId, e.EmployeeCode,
                   CONCAT(e.FirstName,' ',e.LastName) AS EmployeeName,
                   d.Name AS Department, a.AttendanceDate, a.Status
            FROM AttendanceLogs a
            INNER JOIN Employees e ON e.Id = a.EmployeeId
            INNER JOIN Departments d ON d.Id = e.DepartmentId
            WHERE a.Status = @Absent AND a.AttendanceDate BETWEEN @From AND @To
              AND a.IsDeleted = 0
              AND (@DeptId IS NULL OR e.DepartmentId = @DeptId)
            ORDER BY a.AttendanceDate, e.FirstName";
        return await _dapper.QueryAsync<AttendanceLogDto>(sql,
            new { Absent = (int)AttendanceStatus.Absent, From = from.Date, To = to.Date, DeptId = departmentId });
    }

    public async Task<IEnumerable<LeaveRequestDto>> GetLeaveReportAsync(
        DateTime from, DateTime to, int? departmentId = null)
    {
        var sql = @"
            SELECT l.Id, l.EmployeeId, e.EmployeeCode,
                   CONCAT(e.FirstName,' ',e.LastName) AS EmployeeName,
                   d.Name AS Department, lt.Name AS LeaveTypeName,
                   l.FromDate, l.ToDate, l.TotalDays, l.Reason, l.Status
            FROM LeaveRequests l
            INNER JOIN Employees e ON e.Id = l.EmployeeId
            INNER JOIN Departments d ON d.Id = e.DepartmentId
            INNER JOIN LeaveTypes lt ON lt.Id = l.LeaveTypeId
            WHERE l.FromDate >= @From AND l.ToDate <= @To AND l.IsDeleted = 0
              AND (@DeptId IS NULL OR e.DepartmentId = @DeptId)
            ORDER BY l.FromDate, e.FirstName";
        return await _dapper.QueryAsync<LeaveRequestDto>(sql,
            new { From = from.Date, To = to.Date, DeptId = departmentId });
    }

    public async Task<IEnumerable<EmployeeListItemDto>> GetEmployeeListReportAsync(int? departmentId = null)
    {
        var sql = @"
            SELECT e.Id, e.EmployeeCode, CONCAT(e.FirstName,' ',e.LastName) AS FullName,
                   d.Name AS Department, des.Name AS Designation,
                   b.Name AS Branch, e.Phone, e.Email, e.IsActive
            FROM Employees e
            INNER JOIN Departments d ON d.Id = e.DepartmentId
            INNER JOIN Designations des ON des.Id = e.DesignationId
            INNER JOIN Branches b ON b.Id = e.BranchId
            WHERE e.IsDeleted = 0
              AND (@DeptId IS NULL OR e.DepartmentId = @DeptId)
            ORDER BY e.FirstName";
        return await _dapper.QueryAsync<EmployeeListItemDto>(sql, new { DeptId = departmentId });
    }
}
