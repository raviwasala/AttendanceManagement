using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Common.Constants;
using AttendanceSystem.Common.Helpers;
using AttendanceSystem.Common.Logging;
using AttendanceSystem.Common.Models;
using AttendanceSystem.Common.Session;
using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Domain.Enums;
using AttendanceSystem.Domain.Interfaces;

namespace AttendanceSystem.Application.Services;

/// <summary>
/// Attendance review: what each employee was rostered to work, next to what the fingerprint
/// device actually recorded, with the in/out times correctable.
///
/// Works over a date range, so it serves both "everyone today" and "one employee this month".
///
/// Corrections re-derive late, early-leave, working hours and status from the shift in force
/// on that date. That matters: AttendanceService.EditAsync used to recompute working hours but
/// leave IsLate and LateMinutes untouched, so correcting a check-in from 09:30 to 09:05 left
/// the employee still flagged late by 15 minutes. A correction screen that leaves stale derived
/// values behind is worse than no correction screen.
/// </summary>
public class AttendanceReviewService : IAttendanceReviewService
{
    /// <summary>
    /// Ceiling on rows returned. employees × days grows quickly — 500 staff over a month is
    /// 15,000 rows, which is neither useful on screen nor kind to the browser. Better to cap
    /// and say so than to hang.
    /// </summary>
    private const int MaxRows = 5000;

    private const int MaxRangeDays = 366;

    private readonly IUnitOfWork _uow;
    private readonly IAuditService _audit;
    private readonly ICurrentUserContext _currentUser;

    public AttendanceReviewService(IUnitOfWork uow, IAuditService audit, ICurrentUserContext currentUser)
    {
        _uow = uow;
        _audit = audit;
        _currentUser = currentUser;
    }

    public Task<Result<AttendanceReviewDto>> GetDailyReviewAsync(DateTime date, int? departmentId = null) =>
        GetReviewAsync(date, date, null, departmentId);

