using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Application.Services;
using AttendanceSystem.Common.Logging;
using AttendanceSystem.Common.Models;
using AttendanceSystem.Common.Session;
using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Domain.Enums;
using AttendanceSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.Infrastructure.Services;

/// <summary>
/// Attendance period locks, and recalculating a period.
///
/// The lock check runs on every attendance save and once per imported day, so the lock list is
/// read once per request and held. It is a handful of rows and changes when somebody closes a
/// month — querying it per punch would turn a 136,000-row import into 136,000 extra round
/// trips.
/// </summary>
public class AttendanceLockService : IAttendanceLockService
{
    private readonly AttendanceDbContext _db;
    private readonly ICurrentUserContext _currentUser;

    private List<AttendancePeriodLock>? _cache;

    public AttendanceLockService(AttendanceDbContext db, ICurrentUserContext currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    private async Task<List<AttendancePeriodLock>> LocksAsync() =>
        _cache ??= await _db.AttendancePeriodLocks.AsNoTracking().ToListAsync();

    // ──────────────────────────────────────────────────────────────────────────
    // Locks
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<Result<IEnumerable<AttendancePeriodLockDto>>> GetLocksAsync()
    {
        try
        {
            var locks = await _db.AttendancePeriodLocks
                .Include(l => l.Branch)
                .OrderByDescending(l => l.FromDate)
                .ToListAsync();

            var users = await _db.Users.AsNoTracking()
                .ToDictionaryAsync(u => u.Id, u => u.FullName);

            var dtos = new List<AttendancePeriodLockDto>();
            foreach (var l in locks)
            {
                // What the lock is protecting, so an operator can see the weight of unlocking it.
                var count = await _db.AttendanceLogs.CountAsync(a =>
                    a.AttendanceDate >= l.FromDate && a.AttendanceDate <= l.ToDate &&
                    (l.BranchId == null || a.Employee.BranchId == l.BranchId));

                dtos.Add(new AttendancePeriodLockDto
                {
                    Id = l.Id, FromDate = l.FromDate, ToDate = l.ToDate,
                    BranchId = l.BranchId,
                    BranchName = l.Branch?.Name ?? "All branches",
                    Reason = l.Reason, CreatedAt = l.CreatedAt,
                    LockedByName = l.CreatedBy.HasValue && users.TryGetValue(l.CreatedBy.Value, out var n) ? n : null,
                    RecordCount = count
                });
            }

            return Result<IEnumerable<AttendancePeriodLockDto>>.Success(dtos);
        }
        catch (Exception ex)
        {
            AppLogger.Error("AttendanceLockService.GetLocksAsync", ex);
            return Result<IEnumerable<AttendancePeriodLockDto>>.Failure("Could not load the locked periods.");
        }
    }

    public async Task<Result> LockPeriodAsync(LockPeriodDto dto)
    {
        try
        {
            if (dto.ToDate.Date < dto.FromDate.Date)
                return Result.Failure("The end date must be on or after the start date.");

            // Overlapping locks for the same scope would make "is this locked" ambiguous and
            // leave a period still closed after one of them is removed.
            var existing = await _db.AttendancePeriodLocks
                .Include(l => l.Branch)
                .Where(l => l.FromDate <= dto.ToDate.Date && l.ToDate >= dto.FromDate.Date)
                .ToListAsync();

            var clash = existing.FirstOrDefault(l => l.BranchId == null
                                                  || dto.BranchId == null
                                                  || l.BranchId == dto.BranchId);
            if (clash != null)
                return Result.Failure(
                    $"That overlaps a period already locked " +
                    $"({clash.FromDate:dd-MMM-yyyy} – {clash.ToDate:dd-MMM-yyyy}, " +
                    $"{clash.Branch?.Name ?? "all branches"}).");

            _db.AttendancePeriodLocks.Add(new AttendancePeriodLock
            {
                FromDate = dto.FromDate.Date,
                ToDate = dto.ToDate.Date,
                BranchId = dto.BranchId,
                Reason = dto.Reason.Trim(),
                CreatedAt = DateTime.Now,
                CreatedBy = _currentUser.UserId
            });

            await _db.SaveChangesAsync();
            _cache = null;
            return Result.Success();
        }
        catch (Exception ex)
        {
            AppLogger.Error("AttendanceLockService.LockPeriodAsync", ex);
            return Result.Failure("Could not lock that period.");
        }
    }

    public async Task<Result> UnlockPeriodAsync(UnlockPeriodDto dto)
    {
        try
        {
            var l = await _db.AttendancePeriodLocks.FirstOrDefaultAsync(x => x.Id == dto.Id);
            if (l == null) return Result.Failure("That locked period no longer exists.");

            // Soft delete, with the reason kept. A period that was closed when payroll ran is
            // still verifiably closed at that moment, even after somebody reopens it.
            l.IsDeleted = true;
            l.UnlockReason = dto.Reason.Trim();
            l.UnlockedAt = DateTime.Now;
            l.UnlockedBy = _currentUser.UserId;
            l.ModifiedAt = DateTime.Now;
            l.ModifiedBy = _currentUser.UserId;

            await _db.SaveChangesAsync();
            _cache = null;
            return Result.Success();
        }
        catch (Exception ex)
        {
            AppLogger.Error("AttendanceLockService.UnlockPeriodAsync", ex);
            return Result.Failure("Could not unlock that period.");
        }
    }

    public async Task<bool> IsLockedAsync(DateTime date, int? branchId = null) =>
        (await GetLockForAsync(date, branchId)) != null;

    public async Task<AttendancePeriodLockDto?> GetLockForAsync(DateTime date, int? branchId = null)
    {
        var d = date.Date;
        var match = (await LocksAsync()).FirstOrDefault(l =>
            l.FromDate <= d && l.ToDate >= d &&
            // A company-wide lock covers every branch; a branch lock covers only its own.
            // An unknown branch is treated as covered by a company-wide lock only.
            (l.BranchId == null || l.BranchId == branchId));

        return match == null ? null : new AttendancePeriodLockDto
        {
            Id = match.Id, FromDate = match.FromDate, ToDate = match.ToDate,
            BranchId = match.BranchId, Reason = match.Reason, CreatedAt = match.CreatedAt
        };
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Reprocess
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<Result<ReprocessResultDto>> ReprocessAsync(ReprocessRequestDto dto)
    {
        var result = new ReprocessResultDto();

        try
        {
            var from = dto.FromDate.Date;
            var to = dto.ToDate.Date;
            if (to < from) return Result<ReprocessResultDto>.Failure("The end date must be on or after the start date.");

            var logs = await _db.AttendanceLogs
                .Include(a => a.Employee)
                .Where(a => a.AttendanceDate >= from && a.AttendanceDate <= to
                         && (dto.EmployeeId == null || a.EmployeeId == dto.EmployeeId)
                         && (dto.DepartmentId == null || a.Employee.DepartmentId == dto.DepartmentId))
                .ToListAsync();

            result.Examined = logs.Count;
            if (logs.Count == 0)
                return Result<ReprocessResultDto>.Success(result);

            var shifts = await _db.Shifts.AsNoTracking().ToDictionaryAsync(s => s.Id);
            var assignments = await _db.EmployeeShifts.AsNoTracking().ToListAsync();

            var holidays = (await _db.Holidays.AsNoTracking()
                    .Where(h => (h.HolidayDate >= from && h.HolidayDate <= to) || h.IsRecurring)
                    .ToListAsync());

            foreach (var log in logs)
            {
                if (log.IsManual && !dto.IncludeManual) { result.SkippedManual++; continue; }

                if (await IsLockedAsync(log.AttendanceDate, log.Employee?.BranchId))
                {
                    result.SkippedLocked++;
                    continue;
                }

                var shift = ResolveShift(assignments, shifts, log.EmployeeId, log.AttendanceDate);
                if (shift == null) { result.SkippedNoShift++; continue; }

                var isHoliday = holidays.Any(h =>
                    h.HolidayDate.Date == log.AttendanceDate.Date ||
                    (h.IsRecurring && h.HolidayDate.Month == log.AttendanceDate.Month
                                   && h.HolidayDate.Day == log.AttendanceDate.Day
                                   && h.HolidayDate.Year <= log.AttendanceDate.Year));

                var before = (log.IsLate, log.LateMinutes, log.IsEarlyLeave, log.EarlyLeaveMinutes,
                              log.GrossHours, log.WorkingHours, log.OvertimeMinutes, log.Status);

                // The same calculator every other path uses. Reprocessing that reimplemented
                // the rules would drift from check-in and import, which is the drift this
                // calculator was consolidated to remove.
                var calc = AttendanceCalculator.Calculate(
                    shift, log.AttendanceDate, log.CheckIn, log.CheckOut,
                    isHoliday, log.Status == AttendanceStatus.OnLeave);

                // An operator-set status is a decision about the day, not a derived value;
                // recalculating hours must not quietly turn "On Leave" back into "Absent".
                AttendanceCalculator.Apply(log, calc,
                    log.Status == AttendanceStatus.OnLeave ? AttendanceStatus.OnLeave : null);

                var after = (log.IsLate, log.LateMinutes, log.IsEarlyLeave, log.EarlyLeaveMinutes,
                             log.GrossHours, log.WorkingHours, log.OvertimeMinutes, log.Status);

                if (before.Equals(after)) { result.Unchanged++; continue; }

                log.ModifiedAt = DateTime.Now;
                log.ModifiedBy = _currentUser.UserId;
                result.Updated++;
            }

            if (result.Updated > 0) await _db.SaveChangesAsync();

            if (result.SkippedLocked > 0)
                result.Warnings.Add($"{result.SkippedLocked} record(s) fall in a locked period and were left alone.");
            if (result.SkippedManual > 0)
                result.Warnings.Add($"{result.SkippedManual} manually corrected record(s) were left alone.");
            if (result.SkippedNoShift > 0)
                result.Warnings.Add($"{result.SkippedNoShift} record(s) have no shift assigned for that date and cannot be calculated.");

            return Result<ReprocessResultDto>.Success(result);
        }
        catch (Exception ex)
        {
            AppLogger.Error("AttendanceLockService.ReprocessAsync", ex);
            return Result<ReprocessResultDto>.Failure("Reprocessing failed. See the log for details.");
        }
    }

    /// <summary>
    /// The shift in force on a date: assignments covering it, latest EffectiveFrom wins —
    /// the same rule attendance and the importer use.
    /// </summary>
    private static Shift? ResolveShift(
        List<EmployeeShift> assignments, Dictionary<int, Shift> shifts, int employeeId, DateTime date)
    {
        var match = assignments
            .Where(a => a.EmployeeId == employeeId
                     && a.EffectiveFrom.Date <= date.Date
                     && (a.EffectiveTo == null || a.EffectiveTo.Value.Date >= date.Date))
            .OrderByDescending(a => a.EffectiveFrom)
            .FirstOrDefault();

        return match != null && shifts.TryGetValue(match.ShiftId, out var s) ? s : null;
    }
}
