using System.Data;
using System.Data.OleDb;
using System.Globalization;
using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Domain.Enums;
using AttendanceSystem.Domain.Interfaces;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using AttendanceSystem.Infrastructure.Data;

namespace AttendanceSystem.Infrastructure.Services;

/// <summary>
/// Imports attendance punch data from biometric devices.
/// Supports: MS Access .mdb/.accdb (direct), CSV, and Excel (.xlsx) files.
/// </summary>
public class BiometricImportService : IBiometricImportService
{
    private readonly AttendanceDbContext _context;

    public BiometricImportService(AttendanceDbContext context)
    {
        _context = context;
    }

    // ──────────────────────────────────────────────────────────────────
    // PUBLIC METHODS
    // ──────────────────────────────────────────────────────────────────

    public async Task<BiometricImportResultDto> ImportFromAccessFileAsync(
        string mdbFilePath, DateTime fromDate, DateTime toDate)
    {
        var punches = await ReadFromAccessAsync(mdbFilePath, fromDate, toDate);
        return await ProcessPunchesAsync(punches);
    }

    public async Task<BiometricImportResultDto> ImportFromFileAsync(
        string filePath, DateTime fromDate, DateTime toDate)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        List<BiometricPunchDto> punches = ext switch
        {
            ".xlsx" or ".xls" => ReadFromExcel(filePath),
            ".csv" or ".txt"  => ReadFromCsv(filePath),
            _ => throw new NotSupportedException($"File type '{ext}' is not supported.")
        };

        punches = punches
            .Where(p => p.PunchTime.Date >= fromDate.Date && p.PunchTime.Date <= toDate.Date)
            .ToList();

