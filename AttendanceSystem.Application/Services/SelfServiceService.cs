using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Common.Logging;
using AttendanceSystem.Common.Models;
using AttendanceSystem.Common.Session;
using AttendanceSystem.Domain.Enums;
using AttendanceSystem.Domain.Interfaces;

namespace AttendanceSystem.Application.Services;

/// <summary>
/// Employee self-service: an employee's own attendance and leave.
///
/// Every method resolves the employee from the signed-in user's linked EmployeeId and
/// **never** takes an employee id from the caller. That is the whole security model of this
/// service: if a screen could pass an id, any employee could read a colleague's attendance
/// simply by changing a number in the URL.
///
/// Until now the Employee role had Attendance.View and Leave.View, which pointed at the
/// company-wide admin screens — so staff could see everyone. These endpoints exist so that
/// permission can mean "my own records".
/// </summary>
public class SelfServiceService : ISelfServiceService
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _currentUser;

    public SelfServiceService(IUnitOfWork uow, ICurrentUserContext currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<MyProfileDto>> GetMyProfileAsync()
    {
        try
        {
            var employee = await ResolveEmployeeAsync();
            if (employee == null) return Result<MyProfileDto>.Failure(NotLinkedMessage);

            var dept = await _uow.Departments.GetByIdAsync(employee.DepartmentId);
            var desig = await _uow.Designations.GetByIdAsync(employee.DesignationId);
            var branch = await _uow.Branches.GetByIdAsync(employee.BranchId);

            var today = DateTime.Today;
            var assignments = await _uow.EmployeeShifts.FindAsync(es =>
                es.EmployeeId == employee.Id && !es.IsDeleted &&
                es.EffectiveFrom <= today && (es.EffectiveTo == null || es.EffectiveTo >= today));
            var current = assignments.OrderByDescending(a => a.EffectiveFrom).FirstOrDefault();
            var shift = current != null ? await _uow.Shifts.GetByIdAsync(current.ShiftId) : null;

            return Result<MyProfileDto>.Success(new MyProfileDto
            {
                EmployeeId = employee.Id,
                EmployeeCode = employee.EmployeeCode,
                FullName = $"{employee.FirstName} {employee.LastName}",
                Department = dept?.Name ?? string.Empty,
                Designation = desig?.Name ?? string.Empty,
                Branch = branch?.Name ?? string.Empty,
                JoiningDate = employee.JoiningDate,
                ShiftName = shift?.Name,
                ShiftTimes = shift != null
                    ? $"{DateTime.Today.Add(shift.StartTime):HH:mm} – {DateTime.Today.Add(shift.EndTime):HH:mm}"
                    : null
            });
        }
        catch (Exception ex)
        {
            AppLogger.Error("SelfServiceService.GetMyProfileAsync", ex);
            return Result<MyProfileDto>.Failure("Failed to load your profile.");
        }
    }

    public async Task<Result<MyAttendanceDto>> GetMyAttendanceAsync(int year, int month)
    {
        try
        {
            var employee = await ResolveEmployeeAsync();
            if (employee == null) return Result<MyAttendanceDto>.Failure(NotLinkedMessage);

            if (month is < 1 or > 12) return Result<MyAttendanceDto>.Failure("Month must be between 1 and 12.");

            var daysInMonth = DateTime.DaysInMonth(year, month);
            var from = new DateTime(year, month, 1);
            var to = from.AddDays(daysInMonth - 1);

            var logs = (await _uow.Attendance.FindAsync(a =>
                    a.EmployeeId == employee.Id && !a.IsDeleted &&
                    a.AttendanceDate >= from && a.AttendanceDate <= to))
                .ToDictionary(a => a.AttendanceDate.Date);

            var assignments = (await _uow.EmployeeShifts.FindAsync(es =>
                    es.EmployeeId == employee.Id && !es.IsDeleted &&
                    es.EffectiveFrom <= to && (es.EffectiveTo == null || es.EffectiveTo >= from)))
                .ToList();

            var shifts = (await _uow.Shifts.GetAllAsync()).ToDictionary(s => s.Id);

            var holidays = (await _uow.Holidays.FindAsync(h =>
                    !h.IsDeleted && h.HolidayDate >= from && h.HolidayDate <= to))
                .GroupBy(h => h.HolidayDate.Date)
                .ToDictionary(g => g.Key, g => g.First().Name);

            var leaves = (await _uow.Leaves.FindAsync(l =>
                    l.EmployeeId == employee.Id && l.Status == LeaveStatus.Approved && !l.IsDeleted &&
                    l.FromDate.Date <= to && l.ToDate.Date >= from))
                .ToList();

            var dto = new MyAttendanceDto
            {
                Year = year,
                Month = month,
                MonthName = from.ToString("MMMM yyyy")
            };

            var today = DateTime.Today;

            for (var d = from; d <= to; d = d.AddDays(1))
            {
                // Nothing to say about days that have not happened yet.
                if (d > today) break;

                logs.TryGetValue(d, out var log);

                var assignment = assignments
                    .Where(a => a.EffectiveFrom.Date <= d && (a.EffectiveTo == null || a.EffectiveTo.Value.Date >= d))
                    .OrderByDescending(a => a.EffectiveFrom)
                    .FirstOrDefault();
                var shift = assignment != null && shifts.TryGetValue(assignment.ShiftId, out var sh) ? sh : null;

                var isHoliday = holidays.ContainsKey(d);
                var onLeave = leaves.Any(l => l.FromDate.Date <= d && l.ToDate.Date >= d);

                var row = new MyAttendanceDayDto
                {
                    Date = d,
                    DateDisplay = d.ToString("ddd dd MMM"),
                    ShiftName = shift?.Name,
                    ExpectedIn = shift != null ? DateTime.Today.Add(shift.StartTime).ToString("HH:mm") : null,
                    ExpectedOut = shift != null ? DateTime.Today.Add(shift.EndTime).ToString("HH:mm") : null,
                    IsHoliday = isHoliday,
                    HolidayName = isHoliday ? holidays[d] : null,
                    IsWeeklyOff = shift != null && AttendanceCalculator.IsWeeklyOff(shift, d)
                };

                if (log != null)
                {
                    row.CheckIn = log.CheckIn?.ToString("HH:mm");
                    row.CheckOut = log.CheckOut?.ToString("HH:mm");
                    row.LateMinutes = log.LateMinutes;
                    row.EarlyLeaveMinutes = log.EarlyLeaveMinutes;
                    row.WorkingHours = log.WorkingHours;
                    row.OvertimeMinutes = log.OvertimeMinutes;
                    row.Status = log.Status;
                }
                else
                {
                    row.Status = onLeave ? AttendanceStatus.OnLeave
                               : isHoliday ? AttendanceStatus.Holiday
                               : row.IsWeeklyOff ? AttendanceStatus.WeeklyOff
                               : AttendanceStatus.Absent;
                }

                dto.Days.Add(row);
            }

            dto.PresentDays = dto.Days.Count(x => x.Status is AttendanceStatus.Present or AttendanceStatus.Late);
            dto.LateDays = dto.Days.Count(x => x.Status == AttendanceStatus.Late);
            dto.AbsentDays = dto.Days.Count(x => x.Status == AttendanceStatus.Absent);
            dto.LeaveDays = dto.Days.Count(x => x.Status == AttendanceStatus.OnLeave);
            dto.TotalWorkingHours = Math.Round(dto.Days.Sum(x => x.WorkingHours ?? 0), 1);
            dto.TotalOvertimeMinutes = dto.Days.Sum(x => x.OvertimeMinutes ?? 0);
            dto.TotalLateMinutes = dto.Days.Sum(x => x.LateMinutes ?? 0);

            return Result<MyAttendanceDto>.Success(dto);
        }
        catch (Exception ex)
        {
            AppLogger.Error("SelfServiceService.GetMyAttendanceAsync", ex);
            return Result<MyAttendanceDto>.Failure("Failed to load your attendance.");
        }
    }

    public async Task<Result<MyLeaveDto>> GetMyLeaveAsync()
    {
        try
        {
            var employee = await ResolveEmployeeAsync();
            if (employee == null) return Result<MyLeaveDto>.Failure(NotLinkedMessage);

            var year = DateTime.Today.Year;
            var leaveTypes = (await _uow.LeaveTypes.GetAllAsync()).Where(t => t.IsActive).ToList();

            var mine = (await _uow.Leaves.GetByEmployeeAsync(employee.Id)).ToList();

            var dto = new MyLeaveDto { Year = year };

            dto.Balances = leaveTypes.Select(t =>
            {
                var used = mine
                    .Where(l => l.LeaveTypeId == t.Id && l.Status == LeaveStatus.Approved &&
                                l.FromDate.Year == year)
                    .Sum(l => l.TotalDays);

                return new MyLeaveBalanceDto
                {
                    LeaveTypeId = t.Id,
                    LeaveType = t.Name,
                    Entitled = t.TotalDays,
                    Used = used,
                    Remaining = Math.Max(0, t.TotalDays - used),
                    IsPaid = t.IsPaid
                };
            }).ToList();

            dto.Requests = mine
                .OrderByDescending(l => l.FromDate)
                .Take(50)
                .Select(l => new MyLeaveRequestDto
                {
                    Id = l.Id,
                    LeaveType = leaveTypes.FirstOrDefault(t => t.Id == l.LeaveTypeId)?.Name ?? string.Empty,
                    FromDate = l.FromDate,
                    ToDate = l.ToDate,
                    TotalDays = l.TotalDays,
                    Reason = l.Reason,
                    Status = l.Status,
                    RejectionReason = l.RejectionReason,
                    AppliedOn = l.CreatedAt
                })
                .ToList();

            dto.PendingCount = dto.Requests.Count(r => r.Status == LeaveStatus.Pending);

            return Result<MyLeaveDto>.Success(dto);
        }
        catch (Exception ex)
        {
            AppLogger.Error("SelfServiceService.GetMyLeaveAsync", ex);
            return Result<MyLeaveDto>.Failure("Failed to load your leave.");
        }
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    private const string NotLinkedMessage =
        "Your user account is not linked to an employee record, so there is nothing to show. " +
        "Ask an administrator to link them on the System Users screen.";

    /// <summary>
    /// The employee behind the signed-in user. The only source of an employee id in this
    /// service — never a parameter.
    /// </summary>
    private async Task<Domain.Entities.Employee?> ResolveEmployeeAsync()
    {
        var userId = _currentUser.UserId;
        if (!userId.HasValue) return null;

        var user = await _uow.Users.GetByIdAsync(userId.Value);
        if (user?.EmployeeId == null) return null;

        return await _uow.Employees.GetByIdAsync(user.EmployeeId.Value);
    }
}
