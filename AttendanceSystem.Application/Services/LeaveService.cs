using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Common.Logging;
using AttendanceSystem.Common.Models;
using AttendanceSystem.Common.Session;
using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Domain.Enums;
using AttendanceSystem.Domain.Interfaces;

namespace AttendanceSystem.Application.Services;

/// <summary>Leave type CRUD + leave application, approval and balance tracking.</summary>
public class LeaveService : ILeaveService
{
    private readonly IUnitOfWork _uow;
    private readonly IAuditService _audit;
    private readonly ICurrentUserContext _currentUser;
    private readonly IApprovalScopeService _scopes;

    public LeaveService(IUnitOfWork uow, IAuditService audit, ICurrentUserContext currentUser,
                        IApprovalScopeService scopes)
    { _uow = uow; _audit = audit; _currentUser = currentUser; _scopes = scopes; }

    // ── Leave Types ────────────────────────────────────────────────────────────
    public async Task<Result<IEnumerable<LeaveTypeDto>>> GetLeaveTypesAsync()
    {
        try
        {
            var list = await _uow.LeaveTypes.GetAllAsync();
            return Result<IEnumerable<LeaveTypeDto>>.Success(
                list.OrderBy(l => l.Name).Select(MapType));
        }
        catch (Exception ex) { return Result<IEnumerable<LeaveTypeDto>>.Failure(ex.Message); }
    }

    public async Task<Result<LeaveTypeDto>> SaveLeaveTypeAsync(SaveLeaveTypeDto dto)
    {
        try
        {
            var all = await _uow.LeaveTypes.GetAllAsync();
            if (all.Any(l => l.Name.Equals(dto.Name.Trim(), StringComparison.OrdinalIgnoreCase) && l.Id != dto.Id))
                return Result<LeaveTypeDto>.Failure($"Leave type '{dto.Name}' already exists.");

            if (dto.Id == 0)
            {
                var entity = new LeaveType { Name = dto.Name.Trim(), TotalDays = dto.TotalDays, IsPaid = dto.IsPaid, IsActive = dto.IsActive, CreatedBy = _currentUser.UserId, CreatedAt = DateTime.Now };
                await _uow.LeaveTypes.AddAsync(entity);
                await _uow.SaveChangesAsync();
                return Result<LeaveTypeDto>.Success(MapType(entity));
            }
            else
            {
                var entity = await _uow.LeaveTypes.GetByIdAsync(dto.Id);
                if (entity == null) return Result<LeaveTypeDto>.Failure("Leave type not found.");
                entity.Name = dto.Name.Trim(); entity.TotalDays = dto.TotalDays;
                entity.IsPaid = dto.IsPaid; entity.IsActive = dto.IsActive;
                entity.ModifiedBy = _currentUser.UserId; entity.ModifiedAt = DateTime.Now;
                await _uow.LeaveTypes.UpdateAsync(entity);
                await _uow.SaveChangesAsync();
                return Result<LeaveTypeDto>.Success(MapType(entity));
            }
        }
        catch (Exception ex) { return Result<LeaveTypeDto>.Failure(ex.Message); }
    }

    public async Task<Result> DeleteLeaveTypeAsync(int id)
    {
        try
        {
            var requests = await _uow.Leaves.FindAsync(r => r.LeaveTypeId == id);
            if (requests.Any()) return Result.Failure("Cannot delete — leave requests reference this type.");
            var entity = await _uow.LeaveTypes.GetByIdAsync(id);
            if (entity == null) return Result.Failure("Not found.");
            entity.IsDeleted = true; entity.ModifiedBy = _currentUser.UserId; entity.ModifiedAt = DateTime.Now;
            await _uow.LeaveTypes.UpdateAsync(entity);
            await _uow.SaveChangesAsync();
            return Result.Success();
        }
        catch (Exception ex) { return Result.Failure(ex.Message); }
    }

    // ── Leave Requests ─────────────────────────────────────────────────────────
    public async Task<Result<IEnumerable<LeaveRequestDto>>> GetAllRequestsAsync()
    {
        try
        {
            var list = await _uow.Leaves.GetAllAsync();
            return Result<IEnumerable<LeaveRequestDto>>.Success(list.Select(MapRequest));
        }
        catch (Exception ex) { return Result<IEnumerable<LeaveRequestDto>>.Failure(ex.Message); }
    }

