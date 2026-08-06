using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Common.Logging;
using AttendanceSystem.Common.Models;
using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Domain.Enums;
using AttendanceSystem.Domain.Interfaces;

namespace AttendanceSystem.Application.Services;

/// <summary>
/// Aggregate read models for the dashboard.
///
/// Every method loads its lookup tables (employees, departments, shifts) once and joins in
/// memory. The repositories return entities without navigation properties populated, so
/// resolving a name per row would issue a query per row — with a month of records that is
/// thousands of round trips. The datasets here are small enough that one pass each is cheaper
/// and far more predictable.
/// </summary>
public class AnalyticsService : IAnalyticsService
{
    private const int MaxTrendDays = 90;

    private readonly IUnitOfWork _uow;
    private readonly IApprovalScopeService _scopes;

    public AnalyticsService(IUnitOfWork uow, IApprovalScopeService scopes)
    {
        _uow = uow;
        _scopes = scopes;
    }

    /// <summary>
    /// The employees this user may see, for the analytics below.
    ///
    /// Every chart here is an aggregate over a population, so scoping the population scopes
    /// the chart. A department head's punctuality figures describe their department; without
    /// this they described the company and were indistinguishable from an administrator's.
    /// </summary>
    private async Task<List<Employee>> VisibleEmployeesAsync(bool activeOnly = true)
    {
        var scope = await _scopes.GetDataScopeAsync();
        return (await _uow.Employees.FindAsync(e => !e.IsDeleted && (!activeOnly || e.IsActive)))
            .Where(e => scope.Allows(e.Id, e.DepartmentId))
            .ToList();
    }

    // ── Trend ─────────────────────────────────────────────────────────────────

    public async Task<Result<AttendanceTrendDto>> GetAttendanceTrendAsync(int days = 7)
    {
        try
        {
            days = Math.Clamp(days, 1, MaxTrendDays);

            var today = DateTime.Today;
            var from = today.AddDays(-(days - 1));

            var employees = await VisibleEmployeesAsync();

            // Records are restricted to the visible population as well as the counts derived
            // from it. Scoping the employee list alone would still leave Present and Late
            // counting the whole company against a departmental headcount.
            var visibleIds = employees.Select(e => e.Id).ToHashSet();

            var logs = (await _uow.Attendance.FindAsync(
                a => a.AttendanceDate >= from && a.AttendanceDate <= today && !a.IsDeleted))
                .Where(a => visibleIds.Contains(a.EmployeeId)).ToList();

            var leaves = (await _uow.Leaves.FindAsync(
                l => l.Status == LeaveStatus.Approved && !l.IsDeleted
                     && l.FromDate.Date <= today && l.ToDate.Date >= from))
                .Where(l => visibleIds.Contains(l.EmployeeId)).ToList();

            var holidays = (await _uow.Holidays.FindAsync(
                h => h.HolidayDate >= from && h.HolidayDate <= today && !h.IsDeleted))
                .Select(h => h.HolidayDate.Date).ToHashSet();

            var offDayLookup = await BuildWeeklyOffLookupAsync(employees.Select(e => e.Id));

            var logsByDate = logs.GroupBy(l => l.AttendanceDate.Date)
                                 .ToDictionary(g => g.Key, g => g.ToList());

            var result = new AttendanceTrendDto { Days = days };

            for (var d = from; d <= today; d = d.AddDays(1))
            {
                var dayLogs = logsByDate.TryGetValue(d, out var found) ? found : new List<AttendanceLog>();

                var present = dayLogs.Count(l => l.Status == AttendanceStatus.Present);
                var late = dayLogs.Count(l => l.Status == AttendanceStatus.Late);
                var onLeave = leaves.Count(l => l.FromDate.Date <= d && l.ToDate.Date >= d);

                var isHoliday = holidays.Contains(d);

                // Employees whose own shift makes this a non-working day. Counting these as
                // absent would make every weekend look like a company-wide walkout.
                var offToday = isHoliday
                    ? employees.Count
                    : employees.Count(e => offDayLookup.TryGetValue(e.Id, out var offDays)
                                           && offDays.Contains(d.DayOfWeek));

                var expected = Math.Max(0, employees.Count - offToday - onLeave);
                var checkedIn = present + late;
                var absent = Math.Max(0, expected - checkedIn);

                // Someone working on their weekly off is counted in checkedIn but excluded from
                // expected, which would push the ratio above 100% (a Saturday with 1 scheduled
                // employee and 4 who turned up read as 400%). Widen the denominator to include
                // anyone who actually worked, so the figure stays a meaningful 0–100%.
                var denominator = Math.Max(expected, checkedIn);

                result.Points.Add(new AttendanceTrendPointDto
                {
                    Date = d,
                    Label = d.ToString("ddd dd"),
                    Present = present,
                    Late = late,
                    Absent = absent,
                    OnLeave = onLeave,
                    NonWorking = offToday,
                    IsHoliday = isHoliday,
                    AttendancePercentage = denominator > 0
                        ? Math.Round((double)checkedIn / denominator * 100, 1)
                        : 0
                });
            }

            // Average over days that actually saw someone check in. Days before the system was
            // in use look like 100% absence, and including them would drag the average toward
            // zero and describe the rollout rather than the workforce.
            var daysWithCheckIns = result.Points.Where(p => p.Present + p.Late > 0).ToList();
            result.DaysWithData = daysWithCheckIns.Count;
            result.AverageAttendancePercentage = daysWithCheckIns.Count > 0
                ? Math.Round(daysWithCheckIns.Average(p => p.AttendancePercentage), 1)
                : 0;

            return Result<AttendanceTrendDto>.Success(result);
        }
        catch (Exception ex)
        {
            AppLogger.Error("AnalyticsService.GetAttendanceTrendAsync", ex);
            return Result<AttendanceTrendDto>.Failure("Failed to build the attendance trend.");
        }
    }

