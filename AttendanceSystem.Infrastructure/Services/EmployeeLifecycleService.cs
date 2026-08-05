using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Common.Logging;
using AttendanceSystem.Common.Models;
using AttendanceSystem.Common.Session;
using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Domain.Enums;
using AttendanceSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.Infrastructure.Services;

/// <summary>
/// Transfers, status changes, resignation, documents and the employee profile.
///
/// Every operation here writes an <see cref="EmployeeHistory"/> row in the same transaction as
/// the change itself. That pairing is the point of the service: the employee row says where
/// somebody is now, and history says how they got there. Without it, editing a department
/// re-attributes months of attendance to a department the person never worked in, and no
/// report can tell you it happened.
/// </summary>
public class EmployeeLifecycleService : IEmployeeLifecycleService
{
    private readonly AttendanceDbContext _db;
    private readonly ICurrentUserContext _currentUser;

    /// <summary>Enough for a scanned certificate; small enough that a row stays sane.</summary>
    private const long MaxDocumentBytes = 10 * 1024 * 1024;

    public EmployeeLifecycleService(AttendanceDbContext db, ICurrentUserContext currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Profile
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<Result<EmployeeProfileDto>> GetProfileAsync(int employeeId)
    {
        try
        {
            var e = await _db.Employees
                .Include(x => x.Department).Include(x => x.Designation).Include(x => x.Branch)
                .FirstOrDefaultAsync(x => x.Id == employeeId);

            if (e == null) return Result<EmployeeProfileDto>.Failure("Employee not found.");

            var today = DateTime.Today;
            var monthStart = new DateTime(today.Year, today.Month, 1);

            // The shift in force today — the same resolution rule attendance uses: latest
            // EffectiveFrom among assignments covering the date.
            var shift = await _db.EmployeeShifts
                .Include(s => s.Shift)
                .Where(s => s.EmployeeId == employeeId
                         && s.EffectiveFrom <= today
                         && (s.EffectiveTo == null || s.EffectiveTo >= today))
                .OrderByDescending(s => s.EffectiveFrom)
                .Select(s => s.Shift)
                .FirstOrDefaultAsync();

            var logs = await _db.AttendanceLogs
                .Where(a => a.EmployeeId == employeeId && a.AttendanceDate >= monthStart && a.AttendanceDate <= today)
                .Select(a => a.Status)
                .ToListAsync();

            var service = today - e.JoiningDate.Date;
            var totalMonths = (int)(service.TotalDays / 30.44);

            var dto = new EmployeeProfileDto
            {
                Employee = MapEmployee(e),
                Status = e.Status,
                ResignationDate = e.ResignationDate,
                ResignationReason = e.ResignationReason,
                CurrentShift = shift?.Name,
                CurrentShiftTimes = shift == null ? null
                    : $"{shift.StartTime:hh\\:mm} – {shift.EndTime:hh\\:mm}",
                ServiceYears = totalMonths / 12,
                ServiceMonths = totalMonths % 12,
                PresentDays = logs.Count(s => s == AttendanceStatus.Present),
                LateDays    = logs.Count(s => s == AttendanceStatus.Late),
                AbsentDays  = logs.Count(s => s == AttendanceStatus.Absent),
                LeaveDays   = logs.Count(s => s == AttendanceStatus.OnLeave)
            };

            dto.History = (await GetHistoryInternalAsync(employeeId)).ToList();
            dto.Documents = (await GetDocumentsInternalAsync(employeeId)).ToList();
            dto.LeaveBalances = await BalancesAsync(employeeId, today.Year);

            return Result<EmployeeProfileDto>.Success(dto);
        }
        catch (Exception ex)
        {
            AppLogger.Error("EmployeeLifecycleService.GetProfileAsync", ex);
            return Result<EmployeeProfileDto>.Failure("Could not load that employee's profile.");
        }
    }

    private async Task<List<MyLeaveBalanceDto>> BalancesAsync(int employeeId, int year)
    {
        var types = await _db.LeaveTypes.Where(t => t.IsActive).ToListAsync();
        var used = await _db.LeaveRequests
            .Where(l => l.EmployeeId == employeeId
                     && l.Status == LeaveStatus.Approved
                     && l.FromDate.Year == year)
            .GroupBy(l => l.LeaveTypeId)
            .Select(g => new { LeaveTypeId = g.Key, Days = g.Sum(x => x.TotalDays) })
            .ToListAsync();

        return types.Select(t =>
        {
            var u = used.FirstOrDefault(x => x.LeaveTypeId == t.Id)?.Days ?? 0;
            return new MyLeaveBalanceDto
            {
                LeaveTypeId = t.Id, LeaveType = t.Name, IsPaid = t.IsPaid,
                Entitled = t.TotalDays, Used = u, Remaining = Math.Max(0, t.TotalDays - u)
            };
        }).ToList();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // History
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<Result<IEnumerable<EmployeeHistoryDto>>> GetHistoryAsync(int employeeId)
    {
        try { return Result<IEnumerable<EmployeeHistoryDto>>.Success(await GetHistoryInternalAsync(employeeId)); }
        catch (Exception ex)
        {
            AppLogger.Error("EmployeeLifecycleService.GetHistoryAsync", ex);
            return Result<IEnumerable<EmployeeHistoryDto>>.Failure("Could not load the history.");
        }
    }

    private async Task<List<EmployeeHistoryDto>> GetHistoryInternalAsync(int employeeId) =>
        await _db.EmployeeHistories
            .Where(h => h.EmployeeId == employeeId)
            .OrderByDescending(h => h.EffectiveDate).ThenByDescending(h => h.Id)
            .Select(h => new EmployeeHistoryDto
            {
                Id = h.Id, EmployeeId = h.EmployeeId, ChangeType = h.ChangeType,
                EffectiveDate = h.EffectiveDate, FromLabel = h.FromLabel, ToLabel = h.ToLabel,
                Reason = h.Reason, Notes = h.Notes, FromStatus = h.FromStatus, ToStatus = h.ToStatus,
                CreatedAt = h.CreatedAt
            })
            .ToListAsync();

    // ──────────────────────────────────────────────────────────────────────────
    // Transfer / status / resignation
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<Result> TransferAsync(TransferEmployeeDto dto)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var e = await _db.Employees
                .Include(x => x.Department).Include(x => x.Designation).Include(x => x.Branch)
                .FirstOrDefaultAsync(x => x.Id == dto.EmployeeId);
            if (e == null) return Result.Failure("Employee not found.");

            var from = new List<string>();
            var to = new List<string>();
            var type = EmployeeChangeType.Transfer;

            var history = new EmployeeHistory
            {
                EmployeeId = e.Id,
                EffectiveDate = dto.EffectiveDate.Date,
                Reason = dto.Reason,
                Notes = dto.Notes,
                CreatedAt = DateTime.Now,
                CreatedBy = _currentUser.UserId
            };

            if (dto.DepartmentId.HasValue && dto.DepartmentId != e.DepartmentId)
            {
                var target = await _db.Departments.FirstOrDefaultAsync(d => d.Id == dto.DepartmentId);
                if (target == null) return Result.Failure("That department does not exist.");

                history.FromDepartmentId = e.DepartmentId;
                history.ToDepartmentId = target.Id;
                from.Add(e.Department.Name);
                to.Add(target.Name);
                e.DepartmentId = target.Id;
            }

            if (dto.DesignationId.HasValue && dto.DesignationId != e.DesignationId)
            {
                var target = await _db.Designations.FirstOrDefaultAsync(d => d.Id == dto.DesignationId);
                if (target == null) return Result.Failure("That designation does not exist.");

                history.FromDesignationId = e.DesignationId;
                history.ToDesignationId = target.Id;
                from.Add(e.Designation.Name);
                to.Add(target.Name);
                e.DesignationId = target.Id;

                // A designation change on its own is a promotion, not a transfer. The
                // distinction is what people search the history for.
                if (!dto.DepartmentId.HasValue && !dto.BranchId.HasValue)
                    type = EmployeeChangeType.Promotion;
            }

            if (dto.BranchId.HasValue && dto.BranchId != e.BranchId)
            {
                var target = await _db.Branches.FirstOrDefaultAsync(b => b.Id == dto.BranchId);
                if (target == null) return Result.Failure("That branch does not exist.");

                history.FromBranchId = e.BranchId;
                history.ToBranchId = target.Id;
                from.Add(e.Branch.Name);
                to.Add(target.Name);
                e.BranchId = target.Id;
            }

            if (from.Count == 0)
                return Result.Failure("Nothing to transfer — department, designation and branch are unchanged.");

            history.ChangeType = type;
            history.FromLabel = string.Join(" · ", from);
            history.ToLabel = string.Join(" · ", to);

            e.ModifiedAt = DateTime.Now;
            e.ModifiedBy = _currentUser.UserId;

            _db.EmployeeHistories.Add(history);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return Result.Success();
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            AppLogger.Error("EmployeeLifecycleService.TransferAsync", ex);
            return Result.Failure("The transfer failed and nothing was changed.");
        }
    }