    public async Task<Result<PagedResult<LeaveRequestDto>>> GetRequestsPagedAsync(
        string? search, LeaveStatus? status, int? departmentId, int? employeeId,
        DateTime? from, DateTime? to, PageRequest page)
    {
        try
        {
            var scope = await _scopes.GetDataScopeAsync();

            var (items, total) = await _uow.Leaves.GetPagedAsync(
                search, status, departmentId, employeeId, from, to, page.Skip, page.PageSize,
                scope.DepartmentFilter, scope.EmployeeFilter);

            return Result<PagedResult<LeaveRequestDto>>.Success(new PagedResult<LeaveRequestDto>
            {
                Items = items.Select(MapRequest).ToList(),
                Page = page.Page,
                PageSize = page.PageSize,
                TotalCount = total
            });
        }
        catch (Exception ex)
        {
            AppLogger.Error("LeaveService.GetRequestsPagedAsync", ex);
            return Result<PagedResult<LeaveRequestDto>>.Failure("Failed to load leave requests.");
        }
    }

    public async Task<Result<IEnumerable<LeaveRequestDto>>> GetByEmployeeAsync(int employeeId)
    {
        try
        {
            var list = await _uow.Leaves.GetByEmployeeAsync(employeeId);
            return Result<IEnumerable<LeaveRequestDto>>.Success(list.Select(MapRequest));
        }
        catch (Exception ex) { return Result<IEnumerable<LeaveRequestDto>>.Failure(ex.Message); }
    }

    public async Task<Result<IEnumerable<LeaveRequestDto>>> GetPendingAsync()
    {
        try
        {
            var list = (await _uow.Leaves.GetPendingAsync()).ToList();

            // Narrowed to what this approver can actually act on. Listing requests they will
            // be refused on would be a queue of work they cannot do — and their own request
            // sitting in their approval list invites exactly the click the policy forbids.
            var scope = await BuildApprovalScopeAsync(_currentUser.UserId ?? 0);
            list = list.Where(l =>
                l.Employee != null &&
                scope.CanApprove(l.EmployeeId, l.Employee.DepartmentId, out _)).ToList();

            return Result<IEnumerable<LeaveRequestDto>>.Success(list.Select(MapRequest));
        }
        catch (Exception ex) { return Result<IEnumerable<LeaveRequestDto>>.Failure(ex.Message); }
    }

    private Task<LeaveApprovalScope> BuildApprovalScopeAsync(int userId) => _scopes.GetForAsync(userId);

    public async Task<Result<LeaveRequestDto>> ApplyLeaveAsync(ApplyLeaveDto dto)
    {
        try
        {
            if (dto.ToDate < dto.FromDate)
                return Result<LeaveRequestDto>.Failure("To date must be after from date.");

            var leaveType = await _uow.LeaveTypes.GetByIdAsync(dto.LeaveTypeId);
            if (leaveType == null) return Result<LeaveRequestDto>.Failure("Leave type not found.");

            var totalDays = await CountWorkingDaysAsync(dto.EmployeeId, dto.FromDate, dto.ToDate);

            // A range made entirely of days off costs nothing and is almost certainly a
            // mistake, so it is refused rather than stored as a zero-day request.
            if (totalDays == 0)
                return Result<LeaveRequestDto>.Failure(
                    "Those dates are all weekly off days or holidays, so no leave would be used. " +
                    "Check the dates.");

            // Booking the same days twice was possible: nothing compared a new request against
            // the ones already holding those dates, so an employee could apply for the same
            // week repeatedly and have each approved separately.
            var clashes = await _uow.Leaves.GetOverlappingAsync(dto.EmployeeId, dto.FromDate, dto.ToDate);
            var clash = clashes.FirstOrDefault();
            if (clash != null)
                return Result<LeaveRequestDto>.Failure(
                    $"These dates overlap an existing {clash.Status.ToString().ToLowerInvariant()} request " +
                    $"({clash.FromDate:dd-MMM-yyyy} – {clash.ToDate:dd-MMM-yyyy}" +
                    (clash.LeaveType != null ? $", {clash.LeaveType.Name}" : "") + ").");

            // Committed, not just approved: a pending request has to reserve its days, or two
            // undecided requests each pass the check and together exceed the entitlement.
            var committed = await _uow.Leaves.GetCommittedLeaveDaysAsync(
                dto.EmployeeId, dto.LeaveTypeId, dto.FromDate.Year);
            if (committed + totalDays > leaveType.TotalDays)
                return Result<LeaveRequestDto>.Failure(
                    $"Insufficient leave balance. Available: {Math.Max(0, leaveType.TotalDays - committed)} day(s) " +
                    "(including requests already awaiting approval).");

            var entity = new LeaveRequest
            {
                EmployeeId = dto.EmployeeId, LeaveTypeId = dto.LeaveTypeId,
                FromDate = dto.FromDate, ToDate = dto.ToDate, TotalDays = totalDays,
                Reason = dto.Reason, Status = LeaveStatus.Pending,
                CreatedBy = _currentUser.UserId, CreatedAt = DateTime.Now
            };
            await _uow.Leaves.AddAsync(entity);
            await _uow.SaveChangesAsync();
            await _audit.LogAsync("Leave", "Apply", _currentUser.UserId, "LeaveRequest", entity.Id);

            var result = await _uow.Leaves.GetByIdAsync(entity.Id);
            return Result<LeaveRequestDto>.Success(MapRequest(result!));
        }
        catch (Exception ex) { AppLogger.Error("LeaveService.ApplyLeaveAsync", ex); return Result<LeaveRequestDto>.Failure(ex.Message); }
    }

