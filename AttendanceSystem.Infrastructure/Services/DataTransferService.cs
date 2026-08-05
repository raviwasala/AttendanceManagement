using System.IO.Compression;
using System.Text;
using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Common.Logging;
using AttendanceSystem.Common.Models;
using AttendanceSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.Infrastructure.Services;

/// <summary>
/// Dataset exports, backup archives and restore.
///
/// The backup format is a ZIP of one CSV per table plus a manifest, not a SQL <c>.bak</c>.
/// A .bak is byte-exact and restores faster, but <c>RESTORE DATABASE</c> demands exclusive
/// access: it must terminate every connection, including the one serving the request that
/// asked for it. The application would kill itself mid-restore, and a failure would leave no
/// running application and an unusable database — the worst outcome available to a system
/// that decides pay. A logical archive is slower and does not preserve identity seeds, but it
/// restores inside a transaction, can be loaded onto a different server, and cannot destroy a
/// live database.
/// </summary>
public class DataTransferService : IDataTransferService
{
    private readonly AttendanceDbContext _db;

    public DataTransferService(AttendanceDbContext db) => _db = db;

    // Order matters on restore: a table is only written after everything it points at.
    // Employees before AttendanceLogs, lookups before Employees.
    private static readonly string[] TableOrder =
    [
        "Branches", "Departments", "Designations", "Shifts",
        "Roles", "Permissions", "RolePermissions", "Users",
        "Employees", "EmployeeShifts",
        "LeaveTypes", "LeaveRequests", "Holidays",
        "AttendanceLogs", "AttendanceSummaries",
        "OvertimeRules", "OvertimeRecords",
        "Devices", "DeviceUserMappings", "DevicePunches", "DeviceSyncLogs",
        "CompanySettings"
    ];

    // ──────────────────────────────────────────────────────────────────────────
    // Dataset export
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<Result<ExportFileDto>> ExportAsync(ExportDataset dataset, DateTime? from = null, DateTime? to = null)
    {
        try
        {
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmm");

            return dataset switch
            {
                ExportDataset.Employees  => Ok(await EmployeesCsv(), $"employees-{stamp}.csv"),
                ExportDataset.Attendance => Ok(await AttendanceCsv(Range(from), Range(to, true)), $"attendance-{stamp}.csv"),
                ExportDataset.Leave      => Ok(await LeaveCsv(Range(from), Range(to, true)), $"leave-{stamp}.csv"),
                ExportDataset.Overtime   => Ok(await OvertimeCsv(Range(from), Range(to, true)), $"overtime-{stamp}.csv"),
                _ => Result<ExportFileDto>.Failure("Unknown dataset.")
            };
        }
        catch (Exception ex)
        {
            AppLogger.Error("DataTransferService.ExportAsync", ex);
            return Result<ExportFileDto>.Failure("Export failed. See the log for details.");
        }

        static DateTime Range(DateTime? d, bool isEnd = false) =>
            d?.Date ?? (isEnd ? DateTime.Today : new DateTime(2000, 1, 1));

        static Result<ExportFileDto> Ok(string csv, string name) =>
            Result<ExportFileDto>.Success(new ExportFileDto(CsvWriter.ToBytes(csv), name, "text/csv"));
    }

    private async Task<string> EmployeesCsv()
    {
        // Field order matches the employee import template, so an export can be edited and
        // fed straight back in — which is how a bulk correction is actually made.
        var rows = await _db.Employees
            .Include(e => e.Department).Include(e => e.Designation).Include(e => e.Branch)
            .OrderBy(e => e.EmployeeCode)
            .Select(e => new object?[]
            {
                e.EmployeeCode, e.UserCode, e.FirstName, e.LastName, e.NameWithInitials, e.Nic,
                e.Department.Name, e.Designation.Name, e.Branch.Name,
                e.Email, e.Phone, e.Gender, e.DateOfBirth, e.JoiningDate,
                e.BiometricEnrollId, e.Address, e.IsActive
            })
            .ToListAsync();

        return CsvWriter.Build(EmployeeImportService.TemplateHeader, rows);
    }

