using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Common.Logging;
using AttendanceSystem.Common.Models;
using AttendanceSystem.Common.Session;
using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Domain.Interfaces;

namespace AttendanceSystem.Application.Services;

/// <summary>Shift management and employee shift assignment service.</summary>
public class ShiftService : IShiftService
{
    private readonly IUnitOfWork _uow;
    private readonly IAuditService _audit;

    public ShiftService(IUnitOfWork uow, IAuditService audit) { _uow = uow; _audit = audit; }

    public async Task<Result<IEnumerable<ShiftDto>>> GetAllAsync()
    {
        try
        {
            var list = await _uow.Shifts.GetAllAsync();
            return Result<IEnumerable<ShiftDto>>.Success(list.OrderBy(s => s.Name).Select(MapShift));
        }
        catch (Exception ex) { AppLogger.Error("ShiftService.GetAllAsync", ex); return Result<IEnumerable<ShiftDto>>.Failure(ex.Message); }
    }

    public async Task<Result<ShiftDto>> GetByIdAsync(int id)
    {
        try
        {
            var s = await _uow.Shifts.GetByIdAsync(id);
            return s == null ? Result<ShiftDto>.Failure("Shift not found.") : Result<ShiftDto>.Success(MapShift(s));
        }
        catch (Exception ex) { return Result<ShiftDto>.Failure(ex.Message); }
    }

    public async Task<Result<ShiftDto>> SaveAsync(SaveShiftDto dto)
    {
        try
        {
            if (dto.EndTime <= dto.StartTime)
                return Result<ShiftDto>.Failure("End time must be after start time.");

            if (dto.Id == 0)
            {
                var entity = new Shift
                {
                    Name = dto.Name.Trim(), StartTime = dto.StartTime, EndTime = dto.EndTime,
                    GraceMinutes = dto.GraceMinutes, WeeklyOffDays = dto.WeeklyOffDays,
                    IsActive = dto.IsActive, CreatedBy = AppSession.UserId, CreatedAt = DateTime.Now
                };
                await _uow.Shifts.AddAsync(entity);
                await _uow.SaveChangesAsync();
                await _audit.LogAsync("Shifts", "Create", AppSession.UserId, "Shift", entity.Id);
                return Result<ShiftDto>.Success(MapShift(entity));
            }
            else
            {
                var entity = await _uow.Shifts.GetByIdAsync(dto.Id);
                if (entity == null) return Result<ShiftDto>.Failure("Shift not found.");
                entity.Name = dto.Name.Trim(); entity.StartTime = dto.StartTime; entity.EndTime = dto.EndTime;
                entity.GraceMinutes = dto.GraceMinutes; entity.WeeklyOffDays = dto.WeeklyOffDays; entity.IsActive = dto.IsActive;
                entity.ModifiedBy = AppSession.UserId; entity.ModifiedAt = DateTime.Now;
                await _uow.Shifts.UpdateAsync(entity);
                await _uow.SaveChangesAsync();
                return Result<ShiftDto>.Success(MapShift(entity));
            }
        }
        catch (Exception ex) { AppLogger.Error("ShiftService.SaveAsync", ex); return Result<ShiftDto>.Failure(ex.Message); }
    }

    public async Task<Result> DeleteAsync(int id, int deletedBy)
    {
        try
        {
            var assigned = await _uow.EmployeeShifts.FindAsync(es => es.ShiftId == id);
            if (assigned.Any()) return Result.Failure("Cannot delete — shift is assigned to employees.");
            var entity = await _uow.Shifts.GetByIdAsync(id);
            if (entity == null) return Result.Failure("Shift not found.");
            entity.IsDeleted = true; entity.ModifiedBy = deletedBy; entity.ModifiedAt = DateTime.Now;
            await _uow.Shifts.UpdateAsync(entity);
            await _uow.SaveChangesAsync();
            return Result.Success();
        }
        catch (Exception ex) { return Result.Failure(ex.Message); }
    }

    public async Task<Result<IEnumerable<EmployeeShiftDto>>> GetEmployeeShiftsAsync()
    {
        try
        {
            var assignments = await _uow.EmployeeShifts.FindAsync(es => !es.IsDeleted);
            var dtos = new List<EmployeeShiftDto>();
            foreach (var es in assignments.OrderBy(x => x.EmployeeId))
            {
                var emp   = await _uow.Employees.GetByIdAsync(es.EmployeeId);
                var shift = await _uow.Shifts.GetByIdAsync(es.ShiftId);
                if (emp == null || shift == null) continue;
                dtos.Add(new EmployeeShiftDto
                {
                    Id = es.Id, EmployeeId = es.EmployeeId,
                    EmployeeName = $"{emp.FirstName} {emp.LastName}",
                    EmployeeCode = emp.EmployeeCode,
                    ShiftId = es.ShiftId, ShiftName = shift.Name,
                    StartTimeDisplay = DateTime.Today.Add(shift.StartTime).ToString("hh:mm tt"),
                    EndTimeDisplay   = DateTime.Today.Add(shift.EndTime).ToString("hh:mm tt"),
                    EffectiveFrom = es.EffectiveFrom, EffectiveTo = es.EffectiveTo
                });
            }
            return Result<IEnumerable<EmployeeShiftDto>>.Success(dtos);
        }
        catch (Exception ex) { return Result<IEnumerable<EmployeeShiftDto>>.Failure(ex.Message); }
    }

    public async Task<Result> AssignShiftAsync(AssignShiftDto dto)
    {
        try
        {
            var existing = await _uow.EmployeeShifts.FindAsync(
                es => es.EmployeeId == dto.EmployeeId && es.EffectiveTo == null);
            foreach (var es in existing)
            {
                es.EffectiveTo = dto.EffectiveFrom.AddDays(-1);
                await _uow.EmployeeShifts.UpdateAsync(es);
            }
            var newAssign = new EmployeeShift
            {
                EmployeeId = dto.EmployeeId, ShiftId = dto.ShiftId,
                EffectiveFrom = dto.EffectiveFrom, EffectiveTo = dto.EffectiveTo,
                CreatedBy = AppSession.UserId, CreatedAt = DateTime.Now
            };
            await _uow.EmployeeShifts.AddAsync(newAssign);
            await _uow.SaveChangesAsync();
            await _audit.LogAsync("Shifts", "AssignShift", AppSession.UserId, "EmployeeShift", newAssign.Id);
            return Result.Success();
        }
        catch (Exception ex) { AppLogger.Error("ShiftService.AssignShiftAsync", ex); return Result.Failure(ex.Message); }
    }

    private static ShiftDto MapShift(Shift s) => new()
    {
        Id = s.Id, Name = s.Name, StartTime = s.StartTime, EndTime = s.EndTime,
        GraceMinutes = s.GraceMinutes, WeeklyOffDays = s.WeeklyOffDays, IsActive = s.IsActive
    };
}