    public async Task<Result> ApproveRejectAsync(ApproveRejectLeaveDto dto, int actionBy)
    {
        try
        {
            var entity = await _uow.Leaves.GetByIdAsync(dto.LeaveRequestId);
            if (entity == null) return Result.Failure("Leave request not found.");
            if (entity.Status != LeaveStatus.Pending) return Result.Failure("Only pending requests can be approved or rejected.");

            // Leave.Approve says this person may approve; the scope says for whom. Checked
            // here rather than only in the UI — the endpoint is the authoritative gate, and a
            // hidden button is not a permission.
            var applicant = await _uow.Employees.GetByIdAsync(entity.EmployeeId);
            if (applicant == null) return Result.Failure("The employee on this request no longer exists.");

            var scope = await BuildApprovalScopeAsync(actionBy);
            if (!scope.CanApprove(entity.EmployeeId, applicant.DepartmentId, out var refusal))
                return Result.Failure(refusal!);

            var before = AuditSnapshot.Capture(entity);

            entity.Status = dto.IsApproved ? LeaveStatus.Approved : LeaveStatus.Rejected;
            entity.ApprovedBy = actionBy; entity.ApprovedAt = DateTime.Now;
            if (!dto.IsApproved) entity.RejectionReason = dto.RejectionReason;
            entity.ModifiedBy = actionBy; entity.ModifiedAt = DateTime.Now;
            await _uow.Leaves.UpdateAsync(entity);
            await _uow.SaveChangesAsync();

            var action = dto.IsApproved ? "Approve" : "Reject";
            var (oldValues, newValues) = AuditSnapshot.DiffAgainst(before, entity);
            await _audit.LogAsync("Leave", action, actionBy, "LeaveRequest", entity.Id,
                oldValues, newValues);
            return Result.Success();
        }
        catch (Exception ex) { return Result.Failure(ex.Message); }
    }

    public async Task<Result> CancelAsync(int leaveRequestId, int cancelledBy)
    {
        try
        {
            var entity = await _uow.Leaves.GetByIdAsync(leaveRequestId);
            if (entity == null) return Result.Failure("Leave request not found.");
            if (entity.Status == LeaveStatus.Approved && entity.FromDate.Date <= DateTime.Today)
                return Result.Failure("Cannot cancel approved leave that has already started.");
            entity.Status = LeaveStatus.Cancelled;
            entity.ModifiedBy = cancelledBy; entity.ModifiedAt = DateTime.Now;
            await _uow.Leaves.UpdateAsync(entity);
            await _uow.SaveChangesAsync();
            return Result.Success();
        }
        catch (Exception ex) { return Result.Failure(ex.Message); }
    }

