using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Common.Constants;
using AttendanceSystem.Common.Logging;
using AttendanceSystem.Common.Models;
using AttendanceSystem.Common.Session;
using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Domain.Interfaces;

namespace AttendanceSystem.Application.Services;

/// <summary>Holiday management service.</summary>
public class HolidayService : IHolidayService
{
    private readonly IUnitOfWork _uow;
    private readonly IAuditService _audit;
    private readonly ICurrentUserContext _currentUser;
    public HolidayService(IUnitOfWork uow, IAuditService audit, ICurrentUserContext currentUser) { _uow = uow; _audit = audit; _currentUser = currentUser; }

    public async Task<Result<IEnumerable<HolidayDto>>> GetAllAsync()
    {
        try
        {
            var list = await _uow.Holidays.GetAllAsync();
            return Result<IEnumerable<HolidayDto>>.Success(list.OrderBy(h => h.HolidayDate).Select(Map));
        }
        catch (Exception ex) { return Result<IEnumerable<HolidayDto>>.Failure(ex.Message); }
    }

    public async Task<Result<IEnumerable<HolidayDto>>> GetByYearAsync(int year)
    {
        try
        {
            var list = await _uow.Holidays.GetByYearAsync(year);
            return Result<IEnumerable<HolidayDto>>.Success(list.Select(Map));
        }
        catch (Exception ex) { return Result<IEnumerable<HolidayDto>>.Failure(ex.Message); }
    }

    public async Task<Result<HolidayDto>> SaveAsync(SaveHolidayDto dto)
    {
        try
        {
            if (dto.Id == 0)
            {
                var entity = new Holiday
                {
                    Name = dto.Name.Trim(), HolidayDate = dto.HolidayDate.Date,
                    HolidayType = dto.HolidayType, Description = dto.Description?.Trim(),
                    IsRecurring = dto.IsRecurring, CreatedBy = _currentUser.UserId, CreatedAt = DateTime.Now
                };
                await _uow.Holidays.AddAsync(entity);
                await _uow.SaveChangesAsync();
                await _audit.LogAsync("Holidays", "Create", _currentUser.UserId, "Holiday", entity.Id);
                return Result<HolidayDto>.Success(Map(entity));
            }
            else
            {
                var entity = await _uow.Holidays.GetByIdAsync(dto.Id);
                if (entity == null) return Result<HolidayDto>.Failure("Holiday not found.");
                entity.Name = dto.Name.Trim(); entity.HolidayDate = dto.HolidayDate.Date;
                entity.HolidayType = dto.HolidayType; entity.Description = dto.Description?.Trim();
                entity.IsRecurring = dto.IsRecurring;
                entity.ModifiedBy = _currentUser.UserId; entity.ModifiedAt = DateTime.Now;
                await _uow.Holidays.UpdateAsync(entity);
                await _uow.SaveChangesAsync();
                return Result<HolidayDto>.Success(Map(entity));
            }
        }
        catch (Exception ex) { AppLogger.Error("HolidayService.SaveAsync", ex); return Result<HolidayDto>.Failure(ex.Message); }
    }

    public async Task<Result> DeleteAsync(int id, int deletedBy)
    {
        try
        {
            var entity = await _uow.Holidays.GetByIdAsync(id);
            if (entity == null) return Result.Failure("Holiday not found.");
            entity.IsDeleted = true; entity.ModifiedBy = deletedBy; entity.ModifiedAt = DateTime.Now;
            await _uow.Holidays.UpdateAsync(entity);
            await _uow.SaveChangesAsync();
            return Result.Success();
        }
        catch (Exception ex) { return Result.Failure(ex.Message); }
    }

    private static HolidayDto Map(Holiday h) => new()
    {
        Id = h.Id, Name = h.Name, HolidayDate = h.HolidayDate,
        HolidayType = h.HolidayType, Description = h.Description, IsRecurring = h.IsRecurring
    };
}

/// <summary>Company settings read/write service.</summary>
public class SettingsService : ISettingsService
{
    private readonly IUnitOfWork _uow;
    private readonly IAuditService _audit;

    public SettingsService(IUnitOfWork uow, IAuditService audit)
    {
        _uow = uow;
        _audit = audit;
    }

    public async Task<Result<CompanySettingsDto>> GetAsync()
    {
        try
        {
            var all = await _uow.CompanySettings.GetAllAsync();
            var s = all.FirstOrDefault();
            if (s == null) return Result<CompanySettingsDto>.Failure("Settings not configured.");
            return Result<CompanySettingsDto>.Success(Map(s));
        }
        catch (Exception ex) { return Result<CompanySettingsDto>.Failure(ex.Message); }
    }