    // ── Punctuality ───────────────────────────────────────────────────────────

    public async Task<Result<PunctualityDto>> GetPunctualityAsync(DateTime from, DateTime to, int topCount = 10)
    {
        try
        {
            from = from.Date;
            to = to.Date;
            if (to < from) (from, to) = (to, from);

            var employees = await VisibleEmployeesAsync(activeOnly: false);
            var visibleIds = employees.Select(e => e.Id).ToHashSet();

            var logs = (await _uow.Attendance.FindAsync(
                a => a.AttendanceDate >= from && a.AttendanceDate <= to && !a.IsDeleted
                     && (a.Status == AttendanceStatus.Present || a.Status == AttendanceStatus.Late)))
                .Where(a => visibleIds.Contains(a.EmployeeId)).ToList();

            var departments = (await _uow.Departments.GetAllAsync()).ToDictionary(d => d.Id, d => d.Name);

            var empById = employees.ToDictionary(e => e.Id);
            string DeptOf(int employeeId) =>
                empById.TryGetValue(employeeId, out var e) && departments.TryGetValue(e.DepartmentId, out var n)
                    ? n : string.Empty;

            var lateLogs = logs.Where(l => l.IsLate).ToList();

            var dto = new PunctualityDto
            {
                FromDate = from,
                ToDate = to,
                TotalCheckIns = logs.Count,
                TotalLate = lateLogs.Count,
                TotalEarlyLeave = logs.Count(l => l.IsEarlyLeave),
                LatePercentage = logs.Count > 0
                    ? Math.Round((double)lateLogs.Count / logs.Count * 100, 1) : 0,
                AverageLateMinutes = lateLogs.Count > 0
                    ? Math.Round(lateLogs.Average(l => l.LateMinutes ?? 0), 1) : 0
            };

            dto.TopLate = lateLogs
                .GroupBy(l => l.EmployeeId)
                .Select(g =>
                {
                    empById.TryGetValue(g.Key, out var e);
                    var totalMinutes = g.Sum(l => l.LateMinutes ?? 0);
                    return new LateEmployeeDto
                    {
                        EmployeeId = g.Key,
                        EmployeeCode = e?.EmployeeCode ?? string.Empty,
                        EmployeeName = e != null ? $"{e.FirstName} {e.LastName}" : $"#{g.Key}",
                        Department = DeptOf(g.Key),
                        LateCount = g.Count(),
                        TotalLateMinutes = totalMinutes,
                        AverageLateMinutes = Math.Round((double)totalMinutes / g.Count(), 1)
                    };
                })
                .OrderByDescending(x => x.LateCount).ThenByDescending(x => x.TotalLateMinutes)
                .Take(Math.Clamp(topCount, 1, 50))
                .ToList();

            // Every weekday appears, including those with no data, so the chart keeps a stable
            // shape instead of silently dropping quiet days.
            var weekOrder = new[]
            {
                DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
                DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
            };
            dto.ByWeekday = weekOrder.Select(day =>
            {
                var dayLogs = logs.Where(l => l.AttendanceDate.DayOfWeek == day).ToList();
                var lateCount = dayLogs.Count(l => l.IsLate);
                return new WeekdayPunctualityDto
                {
                    Day = day.ToString(),
                    CheckIns = dayLogs.Count,
                    LateCount = lateCount,
                    LatePercentage = dayLogs.Count > 0
                        ? Math.Round((double)lateCount / dayLogs.Count * 100, 1) : 0
                };
            }).ToList();

            dto.ByDepartment = logs
                .GroupBy(l => DeptOf(l.EmployeeId))
                .Where(g => !string.IsNullOrEmpty(g.Key))
                .Select(g =>
                {
                    var late = g.Where(l => l.IsLate).ToList();
                    return new DepartmentPunctualityDto
                    {
                        Department = g.Key,
                        CheckIns = g.Count(),
                        LateCount = late.Count,
                        LatePercentage = Math.Round((double)late.Count / g.Count() * 100, 1),
                        AverageLateMinutes = late.Count > 0
                            ? Math.Round(late.Average(l => l.LateMinutes ?? 0), 1) : 0
                    };
                })
                .OrderByDescending(x => x.LatePercentage)
                .ToList();

            return Result<PunctualityDto>.Success(dto);
        }
        catch (Exception ex)
        {
            AppLogger.Error("AnalyticsService.GetPunctualityAsync", ex);
            return Result<PunctualityDto>.Failure("Failed to build the punctuality analysis.");
        }
    }

