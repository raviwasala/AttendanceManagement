using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Common.Constants;
using AttendanceSystem.Common.Logging;
using AttendanceSystem.Common.Models;
using AttendanceSystem.Common.Session;
using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Domain.Interfaces;

namespace AttendanceSystem.Application.Services;

/// <summary>
/// Monthly shift roster: shows which shift each employee works on each day, and lets
/// individual days be changed.
///
/// No new storage. A per-day change is an <see cref="EmployeeShift"/> whose EffectiveFrom
/// and EffectiveTo are both that date. Shift resolution already picks the assignment with
/// the latest EffectiveFrom among those covering a date, so a single-day row naturally
/// outranks a long-running one — but only on that day. Clearing the override deletes just
/// that row and the day falls back to the normal assignment.
///
/// That rule is the contract between this service and AttendanceService.CheckInAsync. If
/// resolution there ever changes, this screen silently starts lying.
/// </summary>
public class ShiftRosterService : IShiftRosterService
{
    private readonly IUnitOfWork _uow;
    private readonly IAuditService _audit;
    private readonly ICurrentUserContext _currentUser;

    public ShiftRosterService(IUnitOfWork uow, IAuditService audit, ICurrentUserContext currentUser)
    {
        _uow = uow;
        _audit = audit;
        _currentUser = currentUser;
    }