    private async Task<string> AttendanceCsv(DateTime from, DateTime to)
    {
        var rows = await _db.AttendanceLogs
            .Include(a => a.Employee).ThenInclude(e => e.Department)
            .Where(a => a.AttendanceDate >= from && a.AttendanceDate <= to)
            .OrderBy(a => a.AttendanceDate).ThenBy(a => a.Employee.EmployeeCode)
            .Select(a => new object?[]
            {
                a.AttendanceDate, a.Employee.EmployeeCode,
                a.Employee.FirstName + " " + a.Employee.LastName,
                a.Employee.Department.Name,
                a.CheckIn, a.CheckOut,
                a.Status.ToString(), a.IsLate, a.LateMinutes,
                a.IsEarlyLeave, a.EarlyLeaveMinutes,
                a.GrossHours, a.WorkingHours, a.OvertimeMinutes,
                a.IsManual, a.Remarks
            })
            .ToListAsync();

        return CsvWriter.Build(
            ["Date", "Employee Code", "Employee", "Department", "Check In", "Check Out",
             "Status", "Is Late", "Late Minutes", "Early Leave", "Early Leave Minutes",
             "Gross Hours", "Working Hours", "Overtime Minutes", "Manually Corrected", "Remarks"],
            rows);
    }

    private async Task<string> LeaveCsv(DateTime from, DateTime to)
    {
        var rows = await _db.LeaveRequests
            .Include(l => l.Employee).Include(l => l.LeaveType)
            .Where(l => l.FromDate <= to && l.ToDate >= from)
            .OrderByDescending(l => l.FromDate)
            .Select(l => new object?[]
            {
                l.Employee.EmployeeCode, l.Employee.FirstName + " " + l.Employee.LastName,
                l.LeaveType.Name, l.FromDate, l.ToDate, l.TotalDays,
                l.Status.ToString(), l.Reason, l.CreatedAt
            })
            .ToListAsync();

        return CsvWriter.Build(
            ["Employee Code", "Employee", "Leave Type", "From", "To", "Days",
             "Status", "Reason", "Applied On"],
            rows);
    }

    private async Task<string> OvertimeCsv(DateTime from, DateTime to)
    {
        var rows = await _db.OvertimeRecords
            .Include(o => o.Employee).ThenInclude(e => e.Department)
            .Where(o => o.OvertimeDate >= from && o.OvertimeDate <= to)
            .OrderBy(o => o.OvertimeDate).ThenBy(o => o.Employee.EmployeeCode)
            .Select(o => new object?[]
            {
                o.OvertimeDate, o.Employee.EmployeeCode,
                o.Employee.FirstName + " " + o.Employee.LastName,
                o.Employee.Department.Name,
                o.DayType.ToString(), o.RawMinutes, o.ClaimedMinutes, o.ApprovedMinutes,
                o.RateMultiplier, o.RuleName, o.Status.ToString(),
                o.ApprovedAt, o.Remarks, o.RejectionReason
            })
            .ToListAsync();

        return CsvWriter.Build(
            ["Date", "Employee Code", "Employee", "Department", "Day Type",
             "Raw Minutes", "Claimed Minutes", "Approved Minutes",
             "Rate Multiplier", "Rule", "Status", "Approved At", "Remarks", "Rejection Reason"],
            rows);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Backup
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<Result<ExportFileDto>> CreateBackupAsync()
    {
        try
        {
            using var ms = new MemoryStream();
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                var manifest = new StringBuilder();
                manifest.AppendLine("# Attendance Management System — backup archive");
                manifest.AppendLine($"createdUtc={DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}");
                manifest.AppendLine($"version={AttendanceSystem.Common.Constants.AppConstants.AppVersion}");
                manifest.AppendLine("format=csv-per-table");

                foreach (var table in TableOrder)
                {
                    var csv = await DumpTableAsync(table);
                    if (csv == null) continue;

                    var entry = zip.CreateEntry($"data/{table}.csv", CompressionLevel.Optimal);
                    await using var w = new StreamWriter(entry.Open(), new UTF8Encoding(true));
                    await w.WriteAsync(csv.Value.Csv);

                    manifest.AppendLine($"table.{table}={csv.Value.Rows}");
                }

                var mEntry = zip.CreateEntry("manifest.txt", CompressionLevel.Optimal);
                await using var mw = new StreamWriter(mEntry.Open(), new UTF8Encoding(false));
                await mw.WriteAsync(manifest.ToString());
            }

            var name = $"attendance-backup-{DateTime.Now:yyyyMMdd-HHmm}.zip";
            return Result<ExportFileDto>.Success(new ExportFileDto(ms.ToArray(), name, "application/zip"));
        }
        catch (Exception ex)
        {
            AppLogger.Error("DataTransferService.CreateBackupAsync", ex);
            return Result<ExportFileDto>.Failure("Backup failed. See the log for details.");
        }
    }

