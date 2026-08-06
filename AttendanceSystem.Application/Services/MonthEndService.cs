using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Common.Constants;
using AttendanceSystem.Common.Logging;
using AttendanceSystem.Common.Models;
using AttendanceSystem.Common.Session;
using AttendanceSystem.Domain.Enums;
using AttendanceSystem.Domain.Interfaces;

namespace AttendanceSystem.Application.Services;

/// <summary>
/// Closing a month and handing it to payroll.
///
/// These two belong together. Attendance and overtime were exported separately and joined by
/// hand in a spreadsheet, and nothing connected either export to closing the period — so a
/// month could be paid while overtime was still undecided, or edited after the figures had
/// gone out, with no screen able to say which had happened.
///
/// The order is the point: check, close, then export. Closing is what makes the numbers
/// trustworthy, so the export reports whether it happened rather than leaving that to memory.
/// </summary>
public class MonthEndService : IMonthEndService
{
    private readonly IUnitOfWork _uow;
    private readonly IAttendanceService _attendance;
    private readonly IOvertimeService _overtime;
    private readonly IAttendanceLockService _locks;
    private readonly IApprovalScopeService _scopes;
    private readonly IAuditService _audit;
    private readonly ICurrentUserContext _currentUser;

    public MonthEndService(
        IUnitOfWork uow, IAttendanceService attendance, IOvertimeService overtime,
        IAttendanceLockService locks, IApprovalScopeService scopes,
        IAuditService audit, ICurrentUserContext currentUser)
    {
        _uow = uow;
        _attendance = attendance;
        _overtime = overtime;
        _locks = locks;
        _scopes = scopes;
        _audit = audit;
        _currentUser = currentUser;
    }

    private static (DateTime From, DateTime To) RangeOf(int month, int year)
    {
        var from = new DateTime(year, month, 1);
        return (from, from.AddMonths(1).AddDays(-1));
    }

    // ── Readiness ─────────────────────────────────────────────────────────────