    /// <summary>
    /// The month's roster, one page of employees at a time.
    ///
    /// A page here is a page of <em>employees</em>, not of cells: the day columns always cover
    /// the whole month, because a roster split across pages by date would be unreadable. That
    /// still matters — every row carries a cell per day, so a site with 5,000 staff was asking
    /// the browser to lay out 155,000 clickable cells in one go.
    /// </summary>
    public async Task<Result<ShiftRosterDto>> GetMonthlyRosterAsync(int year, int month, int? departmentId = null,
        string? search = null, int? employeeId = null, int? shiftId = null, PageRequest? page = null)
    {
        try
        {
            if (month is < 1 or > 12) return Result<ShiftRosterDto>.Failure("Month must be between 1 and 12.");
            if (year is < 2000 or > 2100) return Result<ShiftRosterDto>.Failure("Year is out of range.");

            var daysInMonth = DateTime.DaysInMonth(year, month);
            var monthStart = new DateTime(year, month, 1);
            var monthEnd = monthStart.AddDays(daysInMonth - 1);

            // Everything loaded once; the grid is then assembled in memory. Resolving a shift
            // per employee per day against the database would be 11 x 31 round trips.
            var employees = (await _uow.Employees.FindAsync(e =>
                e.IsActive && !e.IsDeleted &&
                (departmentId == null || e.DepartmentId == departmentId) &&
                (employeeId == null || e.Id == employeeId))).ToList();

            // Free-text filter is applied in memory: it has to match the code or either name
            // part or the two joined, and the joined form does not translate to SQL.
            var term = search?.Trim();
            if (!string.IsNullOrEmpty(term))
            {
                employees = employees.Where(e =>
                    Contains(e.EmployeeCode, term) ||
                    Contains(e.FirstName, term) ||
                    Contains(e.LastName, term) ||
                    Contains($"{e.FirstName} {e.LastName}", term)).ToList();
            }

            var employeeIds = employees.Select(e => e.Id).ToHashSet();
            var departments = (await _uow.Departments.GetAllAsync()).ToDictionary(d => d.Id, d => d.Name);
            var shifts = (await _uow.Shifts.GetAllAsync()).ToDictionary(s => s.Id);

            // Any assignment overlapping the month, plus open-ended ones starting earlier.
            var assignments = (await _uow.EmployeeShifts.FindAsync(es =>
                !es.IsDeleted && es.EffectiveFrom <= monthEnd &&
                (es.EffectiveTo == null || es.EffectiveTo >= monthStart)))
                .Where(es => employeeIds.Contains(es.EmployeeId))
                .ToList();

            var holidays = (await _uow.Holidays.FindAsync(h =>
                !h.IsDeleted && h.HolidayDate >= monthStart && h.HolidayDate <= monthEnd))
                .GroupBy(h => h.HolidayDate.Date)
                .ToDictionary(g => g.Key, g => g.First().Name);

            var dto = new ShiftRosterDto
            {
                Year = year,
                Month = month,
                MonthName = monthStart.ToString("MMMM yyyy"),
                DaysInMonth = daysInMonth,
                AvailableShifts = shifts.Values
                    .Where(s => s.IsActive)
                    .OrderBy(s => s.StartTime)
                    .Select(MapShift)
                    .ToList()
            };

            page ??= new PageRequest { Page = 1, PageSize = 0 };

            var ordered = employees.OrderBy(e => e.FirstName).ThenBy(e => e.LastName).ToList();

            // Counted over everyone who matched the filters, not the page — a header saying
            // "3 without a shift" that only meant "on this page" would send people hunting.
            var assignedIds = assignments.Select(a => a.EmployeeId).ToHashSet();
            dto.EmployeesWithoutAssignment = ordered.Count(e => !assignedIds.Contains(e.Id));

            // The shift filter needs the resolved shift for each day, which only exists once a
            // row is built — so it cannot be pushed into the page slice. Narrowing to employees
            // who at least *hold* an assignment to that shift keeps the number of rows that
            // have to be built small, and the exact per-day test still runs below.
            if (shiftId.HasValue)
            {
                var candidates = assignments
                    .Where(a => a.ShiftId == shiftId.Value)
                    .Select(a => a.EmployeeId)
                    .ToHashSet();
                ordered = ordered.Where(e => candidates.Contains(e.Id)).ToList();
            }

            // Without a shift filter the page can be taken before any row is built, so only the
            // rows actually shown are assembled.
            var buildFor = shiftId.HasValue || page.PageSize <= 0
                ? ordered
                : ordered.Skip(page.Skip).Take(page.PageSize).ToList();

            foreach (var emp in buildFor)
            {
                var mine = assignments.Where(a => a.EmployeeId == emp.Id).ToList();

                var row = new RosterEmployeeDto
                {
                    EmployeeId = emp.Id,
                    EmployeeCode = emp.EmployeeCode,
                    EmployeeName = $"{emp.FirstName} {emp.LastName}",
                    Department = departments.TryGetValue(emp.DepartmentId, out var dn) ? dn : string.Empty
                };

                // The "normal" shift is the longest-running assignment covering the month —
                // i.e. the one that is not a single-day override.
                var baseAssignment = mine
                    .Where(a => a.EffectiveTo == null || a.EffectiveTo.Value.Date != a.EffectiveFrom.Date)
                    .OrderByDescending(a => a.EffectiveFrom)
                    .FirstOrDefault();

                if (baseAssignment != null && shifts.TryGetValue(baseAssignment.ShiftId, out var baseShift))
                    row.DefaultShiftName = baseShift.Name;

                row.HasNoAssignment = mine.Count == 0;

                for (var day = 1; day <= daysInMonth; day++)
                {
                    var date = new DateTime(year, month, day);

                    // Same rule as AttendanceService.CheckInAsync: latest EffectiveFrom wins.
                    var effective = mine
                        .Where(a => a.EffectiveFrom.Date <= date &&
                                   (a.EffectiveTo == null || a.EffectiveTo.Value.Date >= date))
                        .OrderByDescending(a => a.EffectiveFrom)
                        .FirstOrDefault();

                    var cell = new RosterDayDto
                    {
                        Date = date,
                        Day = day,
                        DayOfWeek = date.DayOfWeek.ToString().Substring(0, 3),
                        IsHoliday = holidays.ContainsKey(date),
                        HolidayName = holidays.TryGetValue(date, out var hn) ? hn : null
                    };

                    if (effective != null && shifts.TryGetValue(effective.ShiftId, out var shift))
                    {
                        cell.ShiftId = shift.Id;
                        cell.ShiftName = shift.Name;
                        cell.ShiftTimes = $"{Fmt(shift.StartTime)}–{Fmt(shift.EndTime)}";
                        cell.AssignmentId = effective.Id;
                        cell.IsOverride = IsSingleDay(effective);
                        cell.IsWeeklyOff = IsWeeklyOff(shift, date);
                    }

                    row.Days.Add(cell);
                }

                // Shift filter keeps anyone who works that shift on at least one day of the
                // month, so a one-day override is not missed the way a base-shift-only test
                // would miss it — an assignment can be entirely shadowed by a later one.
                if (shiftId == null || row.Days.Any(d => d.ShiftId == shiftId))
                    dto.Employees.Add(row);
            }

            dto.Page = page.Page;
            dto.PageSize = page.PageSize;

            if (shiftId.HasValue)
            {
                // Rows were built for every candidate, so the exact count is known and the page
                // is taken here instead.
                dto.TotalEmployees = dto.Employees.Count;
                if (page.PageSize > 0)
                    dto.Employees = dto.Employees.Skip(page.Skip).Take(page.PageSize).ToList();
            }
            else
            {
                dto.TotalEmployees = ordered.Count;
            }

            return Result<ShiftRosterDto>.Success(dto);
        }
        catch (Exception ex)
        {
            AppLogger.Error("ShiftRosterService.GetMonthlyRosterAsync", ex);
            return Result<ShiftRosterDto>.Failure("Failed to load the shift roster.");
        }
    }