    /// <summary>
    /// Dumps one table generically from EF's model, so a new entity is included in backups
    /// without anyone remembering to add it here — only its name in <see cref="TableOrder"/>.
    /// </summary>
    private async Task<(string Csv, int Rows)?> DumpTableAsync(string table)
    {
        var entityType = _db.Model.GetEntityTypes()
            .FirstOrDefault(t => string.Equals(GetTableName(t), table, StringComparison.OrdinalIgnoreCase));
        if (entityType == null) return null;

        var props = entityType.GetProperties()
            .Where(p => !p.IsShadowProperty())
            .ToList();
        if (props.Count == 0) return null;

        var clrType = entityType.ClrType;
        var set = (IQueryable<object>)typeof(DbContext)
            .GetMethod(nameof(DbContext.Set), 1, Type.EmptyTypes)!
            .MakeGenericMethod(clrType)
            .Invoke(_db, null)!;

        // IgnoreQueryFilters: a backup that silently omitted soft-deleted rows would restore
        // as a different database, and would resurrect ids that are still referenced.
        var items = await EntityFrameworkQueryableExtensions.ToListAsync(
            ((IQueryable<object>)IgnoreFilters(set, clrType)));

        var header = props.Select(p => p.Name).ToList();
        var rows = items.Select(item => props.Select(p => p.PropertyInfo?.GetValue(item)).ToArray().AsEnumerable());

        return (CsvWriter.Build(header, rows), items.Count);
    }

    // Resolved once. EF 10 declares more than one IgnoreQueryFilters overload, so
    // GetMethod(name) throws AmbiguousMatchException — the single-parameter generic one
    // has to be picked explicitly.
    private static readonly System.Reflection.MethodInfo IgnoreQueryFiltersMethod =
        typeof(EntityFrameworkQueryableExtensions)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Single(m => m.Name == nameof(EntityFrameworkQueryableExtensions.IgnoreQueryFilters)
                      && m.IsGenericMethodDefinition
                      && m.GetParameters().Length == 1);

    private static IQueryable IgnoreFilters(IQueryable source, Type clrType) =>
        (IQueryable)IgnoreQueryFiltersMethod.MakeGenericMethod(clrType).Invoke(null, [source])!;

    private static string GetTableName(Microsoft.EntityFrameworkCore.Metadata.IReadOnlyEntityType t) =>
        t.GetTableName() ?? t.ClrType.Name;

