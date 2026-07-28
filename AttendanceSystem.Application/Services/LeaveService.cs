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
    public LeaveService(IUnitOfWork uow, IAuditService audit) { _uow = uow; _audit = audit; }

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
                var entity = new LeaveType { Name = dto.Name.Trim(), TotalDays = dto.TotalDays, IsPaid = dto.IsPaid, IsActive = dto.IsActive, CreatedBy = AppSession.UserId, CreatedAt = DateTime.Now };
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
                entity.ModifiedBy = AppSession.UserId; entity.ModifiedAt = DateTime.Now;
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
            entity.IsDeleted = true; entity.ModifiedBy = AppSession.UserId; entity.ModifiedAt = DateTime.Now;
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
            var list = await _uow.Leaves.GetPendingAsync();
            return Result<IEnumerable<LeaveRequestDto>>.Success(list.Select(MapRequest));
        }
        catch (Exception ex) { return Result<IEnumerable<LeaveRequestDto>>.Failure(ex.Message); }
    }

    public async Task<Result<LeaveRequestDto>> ApplyLeaveAsync(ApplyLeaveDto dto)
    {
        try
        {
            if (dto.ToDate < dto.FromDate)
                return Result<LeaveRequestDto>.Failure("To date must be after from date.");

            var leaveType = await _uow.LeaveTypes.GetByIdAsync(dto.LeaveTypeId);
            if (leaveType == null) return Result<LeaveRequestDto>.Failure("Leave type not found.");

            var totalDays = (int)(dto.ToDate.Date - dto.FromDate.Date).TotalDays + 1;

            // Check balance
            var usedDays = await _uow.Leaves.GetUsedLeaveDaysAsync(dto.EmployeeId, dto.LeaveTypeId, dto.FromDate.Year);
            if (usedDays + totalDays > leaveType.TotalDays)
                return Result<LeaveRequestDto>.Failure($"Insufficient leave balance. Available: {leaveType.TotalDays - usedDays} day(s).");

            var entity = new LeaveRequest
            {
                EmployeeId = dto.EmployeeId, LeaveTypeId = dto.LeaveTypeId,
                FromDate = dto.FromDate, ToDate = dto.ToDate, TotalDays = totalDays,
                Reason = dto.Reason, Status = LeaveStatus.Pending,
                CreatedBy = AppSession.UserId, CreatedAt = DateTime.Now
            };
            await _uow.Leaves.AddAsync(entity);
            await _uow.SaveChangesAsync();
            await _audit.LogAsync("Leave", "Apply", AppSession.UserId, "LeaveRequest", entity.Id);

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

            entity.Status = dto.IsApproved ? LeaveStatus.Approved : LeaveStatus.Rejected;
            entity.ApprovedBy = actionBy; entity.ApprovedAt = DateTime.Now;
            if (!dto.IsApproved) entity.RejectionReason = dto.RejectionReason;
            entity.ModifiedBy = actionBy; entity.ModifiedAt = DateTime.Now;
            await _uow.Leaves.UpdateAsync(entity);
            await _uow.SaveChangesAsync();

            var action = dto.IsApproved ? "Approve" : "Reject";
            await _audit.LogAsync("Leave", action, actionBy, "LeaveRequest", entity.Id);
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
                var used = await _uow.Leaves.GetUsedLeaveDaysAsync(employeeId, lt.Id, DateTime.Now.Year);
                balances.Add(new LeaveBalanceDto
                {
                    EmployeeId = employeeId,
                    EmployeeName = $"{emp.FirstName} {emp.LastName}",
                    LeaveTypeName = lt.Name, TotalAllowed = lt.TotalDays, UsedDays = used
                });
            }
            return Result<IEnumerable<LeaveBalanceDto>>.Success(balances);
        }
        catch (Exception ex) { return Result<IEnumerable<LeaveBalanceDto>>.Failure(ex.Message); }
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