    public async Task<Result> ChangeStatusAsync(ChangeEmployeeStatusDto dto)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var e = await _db.Employees.FirstOrDefaultAsync(x => x.Id == dto.EmployeeId);
            if (e == null) return Result.Failure("Employee not found.");
            if (e.Status == dto.Status) return Result.Failure($"This employee is already {dto.Status}.");

            // Resignation carries a last working day and belongs on its own path, which sets
            // it; routing it through here would leave ResignationDate null and let attendance
            // keep arriving for somebody who has left.
            if (dto.Status is EmployeeStatus.Resigned or EmployeeStatus.Terminated)
                return Result.Failure("Use Resign to record a resignation or termination — it needs a last working day.");

            var from = e.Status;
            e.Status = dto.Status;
            e.IsActive = dto.Status == EmployeeStatus.Active;
            e.ModifiedAt = DateTime.Now;
            e.ModifiedBy = _currentUser.UserId;

            _db.EmployeeHistories.Add(new EmployeeHistory
            {
                EmployeeId = e.Id,
                ChangeType = EmployeeChangeType.StatusChange,
                EffectiveDate = dto.EffectiveDate.Date,
                FromStatus = from,
                ToStatus = dto.Status,
                FromLabel = from.ToString(),
                ToLabel = dto.Status.ToString(),
                Reason = dto.Reason,
                Notes = dto.Notes,
                CreatedAt = DateTime.Now,
                CreatedBy = _currentUser.UserId
            });

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return Result.Success();
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            AppLogger.Error("EmployeeLifecycleService.ChangeStatusAsync", ex);
            return Result.Failure("The status change failed and nothing was changed.");
        }
    }

    public async Task<Result> ResignAsync(ResignEmployeeDto dto)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var e = await _db.Employees.FirstOrDefaultAsync(x => x.Id == dto.EmployeeId);
            if (e == null) return Result.Failure("Employee not found.");

            if (dto.ResignationDate.Date < e.JoiningDate.Date)
                return Result.Failure("The last working day cannot be before the joining date.");

            var from = e.Status;
            var status = dto.IsTermination ? EmployeeStatus.Terminated : EmployeeStatus.Resigned;

            e.Status = status;
            e.IsActive = false;
            e.ResignationDate = dto.ResignationDate.Date;
            e.ResignationReason = dto.Reason;
            e.ModifiedAt = DateTime.Now;
            e.ModifiedBy = _currentUser.UserId;

            _db.EmployeeHistories.Add(new EmployeeHistory
            {
                EmployeeId = e.Id,
                ChangeType = EmployeeChangeType.Resignation,
                EffectiveDate = dto.ResignationDate.Date,
                FromStatus = from,
                ToStatus = status,
                FromLabel = from.ToString(),
                ToLabel = status.ToString(),
                Reason = dto.Reason,
                Notes = dto.Notes,
                CreatedAt = DateTime.Now,
                CreatedBy = _currentUser.UserId
            });

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return Result.Success();
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            AppLogger.Error("EmployeeLifecycleService.ResignAsync", ex);
            return Result.Failure("Recording the resignation failed and nothing was changed.");
        }
    }

    public async Task<Result> RejoinAsync(int employeeId, DateTime effectiveDate, string reason)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var e = await _db.Employees.FirstOrDefaultAsync(x => x.Id == employeeId);
            if (e == null) return Result.Failure("Employee not found.");
            if (e.Status == EmployeeStatus.Active) return Result.Failure("This employee is already active.");

            var from = e.Status;

            e.Status = EmployeeStatus.Active;
            e.IsActive = true;

            // Cleared, because a rejoined employee has no last working day. Their leaving is
            // not lost — the resignation stays in history, which is the whole point of it
            // being there rather than only on the row.
            e.ResignationDate = null;
            e.ResignationReason = null;
            e.ModifiedAt = DateTime.Now;
            e.ModifiedBy = _currentUser.UserId;

            _db.EmployeeHistories.Add(new EmployeeHistory
            {
                EmployeeId = e.Id,
                ChangeType = EmployeeChangeType.Rejoin,
                EffectiveDate = effectiveDate.Date,
                FromStatus = from,
                ToStatus = EmployeeStatus.Active,
                FromLabel = from.ToString(),
                ToLabel = EmployeeStatus.Active.ToString(),
                Reason = reason,
                CreatedAt = DateTime.Now,
                CreatedBy = _currentUser.UserId
            });

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return Result.Success();
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            AppLogger.Error("EmployeeLifecycleService.RejoinAsync", ex);
            return Result.Failure("Recording the rejoin failed and nothing was changed.");
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Documents
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<Result<IEnumerable<EmployeeDocumentDto>>> GetDocumentsAsync(int employeeId)
    {
        try { return Result<IEnumerable<EmployeeDocumentDto>>.Success(await GetDocumentsInternalAsync(employeeId)); }
        catch (Exception ex)
        {
            AppLogger.Error("EmployeeLifecycleService.GetDocumentsAsync", ex);
            return Result<IEnumerable<EmployeeDocumentDto>>.Failure("Could not load the documents.");
        }
    }

    /// <summary>Projects without Content, so listing does not drag every file into memory.</summary>
    private async Task<List<EmployeeDocumentDto>> GetDocumentsInternalAsync(int employeeId) =>
        await _db.EmployeeDocuments
            .Where(d => d.EmployeeId == employeeId)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new EmployeeDocumentDto
            {
                Id = d.Id, EmployeeId = d.EmployeeId, DocumentType = d.DocumentType,
                Title = d.Title, FileName = d.FileName, ContentType = d.ContentType,
                SizeBytes = d.SizeBytes, ExpiryDate = d.ExpiryDate, Notes = d.Notes,
                CreatedAt = d.CreatedAt
            })
            .ToListAsync();

    public async Task<Result<EmployeeDocumentDto>> UploadDocumentAsync(
        int employeeId, EmployeeDocumentType type, string title, string fileName,
        string contentType, byte[] content, DateTime? expiryDate, string? notes)
    {
        try
        {
            if (!await _db.Employees.AnyAsync(e => e.Id == employeeId))
                return Result<EmployeeDocumentDto>.Failure("Employee not found.");

            if (content == null || content.Length == 0)
                return Result<EmployeeDocumentDto>.Failure("The uploaded file is empty.");

            if (content.Length > MaxDocumentBytes)
                return Result<EmployeeDocumentDto>.Failure(
                    $"That file is {content.Length / 1048576.0:0.#} MB. The limit is 10 MB.");

            // Executables are refused outright. These are served back to a browser, and a
            // document store that will hand out anything uploaded to it is a distribution
            // channel for whatever somebody puts in.
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            string[] blocked = [".exe", ".dll", ".bat", ".cmd", ".com", ".scr", ".ps1", ".js",
                                ".vbs", ".jar", ".msi", ".sh", ".htm", ".html", ".svg"];
            if (blocked.Contains(ext))
                return Result<EmployeeDocumentDto>.Failure($"'{ext}' files cannot be stored as documents.");

            var doc = new EmployeeDocument
            {
                EmployeeId = employeeId,
                DocumentType = type,
                Title = string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(fileName) : title.Trim(),
                FileName = Path.GetFileName(fileName),
                ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
                Content = content,
                SizeBytes = content.Length,
                ExpiryDate = expiryDate,
                Notes = notes,
                CreatedAt = DateTime.Now,
                CreatedBy = _currentUser.UserId
            };

            _db.EmployeeDocuments.Add(doc);
            await _db.SaveChangesAsync();

            return Result<EmployeeDocumentDto>.Success(new EmployeeDocumentDto
            {
                Id = doc.Id, EmployeeId = doc.EmployeeId, DocumentType = doc.DocumentType,
                Title = doc.Title, FileName = doc.FileName, ContentType = doc.ContentType,
                SizeBytes = doc.SizeBytes, ExpiryDate = doc.ExpiryDate, Notes = doc.Notes,
                CreatedAt = doc.CreatedAt
            });
        }
        catch (Exception ex)
        {
            AppLogger.Error("EmployeeLifecycleService.UploadDocumentAsync", ex);
            return Result<EmployeeDocumentDto>.Failure("The upload failed.");
        }
    }

    public async Task<Result<ExportFileDto>> DownloadDocumentAsync(int documentId)
    {
        try
        {
            var doc = await _db.EmployeeDocuments.FirstOrDefaultAsync(d => d.Id == documentId);
            if (doc == null) return Result<ExportFileDto>.Failure("Document not found.");

            return Result<ExportFileDto>.Success(new ExportFileDto(doc.Content, doc.FileName, doc.ContentType));
        }
        catch (Exception ex)
        {
            AppLogger.Error("EmployeeLifecycleService.DownloadDocumentAsync", ex);
            return Result<ExportFileDto>.Failure("Could not read that document.");
        }
    }

    public async Task<Result> DeleteDocumentAsync(int documentId)
    {
        try
        {
            var doc = await _db.EmployeeDocuments.FirstOrDefaultAsync(d => d.Id == documentId);
            if (doc == null) return Result.Failure("Document not found.");

            // Soft delete, like everything else — a contract removed by mistake is
            // recoverable, and the bytes are already in every backup taken since.
            doc.IsDeleted = true;
            doc.ModifiedAt = DateTime.Now;
            doc.ModifiedBy = _currentUser.UserId;

            await _db.SaveChangesAsync();
            return Result.Success();
        }
        catch (Exception ex)
        {
            AppLogger.Error("EmployeeLifecycleService.DeleteDocumentAsync", ex);
            return Result.Failure("Could not delete that document.");
        }
    }

    private static EmployeeDto MapEmployee(Employee e) => new()
    {
        Id = e.Id, EmployeeCode = e.EmployeeCode, UserCode = e.UserCode,
        NameWithInitials = e.NameWithInitials, Nic = e.Nic,
        FirstName = e.FirstName, LastName = e.LastName,
        Email = e.Email, Phone = e.Phone, DateOfBirth = e.DateOfBirth,
        JoiningDate = e.JoiningDate, Gender = e.Gender, Address = e.Address, Photo = e.Photo,
        DepartmentId = e.DepartmentId, DepartmentName = e.Department?.Name ?? string.Empty,
        DesignationId = e.DesignationId, DesignationName = e.Designation?.Name ?? string.Empty,
        BranchId = e.BranchId, BranchName = e.Branch?.Name ?? string.Empty,
        IsActive = e.IsActive, CreatedAt = e.CreatedAt, BiometricEnrollId = e.BiometricEnrollId
    };
}
