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
    private readonly IAttendanceLockService _locks;
    private readonly IApprovalScopeService _scopes;

    public AttendanceService(IUnitOfWork uow, IAuditService audit, ICurrentUserContext currentUser,
                             IAttendanceLockService locks, IApprovalScopeService scopes)
    {
        _uow = uow; _audit = audit; _currentUser = currentUser; _locks = locks; _scopes = scopes;
    }

    /// <summary>
    /// Refuses a write into a closed period, returning null when the date is open.
    ///
    /// A lock is only worth anything if every door respects it. AttendanceReviewService
    /// enforced this on the review grid, but these methods sit behind a different endpoint
    /// on the same records — so a month that was closed and paid could still be edited or
    /// deleted through PUT/DELETE /api/attendance/{id}, which is precisely what locking is
    /// meant to prevent.
    /// </summary>
    private async Task<string?> LockRefusalAsync(int employeeId, DateTime date)
    {
        var employee = await _uow.Employees.GetByIdAsync(employeeId);
        var periodLock = await _locks.GetLockForAsync(date.Date, employee?.BranchId);
        if (periodLock == null) return null;

        return $"{date:dd-MMM-yyyy} is in a locked period " +
               $"({periodLock.FromDate:dd-MMM-yyyy} – {periodLock.ToDate:dd-MMM-yyyy}: {periodLock.Reason}). " +
               "Unlock it first if this really needs changing.";
    }

    public async Task<Result<AttendanceLogDto>> CheckInAsync(CheckInDto dto)
    {
        try
        {
            var today = dto.CheckInTime.Date;
            var existing = await _uow.Attendance.GetTodayAttendanceAsync(dto.EmployeeId, today);
            if (existing != null)
                return Result<AttendanceLogDto>.Failure("Employee has already checked in today.");

            // CheckInTime is supplied by the caller, so "today" is not necessarily today —
            // a back-dated punch lands in whatever period that date belongs to.
            var locked = await LockRefusalAsync(dto.EmployeeId, today);
            if (locked != null) return Result<AttendanceLogDto>.Failure(locked);

            var allShiftAssignments = await _uow.EmployeeShifts.FindAsync(
                es => es.EmployeeId == dto.EmployeeId && !es.IsDeleted
                      && es.EffectiveFrom <= today
                      && (es.EffectiveTo == null || es.EffectiveTo >= today));
            var shiftAssignment = allShiftAssignments.OrderByDescending(es => es.EffectiveFrom).FirstOrDefault();
            Shift? shift = shiftAssignment != null ? await _uow.Shifts.GetByIdAsync(shiftAssignment.ShiftId) : null;

            bool isHoliday = await _uow.Holidays.IsHolidayAsync(today);

            var log = new AttendanceLog
            {
                EmployeeId = dto.EmployeeId, AttendanceDate = today, CheckIn = dto.CheckInTime,
                Remarks = dto.Remarks, IsManual = true,
                CreatedBy = _currentUser.UserId, CreatedAt = DateTime.Now
            };

            // Shared with the edit and review paths so night-shift and overtime rules cannot
            // drift between how a punch is captured and how it is later corrected.
            AttendanceCalculator.Apply(log,
                AttendanceCalculator.Calculate(shift, today, log.CheckIn, null, isHoliday));
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

            var lockedOut = await LockRefusalAsync(log.EmployeeId, log.AttendanceDate);
            if (lockedOut != null) return Result<AttendanceLogDto>.Failure(lockedOut);

            log.CheckOut = dto.CheckOutTime;
            if (dto.Remarks != null) log.Remarks = dto.Remarks;

            var allShiftAssignments = await _uow.EmployeeShifts.FindAsync(
                es => es.EmployeeId == log.EmployeeId && !es.IsDeleted
                      && es.EffectiveFrom <= log.AttendanceDate
                      && (es.EffectiveTo == null || es.EffectiveTo >= log.AttendanceDate));
            var sa = allShiftAssignments.OrderByDescending(es => es.EffectiveFrom).FirstOrDefault();
            Shift? shift = sa != null ? await _uow.Shifts.GetByIdAsync(sa.ShiftId) : null;
            var isHolidayOut = await _uow.Holidays.IsHolidayAsync(log.AttendanceDate.Date);

            // Check-out is where overtime and the break deduction are first known.
            AttendanceCalculator.Apply(log,
                AttendanceCalculator.Calculate(
                    shift, log.AttendanceDate, log.CheckIn, log.CheckOut, isHolidayOut));

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

            var locked = await LockRefusalAsync(log.EmployeeId, log.AttendanceDate);
            if (locked != null) return Result.Failure(locked);

            // Snapshot before anything is touched: correcting a punch changes what somebody is
            // paid, so the trail has to show what the times were before the correction.
            var before = AuditSnapshot.Capture(log);

            log.CheckIn = dto.CheckIn; log.CheckOut = dto.CheckOut; log.Status = dto.Status;
            if (dto.Remarks != null) log.Remarks = dto.Remarks;
            log.WorkingHours = log.CheckIn.HasValue && log.CheckOut.HasValue
                ? DateHelper.CalculateWorkingHours(log.CheckIn, log.CheckOut)
                : null;

            // Lateness and early leave are derived from the shift, so a corrected time must
            // re-derive them. Previously only WorkingHours was recomputed, which left an
            // employee still flagged "late by 15 minutes" after their check-in was corrected
            // to a time inside the grace period.
            await ApplyShiftDerivedFieldsAsync(log);

            log.ModifiedBy = modifiedBy; log.ModifiedAt = DateTime.Now;
            await _uow.Attendance.UpdateAsync(log);
            await _uow.SaveChangesAsync();

            var (oldValues, newValues) = AuditSnapshot.DiffAgainst(before, log);
            await _audit.LogAsync("Attendance", "Edit", modifiedBy, "AttendanceLog", log.Id,
                oldValues, newValues);
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

            var locked = await LockRefusalAsync(log.EmployeeId, log.AttendanceDate);
            if (locked != null) return Result.Failure(locked);

            // Captured before the flag flips: a deleted record is invisible to the query
            // filters afterwards, so the trail is the only remaining evidence of what the
            // times were — and every other write path here records one.
            var before = AuditSnapshot.Snapshot(log);

            log.IsDeleted = true; log.ModifiedBy = deletedBy; log.ModifiedAt = DateTime.Now;
            await _uow.Attendance.UpdateAsync(log);
            await _uow.SaveChangesAsync();

            await _audit.LogAsync("Attendance", "Delete", deletedBy, "AttendanceLog", log.Id,
                oldValues: before);
            return Result.Success();
        }
        catch (Exception ex) { AppLogger.Error("AttendanceService.DeleteAsync", ex); return Result.Failure(ex.Message); }
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
            // Built from the attendance logs, not from AttendanceSummaries. Nothing in the
            // system ever writes to that table, so reading it left this screen permanently
            // empty apart from the seeded rows. See ReportService.GetMonthlyAttendanceReportAsync.
            if (month is < 1 or > 12) return Result<IEnumerable<AttendanceSummaryDto>>.Failure("Month must be between 1 and 12.");

            var monthStart = new DateTime(year, month, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);

            var employees = (await _uow.Employees.FindAsync(e => e.IsActive && !e.IsDeleted)).ToList();
            var departments = (await _uow.Departments.GetAllAsync()).ToDictionary(d => d.Id, d => d.Name);
            var logs = (await _uow.Attendance.FindAsync(a =>
                !a.IsDeleted && a.AttendanceDate >= monthStart && a.AttendanceDate <= monthEnd)).ToList();

            // The late allowance lives on the shift, so it is read from whichever assignment
            // covers the month. The last one wins if the shift changed mid-month — the figure
            // reported is then the allowance the employee finished the month on.
            var shifts = (await _uow.Shifts.GetAllAsync()).ToDictionary(s => s.Id);
            var assignments = (await _uow.EmployeeShifts.FindAsync(es => !es.IsDeleted &&
                    es.EffectiveFrom <= monthEnd &&
                    (es.EffectiveTo == null || es.EffectiveTo >= monthStart)))
                .OrderBy(es => es.EffectiveFrom)
                .ToList();

            var allowanceByEmployee = assignments
                .GroupBy(a => a.EmployeeId)
                .ToDictionary(
                    g => g.Key,
                    g => shifts.TryGetValue(g.Last().ShiftId, out var s) ? s.AllowedLateDaysPerMonth : 0);

            var dtos = employees
                .OrderBy(e => e.FirstName).ThenBy(e => e.LastName)
                .Select(emp =>
                {
                    var mine = logs.Where(l => l.EmployeeId == emp.Id).ToList();
                    return new AttendanceSummaryDto
                    {
                        EmployeeId = emp.Id,
                        EmployeeName = $"{emp.FirstName} {emp.LastName}",
                        EmployeeCode = emp.EmployeeCode,
                        Department = departments.TryGetValue(emp.DepartmentId, out var dn) ? dn : string.Empty,
                        Month = month, Year = year,
                        TotalDays = mine.Count,
                        // Late still counts as present — the person was at work.
                        PresentDays = mine.Count(l => l.Status is AttendanceStatus.Present or AttendanceStatus.Late),
                        AbsentDays = mine.Count(l => l.Status == AttendanceStatus.Absent),
                        LateDays = mine.Count(l => l.IsLate),
                        LeaveDays = mine.Count(l => l.Status == AttendanceStatus.OnLeave),
                        HolidayDays = mine.Count(l => l.Status is AttendanceStatus.Holiday or AttendanceStatus.WeeklyOff),
                        TotalWorkingHours = mine.Sum(l => l.WorkingHours ?? 0),
                        LateAllowance = allowanceByEmployee.TryGetValue(emp.Id, out var la) ? la : 0
                    };
                })
                .ToList();

            return Result<IEnumerable<AttendanceSummaryDto>>.Success(dtos);
        }
        catch (Exception ex) { return Result<IEnumerable<AttendanceSummaryDto>>.Failure(ex.Message); }
    }

    public async Task<Result<DashboardStatsDto>> GetDashboardStatsAsync()
    {
        try
        {
            var today = DateTime.Today;

            // Every figure below is counted over this set, so a department head's dashboard
            // describes their department rather than the company. Previously all of it was
            // company-wide for anyone who could open the page, which made the headline
            // numbers meaningless to everyone except an administrator.
            var scope = await _scopes.GetDataScopeAsync();

            var allEmp = (await _uow.Employees.FindAsync(e => e.IsActive && !e.IsDeleted))
                .Where(e => scope.Allows(e.Id, e.DepartmentId))
                .ToList();

            // Filtered in memory rather than through the repository's count methods, which
            // have no scope parameter. A few hundred employees and one day of punches is a
            // small set; a date range would need the filter pushed into SQL.
            var visibleIds = allEmp.Select(e => e.Id).ToHashSet();
            int totalEmployees = allEmp.Count;

            var todayLogs = (await _uow.Attendance.GetByDateAsync(today))
                .Where(a => visibleIds.Contains(a.EmployeeId))
                .ToList();

            var presentToday = todayLogs.Count(a => a.CheckIn.HasValue);
            var lateToday    = todayLogs.Count(a => a.IsLate);

            var onLeave = (await _uow.Leaves.FindAsync(
                    l => l.Status == LeaveStatus.Approved
                         && l.FromDate.Date <= today && l.ToDate.Date >= today && !l.IsDeleted))
                .Count(l => visibleIds.Contains(l.EmployeeId));

            var absentToday = Math.Max(0, totalEmployees - presentToday - onLeave);
            var pct = totalEmployees > 0 ? Math.Round((double)presentToday / totalEmployees * 100, 1) : 0;

            var dtos = new List<AttendanceLogDto>();
            foreach (var log in todayLogs.Take(10)) dtos.Add(await BuildLogDtoAsync(log));

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

    /// <summary>
    /// Recomputes the fields that depend on the employee's shift for the record's date:
    /// late minutes and early-leave minutes.
    ///
    /// Status is deliberately left alone here — EditAsync takes it from the caller, who may
    /// be marking someone On Leave or Holiday regardless of the times.
    /// </summary>
    private async Task ApplyShiftDerivedFieldsAsync(AttendanceLog log)
    {
        var date = log.AttendanceDate.Date;

        var assignments = await _uow.EmployeeShifts.FindAsync(es =>
            es.EmployeeId == log.EmployeeId && !es.IsDeleted &&
            es.EffectiveFrom <= date && (es.EffectiveTo == null || es.EffectiveTo >= date));

        var current = assignments.OrderByDescending(a => a.EffectiveFrom).FirstOrDefault();
        var shift = current != null ? await _uow.Shifts.GetByIdAsync(current.ShiftId) : null;
        var isHoliday = await _uow.Holidays.IsHolidayAsync(date);

        var result = AttendanceCalculator.Calculate(shift, date, log.CheckIn, log.CheckOut, isHoliday);

        // Status is preserved: EditAsync takes it from the caller, who may be marking someone
        // On Leave regardless of the times.
        AttendanceCalculator.Apply(log, result, log.Status);
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