    public async Task<Result<IEnumerable<LeaveBalanceDto>>> GetBalancesAsync(int employeeId)
    {
        try
        {
            var emp = await _uow.Employees.GetByIdAsync(employeeId);
            if (emp == null) return Result<IEnumerable<LeaveBalanceDto>>.Failure("Employee not found.");
            var leaveTypes = await _uow.LeaveTypes.GetAllAsync();
            var balances = new List<LeaveBalanceDto>();
            foreach (var lt in leaveTypes.Where(l => l.IsActive))
            {
                var year = DateTime.Now.Year;
                var used = await _uow.Leaves.GetUsedLeaveDaysAsync(employeeId, lt.Id, year);
                var committed = await _uow.Leaves.GetCommittedLeaveDaysAsync(employeeId, lt.Id, year);
                balances.Add(new LeaveBalanceDto
                {
                    EmployeeId = employeeId,
                    EmployeeName = $"{emp.FirstName} {emp.LastName}",
                    LeaveTypeName = lt.Name, TotalAllowed = lt.TotalDays,
                    UsedDays = used, PendingDays = committed - used
                });
            }
            return Result<IEnumerable<LeaveBalanceDto>>.Success(balances);
        }
        catch (Exception ex) { return Result<IEnumerable<LeaveBalanceDto>>.Failure(ex.Message); }
    }

    /// <summary>
    /// Days actually deducted for a leave range: calendar days, less the employee's weekly
    /// off days and any holidays.
    ///
    /// This used to be a plain inclusive day count, so applying Friday to Monday cost four
    /// days when only two were working ones — the system knew about weekly offs and holidays
    /// and consulted neither, quietly overcharging every request that spanned a weekend.
    ///
    /// Weekly-off membership is read through <see cref="AttendanceCalculator.IsWeeklyOff"/>
    /// rather than reimplemented, so leave and attendance cannot disagree about which days
    /// the shift runs. An employee with no shift assignment has no known off days, so only
    /// holidays are excluded for them.
    /// </summary>
    private async Task<int> CountWorkingDaysAsync(int employeeId, DateTime from, DateTime to)
    {
        var fromDate = from.Date;
        var toDate = to.Date;

        var holidays = await _uow.Holidays.GetHolidayDatesAsync(fromDate, toDate);

        // Every assignment overlapping the range, fetched once — a leave request can span a
        // shift change, and the off days differ either side of it.
        var assignments = (await _uow.EmployeeShifts.FindAsync(
                es => es.EmployeeId == employeeId && !es.IsDeleted
                   && es.EffectiveFrom <= toDate
                   && (es.EffectiveTo == null || es.EffectiveTo >= fromDate)))
            .OrderByDescending(es => es.EffectiveFrom)
            .ToList();

        var shiftCache = new Dictionary<int, Shift?>();
        var days = 0;

        for (var day = fromDate; day <= toDate; day = day.AddDays(1))
        {
            if (holidays.Contains(day)) continue;

            var assignment = assignments.FirstOrDefault(
                es => es.EffectiveFrom.Date <= day && (es.EffectiveTo == null || es.EffectiveTo.Value.Date >= day));

            if (assignment != null)
            {
                if (!shiftCache.TryGetValue(assignment.ShiftId, out var shift))
                {
                    shift = await _uow.Shifts.GetByIdAsync(assignment.ShiftId);
                    shiftCache[assignment.ShiftId] = shift;
                }
                if (shift != null && AttendanceCalculator.IsWeeklyOff(shift, day)) continue;
            }

            days++;
        }

        return days;
    }

    private static LeaveTypeDto MapType(LeaveType l) =>
        new() { Id = l.Id, Name = l.Name, TotalDays = l.TotalDays, IsPaid = l.IsPaid, IsActive = l.IsActive };

    private static LeaveRequestDto MapRequest(LeaveRequest r) => new()
    {
        Id = r.Id, EmployeeId = r.EmployeeId,
        EmployeeName = r.Employee != null ? $"{r.Employee.FirstName} {r.Employee.LastName}" : string.Empty,
        EmployeeCode = r.Employee?.EmployeeCode ?? string.Empty,
        Department = r.Employee?.Department?.Name ?? string.Empty,
        LeaveTypeId = r.LeaveTypeId, LeaveTypeName = r.LeaveType?.Name ?? string.Empty,
        FromDate = r.FromDate, ToDate = r.ToDate, TotalDays = r.TotalDays,
        Reason = r.Reason, Status = r.Status, ApprovedAt = r.ApprovedAt,
        RejectionReason = r.RejectionReason
    };
}