    // ── Leave overview ────────────────────────────────────────────────────────

    public async Task<Result<LeaveOverviewDto>> GetLeaveOverviewAsync()
    {
        try
        {
            var today = DateTime.Today;
            var monthStart = new DateTime(today.Year, today.Month, 1);
            var horizon = today.AddDays(30);

            var employees = await VisibleEmployeesAsync();
            var departments = (await _uow.Departments.GetAllAsync()).ToDictionary(d => d.Id, d => d.Name);
            var leaveTypes = (await _uow.LeaveTypes.GetAllAsync()).Where(t => t.IsActive).ToList();

            var visibleIds = employees.Select(e => e.Id).ToHashSet();
            var allRequests = (await _uow.Leaves.FindAsync(l => !l.IsDeleted))
                .Where(l => visibleIds.Contains(l.EmployeeId)).ToList();

            var empById = employees.ToDictionary(e => e.Id);
            string NameOf(int id) => empById.TryGetValue(id, out var e) ? $"{e.FirstName} {e.LastName}" : $"#{id}";
            string DeptOf(int id) =>
                empById.TryGetValue(id, out var e) && departments.TryGetValue(e.DepartmentId, out var n) ? n : string.Empty;
            string TypeOf(int id) => leaveTypes.FirstOrDefault(t => t.Id == id)?.Name ?? string.Empty;

            var pending = allRequests.Where(l => l.Status == LeaveStatus.Pending).ToList();
            var approved = allRequests.Where(l => l.Status == LeaveStatus.Approved).ToList();

            var dto = new LeaveOverviewDto
            {
                PendingCount = pending.Count,
                OnLeaveToday = approved.Count(l => l.FromDate.Date <= today && l.ToDate.Date >= today),
                ApprovedThisMonth = approved.Count(l => l.FromDate.Date >= monthStart && l.FromDate.Date <= today),
                OldestPendingDays = pending.Count > 0
                    ? (int)(today - pending.Min(l => l.CreatedAt).Date).TotalDays : 0
            };

            dto.PendingRequests = pending
                .OrderBy(l => l.CreatedAt)
                .Take(10)
                .Select(l => new LeaveRequestDto
                {
                    Id = l.Id,
                    EmployeeId = l.EmployeeId,
                    EmployeeName = NameOf(l.EmployeeId),
                    LeaveTypeId = l.LeaveTypeId,
                    LeaveTypeName = TypeOf(l.LeaveTypeId),
                    FromDate = l.FromDate,
                    ToDate = l.ToDate,
                    TotalDays = l.TotalDays,
                    Reason = l.Reason,
                    Status = l.Status
                })
                .ToList();

            // Entitlement is per employee per year, so company capacity is allowance × headcount.
            var year = today.Year;
            dto.Utilisation = leaveTypes.Select(t =>
            {
                var taken = approved
                    .Where(l => l.LeaveTypeId == t.Id && l.FromDate.Year == year)
                    .Sum(l => l.TotalDays);
                var capacity = t.TotalDays * employees.Count;
                return new LeaveTypeUtilisationDto
                {
                    LeaveType = t.Name,
                    AllowancePerEmployee = t.TotalDays,
                    TotalEntitlement = capacity,
                    DaysTaken = taken,
                    UtilisationPercentage = capacity > 0
                        ? Math.Round((double)taken / capacity * 100, 1) : 0
                };
            }).OrderByDescending(u => u.UtilisationPercentage).ToList();

            dto.Upcoming = approved
                .Where(l => l.FromDate.Date > today && l.FromDate.Date <= horizon)
                .OrderBy(l => l.FromDate)
                .Take(10)
                .Select(l => new UpcomingLeaveDto
                {
                    LeaveRequestId = l.Id,
                    EmployeeName = NameOf(l.EmployeeId),
                    Department = DeptOf(l.EmployeeId),
                    LeaveType = TypeOf(l.LeaveTypeId),
                    FromDate = l.FromDate,
                    ToDate = l.ToDate,
                    TotalDays = l.TotalDays,
                    StartsInDays = (int)(l.FromDate.Date - today).TotalDays
                })
                .ToList();

            return Result<LeaveOverviewDto>.Success(dto);
        }
        catch (Exception ex)
        {
            AppLogger.Error("AnalyticsService.GetLeaveOverviewAsync", ex);
            return Result<LeaveOverviewDto>.Failure("Failed to build the leave overview.");
        }
    }