        return await ProcessPunchesAsync(punches);
    }

    public async Task<List<BiometricPunchDto>> PreviewFileAsync(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return await Task.FromResult(ext switch
        {
            ".xlsx" or ".xls" => ReadFromExcel(filePath),
            ".csv" or ".txt"  => ReadFromCsv(filePath),
            _ => throw new NotSupportedException($"File type '{ext}' is not supported.")
        });
    }

    public Task<List<BiometricPunchDto>> PreviewAccessFileAsync(
        string mdbFilePath, DateTime fromDate, DateTime toDate) =>
        ReadFromAccessAsync(mdbFilePath, fromDate, toDate);

    // ──────────────────────────────────────────────────────────────────
    // CORE PROCESSING
    // ──────────────────────────────────────────────────────────────────

    private async Task<BiometricImportResultDto> ProcessPunchesAsync(List<BiometricPunchDto> punches)
    {
        var result = new BiometricImportResultDto { TotalRead = punches.Count };

        // Load all employees with BiometricEnrollId into a lookup
        var employees = await _context.Employees
            .Where(e => e.IsActive && !e.IsDeleted && e.BiometricEnrollId != null)
            .ToListAsync();

        var enrollMap = employees.ToDictionary(e => e.BiometricEnrollId!.Value, e => e.Id);

        // Group punches by EnrollId + Date → first punch = CheckIn, last punch = CheckOut
        var grouped = punches
            .Where(p => enrollMap.ContainsKey(p.EnrollId))
            .GroupBy(p => new { p.EnrollId, p.PunchTime.Date })
            .ToList();

        foreach (var group in grouped)
        {
            try
            {
                if (!enrollMap.TryGetValue(group.Key.EnrollId, out int employeeId))
                {
                    result.Warnings.Add($"EnrollId {group.Key.EnrollId} not mapped to any employee.");
                    result.Skipped++;
                    continue;
                }

                var date = group.Key.Date;

                // Skip if already exists
                bool exists = await _context.AttendanceLogs.AnyAsync(
                    a => a.EmployeeId == employeeId && a.AttendanceDate == date && !a.IsDeleted);

                if (exists)
                {
                    result.Skipped++;
                    continue;
                }

                var sorted = group.OrderBy(p => p.PunchTime).ToList();
                var checkIn  = sorted.First().PunchTime;
                var checkOut = sorted.Count > 1 ? sorted.Last().PunchTime : (DateTime?)null;

                var log = new AttendanceLog
                {
                    EmployeeId      = employeeId,
                    AttendanceDate  = date,
                    CheckIn         = checkIn,
                    CheckOut        = checkOut,
                    Status          = AttendanceStatus.Present,
                    WorkingHours    = checkOut.HasValue
                                        ? (checkOut.Value - checkIn).TotalHours
                                        : null,
                    IsManual        = false,
                    IsLate          = false,
                    IsEarlyLeave    = false,
                    CreatedAt       = DateTime.Now
                };

                _context.AttendanceLogs.Add(log);
                result.Inserted++;
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Errors.Add($"EnrollId {group.Key.EnrollId} on {group.Key.Date:d}: {ex.Message}");
            }
        }

        // Warn about unmapped enrollIds
        var unmapped = punches
            .Select(p => p.EnrollId)
            .Distinct()
            .Where(id => !enrollMap.ContainsKey(id))
            .ToList();

        foreach (var id in unmapped)
            result.Warnings.Add($"EnrollId {id} has no matching employee (BiometricEnrollId not set).");

        await _context.SaveChangesAsync();
        return result;
    }

    // ──────────────────────────────────────────────────────────────────
    // READERS
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Reads punch records directly from a ZKTeco-style MS Access .mdb file.
    /// A punch table must contain both an enrollment ID and a punch date/time.
    /// </summary>
    private static async Task<List<BiometricPunchDto>> ReadFromAccessAsync(
        string mdbFilePath, DateTime fromDate, DateTime toDate)
    {
        var punches = new List<BiometricPunchDto>();

        // Try both Jet (32-bit) and ACE (64-bit) providers
        string[] providers = new[]
        {
            "Microsoft.ACE.OLEDB.16.0",
            "Microsoft.ACE.OLEDB.12.0",
            "Microsoft.Jet.OLEDB.4.0"
        };

        string? connStr = null;
        foreach (var provider in providers)
        {
            try
            {
                connStr = $"Provider={provider};Data Source={mdbFilePath};";
                // Quick open test
                using var testConn = new OleDbConnection(connStr);
                await testConn.OpenAsync();
                testConn.Close();
                break;
            }
            catch { connStr = null; }
        }

        if (connStr == null)
            throw new InvalidOperationException(
                "Cannot connect to the Access file. Install Microsoft Access Database Engine (64-bit) from Microsoft.");

        using var conn = new OleDbConnection(connStr);
        await conn.OpenAsync();

        // Discover table name — ZKTeco uses CHECKINOUT or Att_log
        var tables = conn.GetSchema("Tables");
        var tableNames = tables.AsEnumerable()
            .Where(row => string.Equals(row["TABLE_TYPE"]?.ToString(), "TABLE", StringComparison.OrdinalIgnoreCase))
            .Select(row => row["TABLE_NAME"]?.ToString())
            .Where(name => !string.IsNullOrWhiteSpace(name) && !name.StartsWith("MSys", StringComparison.OrdinalIgnoreCase))
            .Cast<string>()
            .ToList();

        // Never use Enroll as a fallback: it only stores employee/template data.
        string? tableName = tableNames.FirstOrDefault(name =>
            name.Equals("CHECKINOUT", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Att_log", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("AttendanceLogs", StringComparison.OrdinalIgnoreCase));

        // Some devices use a different table name, so identify it by its columns.
        tableName ??= tableNames.FirstOrDefault(name =>
        {
            var tableColumns = conn.GetSchema("Columns", new[] { null, null, name, null })
                .AsEnumerable()
                .Select(row => row["COLUMN_NAME"]?.ToString() ?? string.Empty)
                .ToList();

            return HasColumn(tableColumns, "USERID", "EnrollId", "EmpNo") &&
                   HasColumn(tableColumns, "CHECKTIME", "PunchTime", "DateTime");
        });

        if (tableName is null)
        {
            var availableTables = tableNames.Count == 0 ? "none" : string.Join(", ", tableNames);
            throw new InvalidOperationException(
                "No attendance-log table was found in the Access file. " +
                $"Available tables: {availableTables}. The 'Enroll' table contains biometric enrollment/template data, " +
                "not attendance punches, because it has no CHECKTIME (or equivalent) column.");
        }

        // Try to detect column names
        string userCol  = "USERID";
        string timeCol  = "CHECKTIME";
        string nameCol  = "";
        var cols = conn.GetSchema("Columns", new[] { null, null, tableName, null });
        var colNames = cols.AsEnumerable().Select(r => r["COLUMN_NAME"]?.ToString() ?? string.Empty).ToList();
        userCol = colNames.FirstOrDefault(c =>
            c.Equals("USERID", StringComparison.OrdinalIgnoreCase) ||
            c.Equals("EnrollId", StringComparison.OrdinalIgnoreCase) ||
            c.Equals("EmpNo", StringComparison.OrdinalIgnoreCase)) ?? userCol;
        timeCol = colNames.FirstOrDefault(c =>
            c.Equals("CHECKTIME", StringComparison.OrdinalIgnoreCase) ||
            c.Equals("PunchTime", StringComparison.OrdinalIgnoreCase) ||
            c.Equals("DateTime", StringComparison.OrdinalIgnoreCase)) ?? timeCol;
        nameCol = colNames.FirstOrDefault(c =>
            c.Equals("EmpName", StringComparison.OrdinalIgnoreCase) ||
            c.Equals("Name", StringComparison.OrdinalIgnoreCase)) ?? "";

        if (!colNames.Contains(userCol, StringComparer.OrdinalIgnoreCase) ||
            !colNames.Contains(timeCol, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Attendance table '{tableName}' must contain an enrollment ID and a punch date/time column.");
        }

        string nameSelect = nameCol.Length > 0 ? $", [{nameCol}]" : "";
        string sql = $"SELECT [{userCol}], [{timeCol}]{nameSelect} FROM [{tableName}] " +
                     $"WHERE [{timeCol}] >= ? AND [{timeCol}] < ?";

        using var cmd = new OleDbCommand(sql, conn);
        cmd.Parameters.AddWithValue("@fromDate", fromDate.Date);
        cmd.Parameters.AddWithValue("@toDateExclusive", toDate.Date.AddDays(1));
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            if (int.TryParse(reader[userCol]?.ToString(), out int enrollId) &&
                DateTime.TryParse(reader[timeCol]?.ToString(), out DateTime punchTime))
            {
                punches.Add(new BiometricPunchDto
                {
                    EnrollId  = enrollId,
                    PunchTime = punchTime,
                    EmpName   = nameCol.Length > 0 ? reader[nameCol]?.ToString() : null
                });
            }
        }

        return punches;
    }

    /// <summary>Reads all Enroll-table fields for display only; it never writes to the Access file.</summary>
    public async Task<DataTable> ReadEnrollTableAsync(string mdbFilePath)
    {
        string[] providers =
        [
            "Microsoft.ACE.OLEDB.16.0",
            "Microsoft.ACE.OLEDB.12.0",
            "Microsoft.Jet.OLEDB.4.0"
        ];

        OleDbConnection? connection = null;
        foreach (var provider in providers)
        {
            try
            {
                connection = new OleDbConnection($"Provider={provider};Data Source={mdbFilePath};");
                await connection.OpenAsync();
                break;
            }
            catch
            {
                connection?.Dispose();
                connection = null;
            }
        }

        if (connection is null)
            throw new InvalidOperationException(
                "Cannot connect to the Access file. Install Microsoft Access Database Engine (64-bit) from Microsoft.");

        using (connection)
        {
            var tableName = connection.GetSchema("Tables").AsEnumerable()
                .Where(row => string.Equals(row["TABLE_TYPE"]?.ToString(), "TABLE", StringComparison.OrdinalIgnoreCase))
                .Select(row => row["TABLE_NAME"]?.ToString())
                .FirstOrDefault(name => string.Equals(name, "Enroll", StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrWhiteSpace(tableName))
                throw new InvalidOperationException("The selected Access database does not contain an 'Enroll' table.");

            using var command = new OleDbCommand($"SELECT * FROM [{tableName}]", connection);
            using var reader = await command.ExecuteReaderAsync();
            var enrollments = new DataTable(tableName);
            enrollments.Load(reader);
            return enrollments;
        }
    }

    private static bool HasColumn(IEnumerable<string> columns, params string[] candidates) =>
        columns.Any(column => candidates.Any(candidate =>
            column.Equals(candidate, StringComparison.OrdinalIgnoreCase)));

    /// <summary>
    /// Reads punch records from an Excel file (.xlsx).
    /// Expected columns: EnrollId (or USERID), PunchTime (or CHECKTIME), EmpName (optional)
    /// </summary>
    private static List<BiometricPunchDto> ReadFromExcel(string filePath)
    {
        var punches = new List<BiometricPunchDto>();
        using var wb = new XLWorkbook(filePath);
        var ws = wb.Worksheet(1);

        var headers = ws.Row(1).Cells()
            .Select((c, i) => new { Index = i + 1, Name = c.GetString().Trim().ToLowerInvariant() })
            .ToList();

        int enrollCol = headers.FirstOrDefault(h =>
            h.Name is "enrollid" or "userid" or "empno" or "id")?.Index ?? 2;
        int timeCol = headers.FirstOrDefault(h =>
            h.Name is "checktime" or "punchtime" or "datetime" or "time")?.Index ?? -1;
        int nameCol = headers.FirstOrDefault(h =>
            h.Name is "empname" or "name" or "employeename")?.Index ?? -1;

        // If no header row detected, use column positions from the data sample provided
        // Col order from sample: Id, DeviceId, EnrollId(3), EmpName(4), FingerNumber(5), CardNo(6), Password, Period, PeriodStDate, PeriodEndDate
        bool hasHeader = headers.Any(h => h.Name.Length > 0);
        if (!hasHeader)
        {
            enrollCol = 3; timeCol = -1; nameCol = 4;
        }

        int lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
        for (int r = hasHeader ? 2 : 1; r <= lastRow; r++)
        {
            var row = ws.Row(r);
            if (!int.TryParse(row.Cell(enrollCol).GetString(), out int enrollId)) continue;

            // For enroll-only files (no punch time), use today as placeholder — user should supply date range via direct connect
            DateTime punchTime = DateTime.Today;
            if (timeCol > 0)
            {
                var cell = row.Cell(timeCol);
                if (cell.DataType == XLDataType.DateTime)
                    punchTime = cell.GetDateTime();
                else
                    DateTime.TryParse(cell.GetString(), out punchTime);
            }

            punches.Add(new BiometricPunchDto
            {
                EnrollId  = enrollId,
                PunchTime = punchTime,
                EmpName   = nameCol > 0 ? row.Cell(nameCol).GetString() : null
            });
        }

        return punches;
    }

    /// <summary>
    /// Reads punch records from a CSV / TXT file.
    /// Auto-detects delimiter (tab or comma). Expects a header row.
    /// </summary>
    private static List<BiometricPunchDto> ReadFromCsv(string filePath)
    {
        var punches = new List<BiometricPunchDto>();
        var lines   = File.ReadAllLines(filePath);
        if (lines.Length < 2) return punches;

        // Detect delimiter
        char delim = lines[0].Contains('\t') ? '\t' : ',';
        var headers = lines[0].Split(delim).Select(h => h.Trim().ToLowerInvariant()).ToArray();

        int enrollIdx = IndexOf(headers, "enrollid", "userid", "empno");
        int timeIdx   = IndexOf(headers, "checktime", "punchtime", "datetime", "time");
        int nameIdx   = IndexOf(headers, "empname", "name", "employeename");

        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            var cols = lines[i].Split(delim);

            if (enrollIdx < 0 || enrollIdx >= cols.Length) continue;
            if (!int.TryParse(cols[enrollIdx].Trim(), out int enrollId)) continue;

            DateTime punchTime = DateTime.Today;
            if (timeIdx >= 0 && timeIdx < cols.Length)
                DateTime.TryParse(cols[timeIdx].Trim(), out punchTime);

            punches.Add(new BiometricPunchDto
            {
                EnrollId  = enrollId,
                PunchTime = punchTime,
                EmpName   = nameIdx >= 0 && nameIdx < cols.Length ? cols[nameIdx].Trim() : null
            });
        }

        return punches;
    }

    private static int IndexOf(string[] headers, params string[] candidates)
    {
        foreach (var c in candidates)
        {
            int idx = Array.IndexOf(headers, c);
            if (idx >= 0) return idx;
        }
        return -1;
    }
}