    /// <summary>
    /// The review grid, optionally one page of it.
    ///
    /// Paging here is different in kind from the other list screens. A row is not a database
    /// row — it is one employee on one day, generated whether or not any attendance exists, so
    /// there is nothing to SKIP/TAKE in SQL. The grid is still assembled in memory (bounded by
    /// MaxRangeDays and MaxRows), the header statistics are computed over all of it, and only
    /// the requested page is returned.
    ///
    /// That is deliberately a partial fix: it removes the cost that actually hurt — thousands of
    /// rows of markup, each carrying two editable time inputs — without pretending the server
    /// stopped building the grid.
    /// </summary>
    public async Task<Result<AttendanceReviewDto>> GetReviewAsync(
        DateTime fromDate, DateTime toDate, int? employeeId = null, int? departmentId = null,
        PageRequest? page = null)
    {
        try
        {
            var from = fromDate.Date;
            var to = toDate.Date;
            if (to < from) (from, to) = (to, from);

            var dayCount = (int)(to - from).TotalDays + 1;
            if (dayCount > MaxRangeDays)
                return Result<AttendanceReviewDto>.Failure($"Range cannot exceed {MaxRangeDays} days.");

            // ── Load everything once; the grid is assembled in memory ────────────
            var employees = (await _uow.Employees.FindAsync(e =>
                e.IsActive && !e.IsDeleted &&
                (employeeId == null || e.Id == employeeId) &&
                (departmentId == null || e.DepartmentId == departmentId))).ToList();

            if (employees.Count == 0)
                return Result<AttendanceReviewDto>.Success(EmptyResult(from, to, dayCount));

            var employeeIds = employees.Select(e => e.Id).ToHashSet();
            var departments = (await _uow.Departments.GetAllAsync()).ToDictionary(d => d.Id, d => d.Name);
            var shifts = (await _uow.Shifts.GetAllAsync()).ToDictionary(s => s.Id);

            var logs = (await _uow.Attendance.FindAsync(a =>
                    !a.IsDeleted && a.AttendanceDate >= from && a.AttendanceDate <= to))
                .Where(a => employeeIds.Contains(a.EmployeeId))
                .ToDictionary(a => (a.EmployeeId, a.AttendanceDate.Date));

            var assignments = (await _uow.EmployeeShifts.FindAsync(es =>
                    !es.IsDeleted && es.EffectiveFrom <= to &&
                    (es.EffectiveTo == null || es.EffectiveTo >= from)))
                .Where(es => employeeIds.Contains(es.EmployeeId))
                .ToList();

            var holidays = (await _uow.Holidays.FindAsync(h =>
                    !h.IsDeleted && h.HolidayDate >= from && h.HolidayDate <= to))
                .GroupBy(h => h.HolidayDate.Date)
                .ToDictionary(g => g.Key, g => g.First().Name);

            var leaves = (await _uow.Leaves.FindAsync(l =>
                    l.Status == LeaveStatus.Approved && !l.IsDeleted &&
                    l.FromDate.Date <= to && l.ToDate.Date >= from))
                .Where(l => employeeIds.Contains(l.EmployeeId))
                .ToList();

            // "Third late day this month" has to count from the 1st, not from the start of
            // whatever range is on screen — otherwise looking at 20–25 August would report
            // everyone as being on their first late arrival.
            var lateOccurrence = await BuildLateOccurrenceAsync(employeeIds, from, to);

            var dto = new AttendanceReviewDto
            {
                FromDate = from,
                ToDate = to,
                DayCount = dayCount,
                IsRange = dayCount > 1,
                TotalEmployees = employees.Count,
                RangeDisplay = dayCount == 1
                    ? from.ToString("dddd, dd MMMM yyyy")
                    : $"{from:dd MMM yyyy} – {to:dd MMM yyyy} ({dayCount} days)"
            };

            // Ordered by employee then date when a single person is in view, and by date then
            // employee otherwise — matching how each is actually read.
            var ordered = employees.OrderBy(e => e.FirstName).ThenBy(e => e.LastName).ToList();

            for (var date = from; date <= to; date = date.AddDays(1))
            {
                foreach (var emp in ordered)
                {
                    if (dto.Rows.Count >= MaxRows) { dto.Truncated = true; break; }

                    logs.TryGetValue((emp.Id, date), out var log);
                    var shift = ResolveShift(assignments, shifts, emp.Id, date);

                    dto.Rows.Add(BuildRow(emp, date, shift, log, departments, holidays, leaves,
                        lateOccurrence));
                }
                if (dto.Truncated) break;
            }

            if (employeeId.HasValue)
                dto.Rows = dto.Rows.OrderBy(r => r.Date).ToList();

            // Statistics are computed over the whole grid, before it is cut down to a page —
            // header tiles that described only the visible page would be worse than none.
            dto.Present = dto.Rows.Count(r => r.Status == AttendanceStatus.Present);
            dto.Late = dto.Rows.Count(r => r.Status == AttendanceStatus.Late);
            dto.Absent = dto.Rows.Count(r => r.Status == AttendanceStatus.Absent);
            dto.OnLeave = dto.Rows.Count(r => r.Status == AttendanceStatus.OnLeave);
            dto.MissingCheckOut = dto.Rows.Count(r => r.CheckIn.HasValue && !r.CheckOut.HasValue);
            dto.TotalLateMinutes = dto.Rows.Sum(r => r.LateMinutes ?? 0);
            dto.TotalWorkingHours = Math.Round(dto.Rows.Sum(r => r.WorkingHours ?? 0), 1);
            dto.TotalOvertimeMinutes = dto.Rows.Sum(r => r.OvertimeMinutes ?? 0);
            dto.OverLateAllowance = dto.Rows.Count(r => r.IsOverLateAllowance);

            dto.TotalRows = dto.Rows.Count;
            if (page is { PageSize: > 0 })
            {
                dto.Page = page.Page;
                dto.PageSize = page.PageSize;
                dto.Rows = dto.Rows.Skip(page.Skip).Take(page.PageSize).ToList();
            }
            else
            {
                dto.Page = 1;
                dto.PageSize = 0;
            }

            return Result<AttendanceReviewDto>.Success(dto);
        }
        catch (Exception ex)
        {
            AppLogger.Error("AttendanceReviewService.GetReviewAsync", ex);
            return Result<AttendanceReviewDto>.Failure("Failed to load the attendance review.");
        }
    }