    public async Task<Result<MonthEndStatusDto>> GetStatusAsync(int month, int year)
    {
        try
        {
            if (month is < 1 or > 12) return Result<MonthEndStatusDto>.Failure("Month must be between 1 and 12.");

            var (from, to) = RangeOf(month, year);
            var scope = await _scopes.GetDataScopeAsync();

            var employees = (await _uow.Employees.FindAsync(e => e.IsActive && !e.IsDeleted))
                .Where(e => scope.Allows(e.Id, e.DepartmentId))
                .ToList();
            var visibleIds = employees.Select(e => e.Id).ToHashSet();

            var dto = new MonthEndStatusDto
            {
                Month = month, Year = year, FromDate = from, ToDate = to,
                EmployeeCount = employees.Count
            };

            // Closed means the whole month is covered by a lock. Checking the first and last
            // day rather than one of them: a lock over part of the month leaves the rest open,
            // and reporting that as closed is how a half-closed month gets paid.
            var startLock = await _locks.GetLockForAsync(from);
            var endLock = await _locks.GetLockForAsync(to);
            var closed = startLock != null && endLock != null;

            dto.IsClosed = closed;
            if (closed)
            {
                dto.ClosedReason = startLock!.Reason;
                dto.ClosedAt = startLock.CreatedAt;
                dto.ClosedBy = startLock.LockedByName;
            }

            // ── Blocking ──────────────────────────────────────────────────────

            var pendingOt = (await _uow.OvertimeRecords.FindAsync(
                    o => !o.IsDeleted && o.Status == OvertimeStatus.Pending
                      && o.OvertimeDate >= from && o.OvertimeDate <= to))
                .Count(o => visibleIds.Contains(o.EmployeeId));

            dto.Checks.Add(new MonthEndCheckDto
            {
                Key = "pendingot",
                Title = "Overtime decided",
                Count = pendingOt,
                IsBlocking = true,
                Detail = pendingOt == 0
                    ? "No overtime is awaiting a decision."
                    : $"{pendingOt} overtime claim(s) still awaiting approval. Only approved overtime is exported, "
                      + "so closing now would pay none of these.",
                ActionUrl = "/Admin/OvertimeApproval",
                ActionLabel = "Approve overtime"
            });

            var pendingLeave = (await _uow.Leaves.FindAsync(
                    l => !l.IsDeleted && l.Status == LeaveStatus.Pending
                      && l.FromDate <= to && l.ToDate >= from))
                .Count(l => visibleIds.Contains(l.EmployeeId));

            dto.Checks.Add(new MonthEndCheckDto
            {
                Key = "pendingleave",
                Title = "Leave decided",
                Count = pendingLeave,
                IsBlocking = true,
                Detail = pendingLeave == 0
                    ? "No leave is awaiting a decision."
                    : $"{pendingLeave} leave request(s) overlapping this month are still pending. "
                      + "An undecided request counts as absent, which is probably not what you want to pay.",
                ActionUrl = "/Admin/Leave",
                ActionLabel = "Decide leave"
            });

            // ── Advisory ──────────────────────────────────────────────────────

            // A day with no check-out has no working hours and no overtime, so it is paid as
            // though the person never left. Advisory rather than blocking: on a large site
            // there are always a few, and a month cannot be held hostage to them.
            var openRecords = (await _uow.Attendance.FindAsync(
                    a => !a.IsDeleted && a.AttendanceDate >= from && a.AttendanceDate <= to
                      && a.CheckIn != null && a.CheckOut == null))
                .Count(a => visibleIds.Contains(a.EmployeeId));

            dto.Checks.Add(new MonthEndCheckDto
            {
                Key = "nocheckout",
                Title = "Check-outs complete",
                Count = openRecords,
                IsBlocking = false,
                Detail = openRecords == 0
                    ? "Every attended day has a check-out."
                    : $"{openRecords} day(s) have a check-in but no check-out, so they contribute "
                      + "no working hours and no overtime.",
                ActionUrl = $"/Admin/AttendanceReview?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}&filter=nocheckout",
                ActionLabel = "Review these days"
            });

            // Without a shift the calculator cannot judge lateness, overtime or which days are
            // working days, so these people's figures are guesses.
            var assigned = (await _uow.EmployeeShifts.FindAsync(
                    es => !es.IsDeleted && es.EffectiveFrom <= to
                       && (es.EffectiveTo == null || es.EffectiveTo >= from)))
                .Select(es => es.EmployeeId)
                .ToHashSet();

            var noShift = employees.Count(e => !assigned.Contains(e.Id));

            dto.Checks.Add(new MonthEndCheckDto
            {
                Key = "noshift",
                Title = "Shifts assigned",
                Count = noShift,
                IsBlocking = false,
                Detail = noShift == 0
                    ? "Every employee has a shift for this month."
                    : $"{noShift} employee(s) have no shift covering this month, so lateness, "
                      + "overtime and working days cannot be derived for them.",
                ActionUrl = "/Admin/ShiftRoster",
                ActionLabel = "Assign shifts"
            });

            return Result<MonthEndStatusDto>.Success(dto);
        }
        catch (Exception ex)
        {
            AppLogger.Error("MonthEndService.GetStatusAsync", ex);
            return Result<MonthEndStatusDto>.Failure("Could not check the month.");
        }
    }

    // ── Closing ───────────────────────────────────────────────────────────────

    public async Task<Result> CloseMonthAsync(CloseMonthDto dto)
    {
        try
        {
            var status = await GetStatusAsync(dto.Month, dto.Year);
            if (!status.IsSuccess) return Result.Failure(status.ErrorMessage!);

            var s = status.Data!;

            if (s.IsClosed)
                return Result.Failure($"{s.PeriodDisplay} is already closed.");

            // Never overridable. A month with undecided overtime or leave cannot be paid
            // correctly, and an override flag would turn the check into a formality.
            if (s.Blockers.Count > 0)
                return Result.Failure(
                    "This month cannot be closed yet: "
                    + string.Join(" ", s.Blockers.Select(b => b.Detail)));

            if (s.Warnings.Count > 0 && !dto.AcknowledgeWarnings)
                return Result.Failure(
                    "There are warnings to acknowledge first: "
                    + string.Join(" ", s.Warnings.Select(w => w.Detail)));

            // Closing *is* locking — there is no second concept to keep in step with the
            // first. Everything that already refuses to write into a locked period therefore
            // refuses to write into a closed month, with no further wiring.
            var lockResult = await _locks.LockPeriodAsync(new LockPeriodDto
            {
                FromDate = s.FromDate,
                ToDate = s.ToDate,
                BranchId = null,
                Reason = dto.Reason.Trim()
            });

            if (!lockResult.IsSuccess) return lockResult;

            await _audit.LogAsync(AppConstants.Modules.Attendance, "MonthEndClose",
                _currentUser.UserId, "AttendancePeriodLock", null,
                newValues: $"{s.PeriodDisplay}: {dto.Reason.Trim()}");

            return Result.Success();
        }
        catch (Exception ex)
        {
            AppLogger.Error("MonthEndService.CloseMonthAsync", ex);
            return Result.Failure("Could not close the month.");
        }
    }

