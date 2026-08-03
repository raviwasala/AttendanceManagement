using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Common.Helpers;
using AttendanceSystem.Common.Logging;
using AttendanceSystem.Common.Models;
using AttendanceSystem.Common.Session;
using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Domain.Enums;
using AttendanceSystem.Domain.Interfaces;

namespace AttendanceSystem.Application.Services;

/// <summary>Handles check-in/out, attendance editing, summaries and dashboard stats.</summary>
public class AttendanceService : IAttendanceService
{
    private readonly IUnitOfWork _uow;
    private readonly IAuditService _audit;
    private readonly ICurrentUserContext _currentUser;

    public AttendanceService(IUnitOfWork uow, IAuditService audit, ICurrentUserContext currentUser)
    {
        _uow = uow; _audit = audit; _currentUser = currentUser;
    }

    public async Task<Result<AttendanceLogDto>> CheckInAsync(CheckInDto dto)
    {
        try
        {
            var today = dto.CheckInTime.Date;
            var existing = await _uow.Attendance.GetTodayAttendanceAsync(dto.EmployeeId, today);
            if (existing != null)
                return Result<AttendanceLogDto>.Failure("Employee has already checked in today.");

            var allShiftAssignments = await _uow.EmployeeShifts.FindAsync(
                es => es.EmployeeId == dto.EmployeeId && !es.IsDeleted
                      && es.EffectiveFrom <= today
                      && (es.EffectiveTo == null || es.EffectiveTo >= today));
            var shiftAssignment = allShiftAssignments.OrderByDescending(es => es.EffectiveFrom).FirstOrDefault();
            Shift? shift = shiftAssignment != null ? await _uow.Shifts.GetByIdAsync(shiftAssignment.ShiftId) : null;

            bool isHoliday = await _uow.Holidays.IsHolidayAsync(today);
            bool isWeeklyOff = false, isLate = false;
            int lateMinutes = 0;

            if (shift != null)
            {
                var offDays = shift.WeeklyOffDays.Split(',').Select(d => d.Trim()).ToList();
                isWeeklyOff = offDays.Contains(today.DayOfWeek.ToString());
                lateMinutes = DateHelper.CalculateLateMinutes(dto.CheckInTime.TimeOfDay, shift.StartTime, shift.GraceMinutes);
                isLate = lateMinutes > 0;
            }

            var status = isHoliday ? AttendanceStatus.Holiday
                       : isWeeklyOff ? AttendanceStatus.WeeklyOff
                       : isLate ? AttendanceStatus.Late
                       : AttendanceStatus.Present;

            var log = new AttendanceLog
            {
                EmployeeId = dto.EmployeeId, AttendanceDate = today, CheckIn = dto.CheckInTime,
                Status = status, IsLate = isLate, LateMinutes = lateMinutes > 0 ? lateMinutes : null,
                Remarks = dto.Remarks, IsManual = true, CreatedBy = _currentUser.UserId, CreatedAt = DateTime.Now
            };
            await _uow.Attendance.AddAsync(log);
            await _uow.SaveChangesAsync();
            await _audit.LogAsync("Attendance", "CheckIn", _currentUser.UserId, "AttendanceLog", log.Id);
            return Result<AttendanceLogDto>.Success(await BuildLogDtoAsync(log));
        }
        catch (Exception ex) { AppLogger.Error("AttendanceService.CheckInAsync", ex); return Result<AttendanceLogDto>.Failure(ex.Message); }
    }