    // ── Operations health ─────────────────────────────────────────────────────

    public async Task<Result<OperationsHealthDto>> GetOperationsHealthAsync(DateTime from, DateTime to)
    {
        try
        {
            from = from.Date;
            to = to.Date;
            if (to < from) (from, to) = (to, from);

            var today = DateTime.Today;

            var employees = await VisibleEmployeesAsync();
            var departments = (await _uow.Departments.GetAllAsync()).ToDictionary(d => d.Id, d => d.Name);

            var visibleIds = employees.Select(e => e.Id).ToHashSet();
            var logs = (await _uow.Attendance.FindAsync(
                a => a.AttendanceDate >= from && a.AttendanceDate <= to && !a.IsDeleted))
                .Where(a => visibleIds.Contains(a.EmployeeId)).ToList();

            var empById = employees.ToDictionary(e => e.Id);
            string DeptOf(int deptId) => departments.TryGetValue(deptId, out var n) ? n : string.Empty;

            var dto = new OperationsHealthDto { FromDate = from, ToDate = to };

            // 1. No biometric enroll id — device punches can never be matched to these people,
            //    so they silently appear absent after every import.
            var noBiometric = employees.Where(e => !e.BiometricEnrollId.HasValue).ToList();
            dto.MissingBiometricId = noBiometric.Count;
            dto.MissingBiometricEmployees = noBiometric.Take(20).Select(e => new OperationsIssueEmployeeDto
            {
                EmployeeId = e.Id,
                EmployeeCode = e.EmployeeCode,
                EmployeeName = $"{e.FirstName} {e.LastName}",
                Department = DeptOf(e.DepartmentId)
            }).ToList();

            // 2. Checked in but never out. Today is excluded — those are still at work.
            var openRecords = logs
                .Where(l => l.CheckIn.HasValue && !l.CheckOut.HasValue && l.AttendanceDate.Date < today)
                .OrderByDescending(l => l.AttendanceDate)
                .ToList();
            dto.MissingCheckOut = openRecords.Count;
            dto.MissingCheckOutRecords = openRecords.Take(20).Select(l =>
            {
                empById.TryGetValue(l.EmployeeId, out var e);
                return new OperationsIssueEmployeeDto
                {
                    EmployeeId = l.EmployeeId,
                    EmployeeCode = e?.EmployeeCode ?? string.Empty,
                    EmployeeName = e != null ? $"{e.FirstName} {e.LastName}" : $"#{l.EmployeeId}",
                    Department = e != null ? DeptOf(e.DepartmentId) : string.Empty,
                    Detail = l.AttendanceDate.ToString("dd MMM yyyy")
                };
            }).ToList();

            // 3. No shift covering today — these employees are never flagged late or early,
            //    so their punctuality data is quietly meaningless.
            var offLookup = await BuildWeeklyOffLookupAsync(employees.Select(e => e.Id), includeUnassignedOnly: true);
            var noShift = employees.Where(e => !offLookup.ContainsKey(e.Id)).ToList();
            dto.WithoutShift = noShift.Count;
            dto.WithoutShiftEmployees = noShift.Take(20).Select(e => new OperationsIssueEmployeeDto
            {
                EmployeeId = e.Id,
                EmployeeCode = e.EmployeeCode,
                EmployeeName = $"{e.FirstName} {e.LastName}",
                Department = DeptOf(e.DepartmentId)
            }).ToList();

            // 4. Manual vs device-sourced records.
            dto.ManualRecords = logs.Count(l => l.IsManual);
            dto.DeviceRecords = logs.Count(l => !l.IsManual);
            dto.ManualPercentage = logs.Count > 0
                ? Math.Round((double)dto.ManualRecords / logs.Count * 100, 1) : 0;
            dto.LastDeviceRecordAt = logs.Where(l => !l.IsManual)
                                         .Select(l => (DateTime?)l.CreatedAt)
                                         .DefaultIfEmpty(null)
                                         .Max();

            return Result<OperationsHealthDto>.Success(dto);
        }
        catch (Exception ex)
        {
            AppLogger.Error("AnalyticsService.GetOperationsHealthAsync", ex);
            return Result<OperationsHealthDto>.Failure("Failed to build the operations overview.");
        }
    }