    // ── Payroll export ────────────────────────────────────────────────────────

    public async Task<Result<PayrollExportDto>> GetPayrollAsync(int month, int year)
    {
        try
        {
            if (month is < 1 or > 12) return Result<PayrollExportDto>.Failure("Month must be between 1 and 12.");

            var (from, to) = RangeOf(month, year);

            // Both come from the services that own them, rather than being recomputed here.
            // A second implementation of "days present" or "approved overtime" would be a
            // second answer, and payroll is the worst place to discover there are two.
            var summary = await _attendance.GetMonthlySummaryAsync(month, year);
            if (!summary.IsSuccess) return Result<PayrollExportDto>.Failure(summary.ErrorMessage!);

            var ot = await _overtime.GetSummaryAsync(from, to, null, null);
            if (!ot.IsSuccess) return Result<PayrollExportDto>.Failure(ot.ErrorMessage!);

            var otByEmployee = ot.Data!.Rows.ToDictionary(r => r.EmployeeId);

            var employees = (await _uow.Employees.GetAllAsync()).ToDictionary(e => e.Id);
            var designations = (await _uow.Designations.GetAllAsync()).ToDictionary(d => d.Id, d => d.Name);

            var status = await GetStatusAsync(month, year);

            var rows = new List<PayrollRowDto>();
            foreach (var a in summary.Data!)
            {
                otByEmployee.TryGetValue(a.EmployeeId, out var o);
                employees.TryGetValue(a.EmployeeId, out var emp);

                rows.Add(new PayrollRowDto
                {
                    EmployeeId = a.EmployeeId,
                    EmployeeCode = a.EmployeeCode,
                    EmployeeName = a.EmployeeName,
                    Department = a.Department,
                    Designation = emp != null && designations.TryGetValue(emp.DesignationId, out var dn)
                                    ? dn : string.Empty,

                    TotalDays = a.TotalDays,
                    // Days the person was expected to work — the divisor payroll usually wants.
                    WorkingDays = Math.Max(0, a.TotalDays - a.HolidayDays),
                    PresentDays = a.PresentDays,
                    AbsentDays = a.AbsentDays,
                    LeaveDays = a.LeaveDays,
                    HolidayDays = a.HolidayDays,
                    LateDays = a.LateDays,
                    LateMinutes = 0,
                    WorkingHours = Math.Round(a.TotalWorkingHours, 2),

                    // Approved only. Pending claims are excluded on purpose, and the month
                    // cannot be closed while any remain, so a closed month has none hidden.
                    ApprovedOtHours = o != null ? Math.Round(o.ApprovedMinutes / 60.0, 2) : 0,
                    PremiumOtHours = o != null ? Math.Round(o.PremiumMinutes / 60.0, 2) : 0,

                    AttendancePercentage = a.AttendancePercentage
                });
            }

            return Result<PayrollExportDto>.Success(new PayrollExportDto
            {
                Month = month,
                Year = year,
                PeriodDisplay = from.ToString("MMMM yyyy"),
                IsClosed = status.IsSuccess && status.Data!.IsClosed,
                GeneratedAt = DateTime.Now,
                Rows = rows.OrderBy(r => r.Department).ThenBy(r => r.EmployeeCode).ToList()
            });
        }
        catch (Exception ex)
        {
            AppLogger.Error("MonthEndService.GetPayrollAsync", ex);
            return Result<PayrollExportDto>.Failure("Could not build the payroll data.");
        }
    }
}
