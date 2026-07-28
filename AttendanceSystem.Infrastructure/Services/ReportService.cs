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
        var sql = @"
            SELECT s.EmployeeId, e.EmployeeCode,
                   CONCAT(e.FirstName,' ',e.LastName) AS EmployeeName,
                   d.Name AS Department, s.Month, s.Year,
                   s.TotalDays, s.PresentDays, s.AbsentDays, s.LateDays,
                   s.LeaveDays, s.HolidayDays, s.TotalWorkingHours
            FROM AttendanceSummaries s
            INNER JOIN Employees e ON e.Id = s.EmployeeId
            INNER JOIN Departments d ON d.Id = e.DepartmentId
            WHERE s.Month = @Month AND s.Year = @Year AND s.IsDeleted = 0
              AND (@DeptId IS NULL OR e.DepartmentId = @DeptId)
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