    public async Task<Result<AttendanceLogDto>> CheckOutAsync(CheckOutDto dto)
    {
        try
        {
            var log = await _uow.Attendance.GetByIdAsync(dto.AttendanceLogId);
            if (log == null) return Result<AttendanceLogDto>.Failure("Attendance record not found.");
            if (log.CheckOut.HasValue) return Result<AttendanceLogDto>.Failure("Already checked out.");
            if (log.CheckIn.HasValue && dto.CheckOutTime < log.CheckIn)
                return Result<AttendanceLogDto>.Failure("Check-out time cannot be before check-in time.");

            log.CheckOut = dto.CheckOutTime;
            log.WorkingHours = DateHelper.CalculateWorkingHours(log.CheckIn, dto.CheckOutTime);
            if (dto.Remarks != null) log.Remarks = dto.Remarks;

            var allShiftAssignments = await _uow.EmployeeShifts.FindAsync(
                es => es.EmployeeId == log.EmployeeId && !es.IsDeleted
                      && es.EffectiveFrom <= log.AttendanceDate
                      && (es.EffectiveTo == null || es.EffectiveTo >= log.AttendanceDate));
            var sa = allShiftAssignments.OrderByDescending(es => es.EffectiveFrom).FirstOrDefault();
            Shift? shift = sa != null ? await _uow.Shifts.GetByIdAsync(sa.ShiftId) : null;

            if (shift != null && log.CheckOut.HasValue)
            {
                var earlyMins = (int)(shift.EndTime - log.CheckOut.Value.TimeOfDay).TotalMinutes;
                if (earlyMins > 0) { log.IsEarlyLeave = true; log.EarlyLeaveMinutes = earlyMins; }
            }

            log.ModifiedBy = _currentUser.UserId; log.ModifiedAt = DateTime.Now;
            await _uow.Attendance.UpdateAsync(log);
            await _uow.SaveChangesAsync();
            await _audit.LogAsync("Attendance", "CheckOut", _currentUser.UserId, "AttendanceLog", log.Id);
            return Result<AttendanceLogDto>.Success(await BuildLogDtoAsync(log));
        }
        catch (Exception ex) { AppLogger.Error("AttendanceService.CheckOutAsync", ex); return Result<AttendanceLogDto>.Failure(ex.Message); }
    }

    public async Task<Result> EditAsync(EditAttendanceDto dto, int modifiedBy)
    {
        try
        {
            var log = await _uow.Attendance.GetByIdAsync(dto.Id);
            if (log == null) return Result.Failure("Attendance record not found.");
            log.CheckIn = dto.CheckIn; log.CheckOut = dto.CheckOut; log.Status = dto.Status;
            if (dto.Remarks != null) log.Remarks = dto.Remarks;
            if (log.CheckIn.HasValue && log.CheckOut.HasValue)
                log.WorkingHours = DateHelper.CalculateWorkingHours(log.CheckIn, log.CheckOut);
            log.ModifiedBy = modifiedBy; log.ModifiedAt = DateTime.Now;
            await _uow.Attendance.UpdateAsync(log);
            await _uow.SaveChangesAsync();
            await _audit.LogAsync("Attendance", "Edit", modifiedBy, "AttendanceLog", log.Id);
            return Result.Success();
        }
        catch (Exception ex) { AppLogger.Error("AttendanceService.EditAsync", ex); return Result.Failure(ex.Message); }
    }

    public async Task<Result> DeleteAsync(int id, int deletedBy)
    {
        try
        {
            var log = await _uow.Attendance.GetByIdAsync(id);
            if (log == null) return Result.Failure("Attendance record not found.");
            log.IsDeleted = true; log.ModifiedBy = deletedBy; log.ModifiedAt = DateTime.Now;
            await _uow.Attendance.UpdateAsync(log);
            await _uow.SaveChangesAsync();
            return Result.Success();
        }
        catch (Exception ex) { return Result.Failure(ex.Message); }
    }