    public async Task<Result> SetDayAsync(SetRosterDayDto dto)
    {
        try
        {
            var result = await ApplyDayAsync(dto.EmployeeId, dto.Date.Date, dto.ShiftId);
            if (!result.IsSuccess) return result;

            await _uow.SaveChangesAsync();
            await _audit.LogAsync(AppConstants.Modules.Shifts, "SetRosterDay",
                _currentUser.UserId, nameof(EmployeeShift), dto.EmployeeId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            AppLogger.Error("ShiftRosterService.SetDayAsync", ex);
            return Result.Failure("Failed to update the shift for that day.");
        }
    }

    public async Task<Result> SetRangeAsync(SetRosterRangeDto dto)
    {
        try
        {
            var from = dto.FromDate.Date;
            var to = dto.ToDate.Date;
            if (to < from) return Result.Failure("End date must be on or after the start date.");
            if ((to - from).TotalDays > 366) return Result.Failure("Range cannot exceed one year.");

            Shift? shift = null;
            if (dto.ShiftId.HasValue)
            {
                shift = await _uow.Shifts.GetByIdAsync(dto.ShiftId.Value);
                if (shift == null) return Result.Failure("Selected shift does not exist.");
            }

            for (var d = from; d <= to; d = d.AddDays(1))
            {
                if (dto.SkipWeeklyOff && shift != null && IsWeeklyOff(shift, d)) continue;

                var r = await ApplyDayAsync(dto.EmployeeId, d, dto.ShiftId);
                if (!r.IsSuccess) return r;
            }

            // One commit for the whole range: a half-applied week is worse than none.
            await _uow.SaveChangesAsync();
            await _audit.LogAsync(AppConstants.Modules.Shifts, "SetRosterRange",
                _currentUser.UserId, nameof(EmployeeShift), dto.EmployeeId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            AppLogger.Error("ShiftRosterService.SetRangeAsync", ex);
            return Result.Failure("Failed to apply the shift across the range.");
        }
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Stages the change for one day. Does not save — callers commit, so a range is atomic.
    ///
    /// A null shiftId removes any single-day override for that date. It deliberately does not
    /// touch a longer-running assignment: clearing a day means "go back to normal", not
    /// "delete this employee's shift".
    /// </summary>
    private async Task<Result> ApplyDayAsync(int employeeId, DateTime date, int? shiftId)
    {
        var employee = await _uow.Employees.GetByIdAsync(employeeId);
        if (employee == null) return Result.Failure("Employee not found.");

        var existingOverride = (await _uow.EmployeeShifts.FindAsync(es =>
                es.EmployeeId == employeeId && !es.IsDeleted &&
                es.EffectiveFrom == date && es.EffectiveTo == date))
            .FirstOrDefault();

        if (!shiftId.HasValue)
        {
            if (existingOverride != null)
            {
                existingOverride.IsDeleted = true;
                existingOverride.ModifiedBy = _currentUser.UserId;
                existingOverride.ModifiedAt = DateTime.Now;
                await _uow.EmployeeShifts.UpdateAsync(existingOverride);
            }
            return Result.Success();
        }

        var shift = await _uow.Shifts.GetByIdAsync(shiftId.Value);
        if (shift == null) return Result.Failure("Selected shift does not exist.");

        if (existingOverride != null)
        {
            existingOverride.ShiftId = shiftId.Value;
            existingOverride.ModifiedBy = _currentUser.UserId;
            existingOverride.ModifiedAt = DateTime.Now;
            await _uow.EmployeeShifts.UpdateAsync(existingOverride);
        }
        else
        {
            await _uow.EmployeeShifts.AddAsync(new EmployeeShift
            {
                EmployeeId = employeeId,
                ShiftId = shiftId.Value,
                EffectiveFrom = date,
                EffectiveTo = date,          // from == to marks this as a single-day override
                CreatedBy = _currentUser.UserId,
                CreatedAt = DateTime.Now
            });
        }

        return Result.Success();
    }

    private static bool IsSingleDay(EmployeeShift a) =>
        a.EffectiveTo.HasValue && a.EffectiveTo.Value.Date == a.EffectiveFrom.Date;

    private static bool IsWeeklyOff(Shift shift, DateTime date) =>
        shift.WeeklyOffDays
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(d => d.Trim())
            .Contains(date.DayOfWeek.ToString(), StringComparer.OrdinalIgnoreCase);

    private static string Fmt(TimeSpan t) => DateTime.Today.Add(t).ToString("HH:mm");

    private static bool Contains(string? value, string term) =>
        !string.IsNullOrEmpty(value) && value.Contains(term, StringComparison.OrdinalIgnoreCase);

    private static ShiftDto MapShift(Shift s) => new()
    {
        Id = s.Id, Name = s.Name, StartTime = s.StartTime, EndTime = s.EndTime,
        GraceMinutes = s.GraceMinutes, WeeklyOffDays = s.WeeklyOffDays, IsActive = s.IsActive
    };
}