    public async Task<Result> SaveAsync(CompanySettingsDto dto, int modifiedBy)
    {
        try
        {
            var all = await _uow.CompanySettings.GetAllAsync();
            var entity = all.FirstOrDefault();
            if (entity == null)
            {
                entity = new CompanySettings();
                await _uow.CompanySettings.AddAsync(entity);
            }

            // Working hours and the late tolerance live here, so a settings change moves numbers
            // on everyone's record. Nothing was audited at all before.
            var before = AuditSnapshot.Capture(entity);

            entity.CompanyName = dto.CompanyName; entity.Address = dto.Address;
            entity.Phone = dto.Phone; entity.Email = dto.Email; entity.Website = dto.Website;
            entity.LogoPath = dto.LogoPath; entity.WorkStartTime = dto.WorkStartTime;
            entity.WorkEndTime = dto.WorkEndTime; entity.WeekendDays = dto.WeekendDays;
            entity.MaxLateMinutes = dto.MaxLateMinutes;
            // Clamped rather than rejected: an out-of-range page size is a slip, not a reason
            // to refuse the whole settings save. 0 is kept as-is and means "show everything".
            entity.DefaultPageSize = dto.DefaultPageSize == 0 ? 0 : Math.Clamp(dto.DefaultPageSize, 5, 500);
            entity.ConfirmBeforeDelete = dto.ConfirmBeforeDelete;
            entity.ModifiedBy = modifiedBy; entity.ModifiedAt = DateTime.Now;
            await _uow.CompanySettings.UpdateAsync(entity);
            await _uow.SaveChangesAsync();

            var (oldValues, newValues) = AuditSnapshot.DiffAgainst(before, entity);
            await _audit.LogAsync(AppConstants.Modules.Settings, "Update", modifiedBy,
                nameof(CompanySettings), entity.Id, oldValues, newValues);

            return Result.Success();
        }
        catch (Exception ex) { return Result.Failure(ex.Message); }
    }

    private static CompanySettingsDto Map(CompanySettings s) => new()
    {
        Id = s.Id, CompanyName = s.CompanyName, Address = s.Address, Phone = s.Phone,
        Email = s.Email, Website = s.Website, LogoPath = s.LogoPath,
        WorkStartTime = s.WorkStartTime, WorkEndTime = s.WorkEndTime,
        WeekendDays = s.WeekendDays, MaxLateMinutes = s.MaxLateMinutes,
        DefaultPageSize = s.DefaultPageSize, ConfirmBeforeDelete = s.ConfirmBeforeDelete
    };
}

/// <summary>Audit log service — writes and reads user activity logs.</summary>
public class AuditService : IAuditService
{
    private readonly IUnitOfWork _uow;
    public AuditService(IUnitOfWork uow) => _uow = uow;

    /// <summary>
    /// Writes an audit entry and commits it.
    ///
    /// The commit is deliberate. The repository only stages the row and leaves the transaction
    /// boundary to the caller, but every caller in the system logs *after* its own
    /// SaveChangesAsync — so the staged row was never written and the only action that ever
    /// reached the table was Logout, which happens to save afterwards. Saving here makes the
    /// entry durable regardless of call order; by that point the caller's own work is already
    /// committed, so there is nothing else pending to sweep up.
    ///
    /// Timestamped with DateTime.Now to match every other entity in the system — CreatedAt was
    /// UTC here alone, which showed audit rows hours adrift from the records they describe.
    /// </summary>
    public async Task LogAsync(string module, string action, int? userId = null,
        string? entityName = null, int? entityId = null,
        string? oldValues = null, string? newValues = null)
    {
        try
        {
            await _uow.AuditLogs.AddAsync(new Domain.Entities.AuditLog
            {
                Module = module, Action = action, UserId = userId,
                EntityName = entityName, EntityId = entityId,
                OldValues = oldValues, NewValues = newValues, CreatedAt = DateTime.Now
            });
            await _uow.SaveChangesAsync();
        }
        // Auditing must never take down the operation it is recording.
        catch (Exception ex) { AppLogger.Error("AuditService.LogAsync", ex); }
    }

    public async Task<Result<IEnumerable<AuditLogDto>>> GetRecentAsync(int count = 100)
    {
        try
        {
            var list = await _uow.AuditLogs.GetRecentAsync(count);
            return Result<IEnumerable<AuditLogDto>>.Success(list.Select(Map));
        }
        catch (Exception ex) { return Result<IEnumerable<AuditLogDto>>.Failure(ex.Message); }
    }

    public async Task<Result<IEnumerable<AuditLogDto>>> GetByModuleAsync(string module, int count = 100)
    {
        try
        {
            var list = await _uow.AuditLogs.GetByModuleAsync(module, count);
            return Result<IEnumerable<AuditLogDto>>.Success(list.Select(Map));
        }
        catch (Exception ex) { return Result<IEnumerable<AuditLogDto>>.Failure(ex.Message); }
    }

    public async Task<Result<PagedResult<AuditLogDto>>> GetPagedAsync(
        string? module, string? search, PageRequest page)
    {
        try
        {
            var (items, total) = await _uow.AuditLogs.GetPagedAsync(
                module, search, page.Skip, page.PageSize);

            return Result<PagedResult<AuditLogDto>>.Success(new PagedResult<AuditLogDto>
            {
                Items = items.Select(Map).ToList(),
                Page = page.Page,
                PageSize = page.PageSize,
                TotalCount = total
            });
        }
        catch (Exception ex)
        {
            AppLogger.Error("AuditService.GetPagedAsync", ex);
            return Result<PagedResult<AuditLogDto>>.Failure("Failed to load the audit log.");
        }
    }

    private static AuditLogDto Map(Domain.Entities.AuditLog a) => new()
    {
        Id = a.Id, Username = a.User?.Username, Action = a.Action,
        Module = a.Module, EntityName = a.EntityName, EntityId = a.EntityId,
        OldValues = a.OldValues, NewValues = a.NewValues, CreatedAt = a.CreatedAt
    };
}