    public async Task<Result<IEnumerable<AttendanceLogDto>>> GetTodayAsync()
    {
        try
        {
            var today = DateTime.Today;
            var logs = (await _uow.Attendance.GetByDateAsync(today)).ToList();

            var dtos = new List<AttendanceLogDto>();
            foreach (var log in logs) dtos.Add(await BuildLogDtoAsync(log));

            // Employees with no log today are still part of "today's attendance" — in fact they
            // are the ones you most want to see. Without this the view silently omitted everyone
            // who did not turn up, so an Absent filter could never match anything and the list
            // disagreed with the dashboard's Absent count.
            var loggedEmployeeIds = logs.Select(l => l.EmployeeId).ToHashSet();
            var activeEmployees = await _uow.Employees.FindAsync(e => e.IsActive && !e.IsDeleted);
            var missing = activeEmployees.Where(e => !loggedEmployeeIds.Contains(e.Id)).ToList();

            if (missing.Count > 0)
            {
                var isHoliday = await _uow.Holidays.IsHolidayAsync(today);

                var approvedLeave = await _uow.Leaves.FindAsync(
                    l => l.Status == LeaveStatus.Approved && !l.IsDeleted
                         && l.FromDate.Date <= today && l.ToDate.Date >= today);
                var onLeaveEmployeeIds = approvedLeave.Select(l => l.EmployeeId).ToHashSet();

                foreach (var emp in missing)
                    dtos.Add(await BuildAbsenceDtoAsync(emp, today, isHoliday, onLeaveEmployeeIds));
            }

            return Result<IEnumerable<AttendanceLogDto>>.Success(
                dtos.OrderBy(d => d.EmployeeName).ToList());
        }
        catch (Exception ex) { AppLogger.Error("AttendanceService.GetTodayAsync", ex); return Result<IEnumerable<AttendanceLogDto>>.Failure(ex.Message); }
    }

    /// <summary>
    /// Builds the synthetic row for an employee with no attendance log on <paramref name="date"/>.
    ///
    /// <c>Id = 0</c> marks it as not persisted — there is nothing to edit, check out or delete,
    /// and callers must not treat it as a real record. Status precedence mirrors
    /// <see cref="CheckInAsync"/>: leave and holidays outrank a plain absence, so someone on
    /// approved leave is never reported as absent.
    /// </summary>
    private async Task<AttendanceLogDto> BuildAbsenceDtoAsync(
        Employee emp, DateTime date, bool isHoliday, HashSet<int> onLeaveEmployeeIds)
    {
        var status = onLeaveEmployeeIds.Contains(emp.Id) ? AttendanceStatus.OnLeave
                   : isHoliday                           ? AttendanceStatus.Holiday
                   : await IsWeeklyOffAsync(emp.Id, date) ? AttendanceStatus.WeeklyOff
                   : AttendanceStatus.Absent;

        var dept = await _uow.Departments.GetByIdAsync(emp.DepartmentId);

        return new AttendanceLogDto
        {
            Id = 0,
            EmployeeId = emp.Id,
            EmployeeCode = emp.EmployeeCode,
            EmployeeName = $"{emp.FirstName} {emp.LastName}",
            Department = dept?.Name ?? string.Empty,
            AttendanceDate = date,
            Status = status
        };
    }

    /// <summary>True when the date falls on a weekly off day of the employee's effective shift.</summary>
    private async Task<bool> IsWeeklyOffAsync(int employeeId, DateTime date)
    {
        var assignments = await _uow.EmployeeShifts.FindAsync(
            es => es.EmployeeId == employeeId && !es.IsDeleted
                  && es.EffectiveFrom <= date
                  && (es.EffectiveTo == null || es.EffectiveTo >= date));

        var assignment = assignments.OrderByDescending(es => es.EffectiveFrom).FirstOrDefault();
        if (assignment == null) return false;

        var shift = await _uow.Shifts.GetByIdAsync(assignment.ShiftId);
        if (shift == null) return false;

        return shift.WeeklyOffDays
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(d => d.Trim())
            .Contains(date.DayOfWeek.ToString(), StringComparer.OrdinalIgnoreCase);
    }

    public async Task<Result<IEnumerable<AttendanceLogDto>>> GetByEmployeeAndDateRangeAsync(
        int employeeId, DateTime from, DateTime to)
    {
        try
        {
            var logs = await _uow.Attendance.GetByEmployeeAndDateRangeAsync(employeeId, from, to);
            var dtos = new List<AttendanceLogDto>();
            foreach (var log in logs) dtos.Add(await BuildLogDtoAsync(log));
            return Result<IEnumerable<AttendanceLogDto>>.Success(dtos);
        }
        catch (Exception ex) { return Result<IEnumerable<AttendanceLogDto>>.Failure(ex.Message); }
    }