    public async Task<Result<AttendanceReviewRowDto>> SaveEntryAsync(SaveAttendanceEntryDto dto)
    {
        try
        {
            var day = dto.Date.Date;

            var employee = await _uow.Employees.GetByIdAsync(dto.EmployeeId);
            if (employee == null) return Result<AttendanceReviewRowDto>.Failure("Employee not found.");

            if (!TryParseTime(dto.CheckInTime, day, out var checkIn))
                return Result<AttendanceReviewRowDto>.Failure("Check-in time is not valid. Use HH:mm.");
            if (!TryParseTime(dto.CheckOutTime, day, out var checkOut))
                return Result<AttendanceReviewRowDto>.Failure("Check-out time is not valid. Use HH:mm.");

            // On a night shift the clock time of the check-out belongs to the following day —
            // 21:55 in, 07:30 out. Both parse onto the attendance date, so the out time has to
            // be rolled forward or it reads as "before check-in" and gets rejected.
            var shiftForDay = await ResolveShiftAsync(dto.EmployeeId, day);
            if (checkIn.HasValue && checkOut.HasValue && checkOut <= checkIn &&
                shiftForDay != null && AttendanceCalculator.CrossesMidnight(shiftForDay))
            {
                checkOut = checkOut.Value.AddDays(1);
            }

            if (checkIn.HasValue && checkOut.HasValue && checkOut < checkIn)
                return Result<AttendanceReviewRowDto>.Failure(
                    shiftForDay != null && AttendanceCalculator.CrossesMidnight(shiftForDay)
                        ? "Check-out cannot be before check-in."
                        : "Check-out cannot be before check-in. If this shift runs past midnight, mark it as a night shift.");

            if (!checkIn.HasValue && checkOut.HasValue)
                return Result<AttendanceReviewRowDto>.Failure("Cannot record a check-out without a check-in.");

            var log = await _uow.Attendance.GetTodayAttendanceAsync(dto.EmployeeId, day);

            // Clearing both times removes the record rather than leaving an empty one that
            // would read as "present with no times".
            if (!checkIn.HasValue && !checkOut.HasValue)
            {
                if (log != null)
                {
                    // The whole record goes in OldValues: after this there is nothing left to
                    // compare against, and "what was deleted" is the entire point of the entry.
                    var cleared = AuditSnapshot.Snapshot(log);

                    log.IsDeleted = true;
                    log.ModifiedBy = _currentUser.UserId;
                    log.ModifiedAt = DateTime.Now;
                    await _uow.Attendance.UpdateAsync(log);
                    await _uow.SaveChangesAsync();
                    await _audit.LogAsync(AppConstants.Modules.Attendance, "ClearEntry",
                        _currentUser.UserId, nameof(AttendanceLog), log.Id, cleared);
                }
                return await SingleRowAsync(dto.EmployeeId, day);
            }

            var isNew = log == null;
            // Captured before the edits below so the diff shows the times as the device
            // recorded them, against what a person changed them to.
            var before = isNew ? null : AuditSnapshot.Capture(log);

            if (isNew)
            {
                log = new AttendanceLog
                {
                    EmployeeId = dto.EmployeeId,
                    AttendanceDate = day,
                    CreatedBy = _currentUser.UserId,
                    CreatedAt = DateTime.Now
                };
            }

            log!.CheckIn = checkIn;
            log.CheckOut = checkOut;
            if (dto.Remarks != null) log.Remarks = dto.Remarks;
            log.IsManual = true;

            await RecalculateAsync(log, dto.Status);

            if (isNew)
            {
                await _uow.Attendance.AddAsync(log);
            }
            else
            {
                log.ModifiedBy = _currentUser.UserId;
                log.ModifiedAt = DateTime.Now;
                await _uow.Attendance.UpdateAsync(log);
            }

            await _uow.SaveChangesAsync();

            // A create has no "before", so the new record is recorded whole.
            var (oldValues, newValues) = isNew
                ? (null, AuditSnapshot.Snapshot(log))
                : AuditSnapshot.DiffAgainst(before!, log);

            await _audit.LogAsync(AppConstants.Modules.Attendance, isNew ? "CreateEntry" : "EditEntry",
                _currentUser.UserId, nameof(AttendanceLog), log.Id, oldValues, newValues);

            return await SingleRowAsync(dto.EmployeeId, day);
        }
        catch (Exception ex)
        {
            AppLogger.Error("AttendanceReviewService.SaveEntryAsync", ex);
            return Result<AttendanceReviewRowDto>.Failure("Failed to save the attendance entry.");
        }
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    private static AttendanceReviewDto EmptyResult(DateTime from, DateTime to, int dayCount) => new()
    {
        FromDate = from,
        ToDate = to,
        DayCount = dayCount,
        IsRange = dayCount > 1,
        RangeDisplay = dayCount == 1
            ? from.ToString("dddd, dd MMMM yyyy")
            : $"{from:dd MMM yyyy} – {to:dd MMM yyyy} ({dayCount} days)"
    };

    /// <summary>
    /// Numbers each late arrival within its calendar month: 1 for the first, 2 for the second.
    ///
    /// Deliberately widened to the start of the month containing <paramref name="from"/>, since
    /// the allowance is a monthly one and the range on screen is usually only part of a month.
    /// </summary>
    private async Task<Dictionary<(int, DateTime), int>> BuildLateOccurrenceAsync(
        HashSet<int> employeeIds, DateTime from, DateTime to)
    {
        var monthStart = new DateTime(from.Year, from.Month, 1);

        var lateLogs = (await _uow.Attendance.FindAsync(a =>
                !a.IsDeleted && a.IsLate &&
                a.AttendanceDate >= monthStart && a.AttendanceDate <= to))
            .Where(a => employeeIds.Contains(a.EmployeeId))
            .ToList();

        var map = new Dictionary<(int, DateTime), int>();
        foreach (var group in lateLogs.GroupBy(a => (a.EmployeeId, a.AttendanceDate.Year, a.AttendanceDate.Month)))
        {
            var n = 0;
            foreach (var log in group.OrderBy(a => a.AttendanceDate).ThenBy(a => a.CheckIn))
                map[(log.EmployeeId, log.AttendanceDate.Date)] = ++n;
        }

        return map;
    }

    private static AttendanceReviewRowDto BuildRow(
        Employee emp, DateTime date, Shift? shift, AttendanceLog? log,
        Dictionary<int, string> departments, Dictionary<DateTime, string> holidays,
        List<LeaveRequest> leaves, Dictionary<(int, DateTime), int> lateOccurrence)
    {
        var row = new AttendanceReviewRowDto
        {
            Date = date,
            DateDisplay = date.ToString("ddd dd MMM"),
            EmployeeId = emp.Id,
            EmployeeCode = emp.EmployeeCode,
            EmployeeName = $"{emp.FirstName} {emp.LastName}",
            Department = departments.TryGetValue(emp.DepartmentId, out var dn) ? dn : string.Empty,
            IsHoliday = holidays.ContainsKey(date),
            HolidayName = holidays.TryGetValue(date, out var hn) ? hn : null
        };

        if (shift != null)
        {
            row.ShiftId = shift.Id;
            row.ShiftName = shift.Name;
            row.ExpectedIn = Fmt(shift.StartTime);
            row.ExpectedOut = Fmt(shift.EndTime);
            row.GraceMinutes = shift.GraceMinutes;
            row.LateAllowance = shift.AllowedLateDaysPerMonth;
            row.IsNightShift = AttendanceCalculator.CrossesMidnight(shift);
            row.BreakMinutes = shift.BreakMinutes;
            row.IsWeeklyOff = IsWeeklyOff(shift, date);
        }
        else
        {
            row.HasNoShift = true;
        }

        if (log != null)
        {
            row.AttendanceId = log.Id;
            row.CheckIn = log.CheckIn;
            row.CheckOut = log.CheckOut;
            row.CheckInTime = log.CheckIn?.ToString("HH:mm");
            row.CheckOutTime = log.CheckOut?.ToString("HH:mm");
            row.IsLate = log.IsLate;
            row.LateMinutes = log.LateMinutes;
            if (log.IsLate && lateOccurrence.TryGetValue((emp.Id, date), out var occurrence))
                row.LateOccurrence = occurrence;
            row.IsEarlyLeave = log.IsEarlyLeave;
            row.EarlyLeaveMinutes = log.EarlyLeaveMinutes;
            row.WorkingHours = log.WorkingHours;
            row.GrossHours = log.GrossHours;
            row.OvertimeMinutes = log.OvertimeMinutes;
            row.Status = log.Status;
            row.IsManual = log.IsManual;
            row.Remarks = log.Remarks;
        }
        else
        {
            var onLeave = leaves.Any(l => l.EmployeeId == emp.Id &&
                                          l.FromDate.Date <= date && l.ToDate.Date >= date);

            // Derived, not stored — same precedence the check-in path uses.
            row.Status = onLeave          ? AttendanceStatus.OnLeave
                       : row.IsHoliday    ? AttendanceStatus.Holiday
                       : row.IsWeeklyOff  ? AttendanceStatus.WeeklyOff
                                          : AttendanceStatus.Absent;
        }

        return row;
    }

    /// <summary>
    /// Re-derives every value that depends on the times or the shift.
    ///
    /// Mirrors the precedence in AttendanceService.CheckInAsync exactly — holiday, then
    /// weekly off, then late, then present. If that order ever changes, this must change
    /// with it or corrected records will disagree with device-captured ones.
    /// </summary>
    private async Task RecalculateAsync(AttendanceLog log, AttendanceStatus? explicitStatus)
    {
        var day = log.AttendanceDate.Date;
        var shift = await ResolveShiftAsync(log.EmployeeId, day);
        var isHoliday = await _uow.Holidays.IsHolidayAsync(day);

        var result = AttendanceCalculator.Calculate(
            shift, day, log.CheckIn, log.CheckOut, isHoliday);

        AttendanceCalculator.Apply(log, result, explicitStatus);
    }

    private async Task<Shift?> ResolveShiftAsync(int employeeId, DateTime date)
    {
        var assignments = await _uow.EmployeeShifts.FindAsync(es =>
            es.EmployeeId == employeeId && !es.IsDeleted &&
            es.EffectiveFrom <= date && (es.EffectiveTo == null || es.EffectiveTo >= date));

        var current = assignments.OrderByDescending(a => a.EffectiveFrom).FirstOrDefault();
        return current == null ? null : await _uow.Shifts.GetByIdAsync(current.ShiftId);
    }

    /// <summary>In-memory variant used when building the grid, to avoid a query per row.</summary>
    private static Shift? ResolveShift(
        List<EmployeeShift> assignments, Dictionary<int, Shift> shifts, int employeeId, DateTime date)
    {
        var current = assignments
            .Where(a => a.EmployeeId == employeeId &&
                        a.EffectiveFrom.Date <= date &&
                        (a.EffectiveTo == null || a.EffectiveTo.Value.Date >= date))
            .OrderByDescending(a => a.EffectiveFrom)
            .FirstOrDefault();

        return current != null && shifts.TryGetValue(current.ShiftId, out var s) ? s : null;
    }

    /// <summary>Reloads one row so the grid can update in place after a save.</summary>
    private async Task<Result<AttendanceReviewRowDto>> SingleRowAsync(int employeeId, DateTime day)
    {
        var review = await GetReviewAsync(day, day, employeeId);
        if (!review.IsSuccess) return Result<AttendanceReviewRowDto>.Failure(review.ErrorMessage!);

        var row = review.Data!.Rows.FirstOrDefault(r => r.EmployeeId == employeeId);
        return row == null
            ? Result<AttendanceReviewRowDto>.Failure("Employee not found in the review.")
            : Result<AttendanceReviewRowDto>.Success(row);
    }

    /// <summary>Parses "HH:mm" onto the given date. Blank is a valid "not recorded".</summary>
    private static bool TryParseTime(string? time, DateTime day, out DateTime? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(time)) return true;

        if (!TimeSpan.TryParse(time.Trim(), out var ts)) return false;
        if (ts < TimeSpan.Zero || ts >= TimeSpan.FromHours(24)) return false;

        result = day.Add(ts);
        return true;
    }

    private static bool IsWeeklyOff(Shift shift, DateTime date) =>
        AttendanceCalculator.IsWeeklyOff(shift, date);

    private static string Fmt(TimeSpan t) => DateTime.Today.Add(t).ToString("HH:mm");
}