    // ── Shared ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Maps employee id → the weekdays their current shift treats as off.
    /// Employees with no effective shift assignment are absent from the dictionary, which is
    /// also how callers detect them.
    /// </summary>
    private async Task<Dictionary<int, HashSet<DayOfWeek>>> BuildWeeklyOffLookupAsync(
        IEnumerable<int> employeeIds, bool includeUnassignedOnly = false)
    {
        var today = DateTime.Today;
        var ids = employeeIds.ToHashSet();

        var assignments = (await _uow.EmployeeShifts.FindAsync(
            es => !es.IsDeleted && es.EffectiveFrom <= today
                  && (es.EffectiveTo == null || es.EffectiveTo >= today))).ToList();

        var shifts = (await _uow.Shifts.GetAllAsync()).ToDictionary(s => s.Id);

        var lookup = new Dictionary<int, HashSet<DayOfWeek>>();

        foreach (var group in assignments.Where(a => ids.Contains(a.EmployeeId)).GroupBy(a => a.EmployeeId))
        {
            // Latest effective assignment wins, matching AttendanceService.CheckInAsync.
            var current = group.OrderByDescending(a => a.EffectiveFrom).First();
            if (!shifts.TryGetValue(current.ShiftId, out var shift)) continue;

            var offDays = shift.WeeklyOffDays
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(d => d.Trim())
                .Where(d => Enum.TryParse<DayOfWeek>(d, ignoreCase: true, out _))
                .Select(d => Enum.Parse<DayOfWeek>(d, ignoreCase: true))
                .ToHashSet();

            lookup[current.EmployeeId] = offDays;
        }

        return lookup;
    }
}