    // ──────────────────────────────────────────────────────────────────────────
    // Restore
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<Result<RestorePreviewDto>> PreviewRestoreAsync(byte[] archive)
    {
        var preview = new RestorePreviewDto();

        try
        {
            using var ms = new MemoryStream(archive);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);

            var manifest = zip.GetEntry("manifest.txt");
            if (manifest == null)
            {
                preview.Errors.Add("This is not a backup archive — manifest.txt is missing.");
                return Result<RestorePreviewDto>.Success(preview);
            }

            using (var r = new StreamReader(manifest.Open()))
            {
                string? line;
                while ((line = await r.ReadLineAsync()) != null)
                {
                    if (line.StartsWith("createdUtc=")) preview.CreatedAtUtc = line[11..];
                    else if (line.StartsWith("version=")) preview.SourceVersion = line[8..];
                }
            }

            foreach (var entry in zip.Entries.Where(e => e.FullName.StartsWith("data/") && e.FullName.EndsWith(".csv")))
            {
                var table = Path.GetFileNameWithoutExtension(entry.FullName);
                var recognised = TableOrder.Contains(table, StringComparer.OrdinalIgnoreCase);

                int rowsInFile;
                using (var r = new StreamReader(entry.Open(), Encoding.UTF8))
                    rowsInFile = Math.Max(0, CsvWriter.Parse(await r.ReadToEndAsync()).Count - 1);

                preview.Tables.Add(new BackupTableInfoDto
                {
                    Table = table,
                    RowsInFile = rowsInFile,
                    RowsInDatabase = recognised ? await CountAsync(table) : 0,
                    Recognised = recognised
                });

                if (!recognised)
                    preview.Warnings.Add($"'{table}' is not a table this version knows about; it will be skipped.");
            }

            if (preview.Tables.Count == 0)
                preview.Errors.Add("The archive contains no table data.");

            preview.Warnings.Add(
                "Restoring replaces the contents of the selected tables. Take a backup first.");

            return Result<RestorePreviewDto>.Success(preview);
        }
        catch (InvalidDataException)
        {
            preview.Errors.Add("That file is not a readable ZIP archive.");
            return Result<RestorePreviewDto>.Success(preview);
        }
        catch (Exception ex)
        {
            AppLogger.Error("DataTransferService.PreviewRestoreAsync", ex);
            return Result<RestorePreviewDto>.Failure("Could not read the archive. See the log for details.");
        }
    }

    private async Task<int> CountAsync(string table)
    {
        var entityType = _db.Model.GetEntityTypes()
            .FirstOrDefault(t => string.Equals(GetTableName(t), table, StringComparison.OrdinalIgnoreCase));
        if (entityType == null) return 0;

        // Name comes from EF's model, never from the uploaded archive.
        var sql = $"SELECT COUNT(*) AS Value FROM [{GetTableName(entityType)}]";
#pragma warning disable EF1002 // identifier is from the EF model, not user input
        return await _db.Database.SqlQueryRaw<int>(sql).FirstAsync();
#pragma warning restore EF1002
    }

    public async Task<Result<RestoreResultDto>> RestoreAsync(byte[] archive, IEnumerable<string>? tables = null)
    {
        var wanted = tables?.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var result = new RestoreResultDto();

        // One transaction for the whole restore: a half-applied archive — employees replaced
        // but attendance not — is worse than no restore at all.
        await using var tx = await _db.Database.BeginTransactionAsync();

        try
        {
            using var ms = new MemoryStream(archive);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);

            // Children first when clearing, parents first when writing.
            var order = TableOrder.Where(t =>
                zip.GetEntry($"data/{t}.csv") != null &&
                (wanted == null || wanted.Count == 0 || wanted.Contains(t))).ToList();

            if (order.Count == 0)
                return Result<RestoreResultDto>.Failure("Nothing in the archive matched the tables selected.");

            foreach (var table in Enumerable.Reverse(order))
                await ClearTableAsync(table);

            foreach (var table in order)
            {
                var entry = zip.GetEntry($"data/{table}.csv")!;
                string content;
                using (var r = new StreamReader(entry.Open(), Encoding.UTF8)) content = await r.ReadToEndAsync();

                var written = await WriteTableAsync(table, content);
                result.Tables.Add(new BackupTableInfoDto { Table = table, RowsInFile = written, RowsInDatabase = written });
                result.TotalRowsWritten += written;
            }

            await tx.CommitAsync();
            result.Warnings.Add("Restore complete. Sign out and back in — cached permissions may be stale.");
            return Result<RestoreResultDto>.Success(result);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            AppLogger.Error("DataTransferService.RestoreAsync", ex);
            return Result<RestoreResultDto>.Failure(
                "Restore failed and nothing was changed — the whole operation was rolled back. " +
                ex.Message);
        }
    }

    /// <summary>
    /// Writes one table's rows with explicit identity insert, so ids in the archive survive.
    /// Foreign keys point at those ids; letting the database reassign them would silently
    /// re-attribute every attendance record to a different employee.
    ///
    /// Identifiers are never interpolated from the file. The table name comes from
    /// <see cref="TableOrder"/>, and every column is matched against the EF model for that
    /// entity — an uploaded archive is untrusted input, and a CSV header of
    /// <c>Id] ; DROP TABLE Users --</c> would otherwise be executed.
    /// </summary>
    private async Task<int> WriteTableAsync(string table, string csv)
    {
        var lines = CsvWriter.Parse(csv);
        if (lines.Count < 2) return 0;

        var known = ColumnsOf(table);
        if (known.Count == 0) return 0;

        // Positions of the columns we accept; anything else in the file is dropped.
        var accepted = new List<(int Index, string Column)>();
        for (int i = 0; i < lines[0].Count; i++)
        {
            if (known.TryGetValue(lines[0][i], out var canonical))
                accepted.Add((i, canonical));
        }
        if (accepted.Count == 0) return 0;

        var cols = string.Join(",", accepted.Select(a => $"[{a.Column}]"));
        var placeholders = string.Join(",", accepted.Select((_, i) => $"@p{i}"));
        var insert = $"INSERT INTO [{table}] ({cols}) VALUES ({placeholders})";
        var written = 0;

        await SetIdentityInsertAsync(table, true);
        try
        {
            foreach (var row in lines.Skip(1))
            {
                if (row.Count != lines[0].Count) continue;

                var ps = accepted.Select((a, i) => new Microsoft.Data.SqlClient.SqlParameter(
                    $"@p{i}", row[a.Index].Length == 0 ? DBNull.Value : row[a.Index])).ToArray();

#pragma warning disable EF1002 // identifiers validated above; values are parameters
                await _db.Database.ExecuteSqlRawAsync(insert, ps);
#pragma warning restore EF1002
                written++;
            }
        }
        finally
        {
            await SetIdentityInsertAsync(table, false);
        }

        return written;
    }

    /// <summary>Column names EF maps for a table, keyed case-insensitively.</summary>
    private Dictionary<string, string> ColumnsOf(string table)
    {
        var entityType = _db.Model.GetEntityTypes()
            .FirstOrDefault(t => string.Equals(GetTableName(t), table, StringComparison.OrdinalIgnoreCase));

        if (entityType == null) return new(StringComparer.OrdinalIgnoreCase);

        return entityType.GetProperties()
            .Where(p => !p.IsShadowProperty())
            .Select(p => p.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(n => n, n => n, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// IDENTITY_INSERT cannot be parameterised, so the name is checked against the allow-list
    /// immediately before use rather than trusted from the caller.
    /// </summary>
    private Task SetIdentityInsertAsync(string table, bool on)
    {
        if (!TableOrder.Contains(table, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Refusing to touch unknown table '{table}'.");

#pragma warning disable EF1002 // table name is from the fixed TableOrder allow-list
        return _db.Database.ExecuteSqlRawAsync($"SET IDENTITY_INSERT [{table}] {(on ? "ON" : "OFF")}");
#pragma warning restore EF1002
    }

    private Task ClearTableAsync(string table)
    {
        if (!TableOrder.Contains(table, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Refusing to clear unknown table '{table}'.");

#pragma warning disable EF1002 // table name is from the fixed TableOrder allow-list
        return _db.Database.ExecuteSqlRawAsync($"DELETE FROM [{table}]");
#pragma warning restore EF1002
    }
}