    public async Task<Result<IEnumerable<AttendanceSummaryDto>>> GetMonthlySummaryAsync(int month, int year)
    {
        try
        {
            var summaries = await _uow.AttendanceSummaries.FindAsync(
                s => s.Month == month && s.Year == year && !s.IsDeleted);
            var dtos = new List<AttendanceSummaryDto>();
            foreach (var s in summaries.OrderBy(x => x.EmployeeId))
            {
                var emp  = await _uow.Employees.GetByIdAsync(s.EmployeeId);
                var dept = emp != null ? await _uow.Departments.GetByIdAsync(emp.DepartmentId) : null;
                dtos.Add(new AttendanceSummaryDto
                {
                    EmployeeId = s.EmployeeId,
                    EmployeeName = emp != null ? $"{emp.FirstName} {emp.LastName}" : string.Empty,
                    EmployeeCode = emp?.EmployeeCode ?? string.Empty,
                    Department = dept?.Name ?? string.Empty,
                    Month = s.Month, Year = s.Year,
                    TotalDays = s.TotalDays, PresentDays = s.PresentDays, AbsentDays = s.AbsentDays,
                    LateDays = s.LateDays, LeaveDays = s.LeaveDays, HolidayDays = s.HolidayDays,
                    TotalWorkingHours = s.TotalWorkingHours
                });
            }
            return Result<IEnumerable<AttendanceSummaryDto>>.Success(dtos);
        }
        catch (Exception ex) { return Result<IEnumerable<AttendanceSummaryDto>>.Failure(ex.Message); }
    }

    public async Task<Result<DashboardStatsDto>> GetDashboardStatsAsync()
    {
        try
        {
            var today = DateTime.Today;
            var allEmp = await _uow.Employees.FindAsync(e => e.IsActive && !e.IsDeleted);
            int totalEmployees = allEmp.Count();
            var presentToday = await _uow.Attendance.GetPresentCountTodayAsync(today);
            var lateToday    = await _uow.Attendance.GetLateCountTodayAsync(today);
            var onLeave = (await _uow.Leaves.FindAsync(
                    l => l.Status == LeaveStatus.Approved
                         && l.FromDate.Date <= today && l.ToDate.Date >= today && !l.IsDeleted)).Count();
            var absentToday = Math.Max(0, totalEmployees - presentToday - onLeave);
            var pct = totalEmployees > 0 ? Math.Round((double)presentToday / totalEmployees * 100, 1) : 0;

            var recentLogs = await _uow.Attendance.GetByDateAsync(today);
            var dtos = new List<AttendanceLogDto>();
            foreach (var log in recentLogs.Take(10)) dtos.Add(await BuildLogDtoAsync(log));

            return Result<DashboardStatsDto>.Success(new DashboardStatsDto
            {
                TotalEmployees = totalEmployees, PresentToday = presentToday,
                AbsentToday = absentToday, LateToday = lateToday,
                OnLeaveToday = onLeave, AttendancePercentage = pct,
                RecentAttendance = dtos
            });
        }
        catch (Exception ex) { AppLogger.Error("AttendanceService.GetDashboardStatsAsync", ex); return Result<DashboardStatsDto>.Failure(ex.Message); }
    }

    private async Task<AttendanceLogDto> BuildLogDtoAsync(AttendanceLog log)
    {
        var emp  = log.Employee ?? await _uow.Employees.GetByIdAsync(log.EmployeeId);
        var dept = emp != null ? await _uow.Departments.GetByIdAsync(emp.DepartmentId) : null;
        return new AttendanceLogDto
        {
            Id = log.Id, EmployeeId = log.EmployeeId,
            EmployeeCode = emp?.EmployeeCode ?? string.Empty,
            EmployeeName = emp != null ? $"{emp.FirstName} {emp.LastName}" : string.Empty,
            Department = dept?.Name ?? string.Empty,
            AttendanceDate = log.AttendanceDate, CheckIn = log.CheckIn, CheckOut = log.CheckOut,
            Status = log.Status, IsLate = log.IsLate, IsEarlyLeave = log.IsEarlyLeave,
            LateMinutes = log.LateMinutes, EarlyLeaveMinutes = log.EarlyLeaveMinutes,
            WorkingHours = log.WorkingHours, Remarks = log.Remarks, IsManual = log.IsManual
        };
    }
}

