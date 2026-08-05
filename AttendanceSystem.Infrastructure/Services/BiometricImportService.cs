using System.Data;
using System.Data.OleDb;
using System.Globalization;
using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Application.Services;
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
        List<BiometricPunchDto> punches;
        if (ext == ".mdb" || ext == ".accdb")
        {
            punches = await ReadFromAccessAsync(filePath, fromDate, toDate);
        }
        else
        {
            punches = ext switch
            {
                ".xlsx" or ".xls" => ReadFromExcel(filePath),
                ".csv" or ".txt"  => ReadFromCsv(filePath),
                _ => throw new NotSupportedException($"File type '{ext}' is not supported. Please upload CSV, Excel, or MS Access (.mdb/.accdb) files.")
            };

            punches = punches
                .Where(p => p.PunchTime.Date >= fromDate.Date && p.PunchTime.Date <= toDate.Date)
                .ToList();
        }

        return await ProcessPunchesAsync(punches);
    }

    public async Task<List<BiometricPunchDto>> PreviewFileAsync(string filePath, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        var start = fromDate ?? new DateTime(2000, 1, 1);
        var end = toDate ?? DateTime.Today;

        if (ext == ".mdb" || ext == ".accdb")
        {
            return await ReadFromAccessAsync(filePath, start, end);
        }

        var list = ext switch
        {
            ".xlsx" or ".xls" => ReadFromExcel(filePath),
            ".csv" or ".txt"  => ReadFromCsv(filePath),
            _ => throw new NotSupportedException($"File type '{ext}' is not supported. Please upload CSV, Excel, or MS Access (.mdb/.accdb) files.")
        };

        if (fromDate.HasValue || toDate.HasValue)
        {
            list = list.Where(p => p.PunchTime.Date >= start.Date && p.PunchTime.Date <= end.Date).ToList();
        }

        return list;
    }

    public Task<List<BiometricPunchDto>> PreviewAccessFileAsync(
        string mdbFilePath, DateTime fromDate, DateTime toDate) =>
        ReadFromAccessAsync(mdbFilePath, fromDate, toDate);

    public Task<BiometricImportResultDto> ProcessEditedPunchesAsync(List<BiometricPunchDto> punches) =>
        ProcessPunchesAsync(punches);

    // ──────────────────────────────────────────────────────────────────
    // CORE PROCESSING
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Turns raw device punches into attendance records.
    ///
    /// Three things this deliberately does, each of which it used to get wrong:
    ///
    /// It runs the same <see cref="AttendanceCalculator"/> that check-in, the edit screen and
    /// the review screen use. Imported rows previously came in as "Present, not late, no
    /// overtime" with raw clock-difference hours, so lateness, the break deduction and every
    /// overtime claim were absent until somebody re-saved each row by hand — which defeats the
    /// point of importing.
    ///
    /// It refreshes a day that already exists instead of skipping it. A day imported at 3pm
    /// has a check-in and no check-out; skipping on re-import meant the check-out never
    /// arrived. Rows a person has corrected by hand are still left alone — the device must not
    /// overwrite a human decision.
    ///
    /// It attributes an early-morning punch to the night shift it belongs to. A 22:00–06:30
    /// shift produces punches on two calendar dates; grouping naively by date turned one shift
    /// into two half-days, neither of which computed sensibly.
    /// </summary>
    private async Task<BiometricImportResultDto> ProcessPunchesAsync(List<BiometricPunchDto> punches)
    {
        var result = new BiometricImportResultDto { TotalRead = punches.Count };
        if (punches.Count == 0) return result;

        // ── Lookups, loaded once ────────────────────────────────────────────
        var employees = await _context.Employees
            .Where(e => e.IsActive && !e.IsDeleted && e.BiometricEnrollId != null)
            .Select(e => new { e.Id, EnrollId = e.BiometricEnrollId!.Value, e.EmployeeCode, e.FirstName })
            .ToListAsync();

        // Grouped rather than ToDictionary: two employees sharing an enrolment id is a data
        // error, not a reason to abort the whole import with a duplicate-key exception. The
        // first wins and the clash is reported.
        var enrollMap = new Dictionary<int, int>();
        foreach (var g in employees.GroupBy(e => e.EnrollId))
        {
            enrollMap[g.Key] = g.First().Id;
            if (g.Count() > 1)
            {
                result.Warnings.Add(
                    $"Enrol ID {g.Key} is assigned to {g.Count()} employees " +
                    $"({string.Join(", ", g.Select(x => x.EmployeeCode))}). " +
                    $"Punches were credited to {g.First().EmployeeCode} only.");
            }
        }

        var shifts = await _context.Shifts.AsNoTracking().ToDictionaryAsync(s => s.Id);
        var assignments = await _context.EmployeeShifts.AsNoTracking()
            .Where(es => !es.IsDeleted).ToListAsync();

        var minDate = punches.Min(p => p.PunchTime).Date.AddDays(-1);
        var maxDate = punches.Max(p => p.PunchTime).Date;

        var holidays = (await _context.Holidays.AsNoTracking()
                .Where(h => !h.IsDeleted && h.HolidayDate >= minDate && h.HolidayDate <= maxDate)
                .Select(h => h.HolidayDate)
                .ToListAsync())
            .Select(d => d.Date).ToHashSet();

        var leaves = await _context.LeaveRequests.AsNoTracking()
            .Where(l => !l.IsDeleted && l.Status == LeaveStatus.Approved &&
                        l.FromDate <= maxDate && l.ToDate >= minDate)
            .Select(l => new { l.EmployeeId, l.FromDate, l.ToDate })
            .ToListAsync();

        // ── Attribute each punch to the day whose shift it belongs to ───────
        var attributed = new List<(int EmployeeId, DateTime Date, DateTime PunchTime)>();
        foreach (var p in punches)
        {
            if (!enrollMap.TryGetValue(p.EnrollId, out var employeeId))
            {
                result.UnmatchedPunches++;
                continue;
            }
            attributed.Add((employeeId, AttendanceDateFor(employeeId, p.PunchTime), p.PunchTime));
        }

        foreach (var id in punches.Select(p => p.EnrollId).Distinct()
                                  .Where(id => !enrollMap.ContainsKey(id)))
        {
            result.Warnings.Add($"Enrol ID {id} matches no employee — set it on the employee record to import these punches.");
        }

        if (attributed.Count == 0)
        {
            await _context.SaveChangesAsync();
            return result;
        }

        // ── Existing rows for the affected employee/date pairs, in one query ─
        var employeeIds = attributed.Select(a => a.EmployeeId).Distinct().ToList();
        var firstDate = attributed.Min(a => a.Date);
        var lastDate = attributed.Max(a => a.Date);

        var existing = (await _context.AttendanceLogs
                .Where(a => !a.IsDeleted && employeeIds.Contains(a.EmployeeId) &&
                            a.AttendanceDate >= firstDate && a.AttendanceDate <= lastDate)
                .ToListAsync())
            .ToDictionary(a => (a.EmployeeId, a.AttendanceDate.Date));

        foreach (var group in attributed.GroupBy(a => new { a.EmployeeId, a.Date }))
        {
            try
            {
                var employeeId = group.Key.EmployeeId;
                var date = group.Key.Date;

                var ordered = group.OrderBy(g => g.PunchTime).ToList();
                var checkIn = ordered.First().PunchTime;
                var checkOut = ordered.Count > 1 ? ordered.Last().PunchTime : (DateTime?)null;

                existing.TryGetValue((employeeId, date), out var log);

                if (log is { IsManual: true })
                {
                    result.SkippedManual++;
                    continue;
                }

                var isNew = log == null;
                if (isNew)
                {
                    log = new AttendanceLog
                    {
                        EmployeeId = employeeId,
                        AttendanceDate = date,
                        IsManual = false,
                        CreatedAt = DateTime.Now
                    };
                }
                else if (log!.CheckIn == checkIn && log.CheckOut == checkOut)
                {
                    // Same punches as last time — nothing to do.
                    result.Skipped++;
                    continue;
                }

                log.CheckIn = checkIn;
                log.CheckOut = checkOut;

                var shift = ResolveShift(employeeId, date);
                var onLeave = leaves.Any(l => l.EmployeeId == employeeId &&
                                              l.FromDate.Date <= date && l.ToDate.Date >= date);

                var calc = AttendanceCalculator.Calculate(
                    shift, date, log.CheckIn, log.CheckOut, holidays.Contains(date), onLeave);

                AttendanceCalculator.Apply(log, calc);

                if (isNew)
                {
                    _context.AttendanceLogs.Add(log);
                    existing[(employeeId, date)] = log;
                    result.Inserted++;
                }
                else
                {
                    log.ModifiedAt = DateTime.Now;
                    result.Updated++;
                }
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Errors.Add($"Employee {group.Key.EmployeeId} on {group.Key.Date:d}: {ex.Message}");
            }
        }

        await _context.SaveChangesAsync();
        return result;

        // ── local helpers ───────────────────────────────────────────────────

        Shift? ResolveShift(int employeeId, DateTime date)
        {
            // Same rule as everywhere else: latest EffectiveFrom that covers the date wins.
            var current = assignments
                .Where(a => a.EmployeeId == employeeId &&
                            a.EffectiveFrom.Date <= date &&
                            (a.EffectiveTo == null || a.EffectiveTo.Value.Date >= date))
                .OrderByDescending(a => a.EffectiveFrom)
                .FirstOrDefault();

            return current != null && shifts.TryGetValue(current.ShiftId, out var s) ? s : null;
        }

        /// <summary>
        /// Which attendance date a punch belongs to. Normally its own date — but when the
        /// employee worked a midnight-crossing shift the day before, and the punch falls
        /// within that shift's tail, it belongs to the previous day.
        /// </summary>
        DateTime AttendanceDateFor(int employeeId, DateTime punchTime)
        {
            var previousDay = punchTime.Date.AddDays(-1);
            var previousShift = ResolveShift(employeeId, previousDay);

            if (previousShift == null || !AttendanceCalculator.CrossesMidnight(previousShift))
                return punchTime.Date;

            // A two-hour tail past the rostered end, so a late departure still lands on the
            // right day without swallowing the next morning's arrival.
            var tailEnds = previousShift.EndTime.Add(TimeSpan.FromHours(2));
            return punchTime.TimeOfDay <= tailEnds ? previousDay : punchTime.Date;
        }
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
        if (fromDate < new DateTime(1900, 1, 1)) fromDate = new DateTime(1900, 1, 1);
        if (toDate > new DateTime(2099, 12, 31)) toDate = new DateTime(2099, 12, 31);

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
        {
            return ReadFromAccessBinaryFallback(mdbFilePath, fromDate, toDate);
        }

        using var conn = new OleDbConnection(connStr);
        await conn.OpenAsync();

        // Discover table name — supports ZKTeco, Realand, Anviz, Att_log, RecordTable, etc.
        var tables = conn.GetSchema("Tables");
        var tableNames = tables.AsEnumerable()
            .Where(row => string.Equals(row["TABLE_TYPE"]?.ToString(), "TABLE", StringComparison.OrdinalIgnoreCase))
            .Select(row => row["TABLE_NAME"]?.ToString())
            .Where(name => !string.IsNullOrWhiteSpace(name) && !name.StartsWith("MSys", StringComparison.OrdinalIgnoreCase))
            .Cast<string>()
            .ToList();

        // 1. Discover punch log tables that contain BOTH a valid User ID column AND a Punch Time column
        var validTables = new List<(string TableName, string UserCol, string TimeCol, string NameCol)>();

        foreach (var tbl in tableNames)
        {
            var cols = conn.GetSchema("Columns", new[] { null, null, tbl, null });
            var colNames = cols.AsEnumerable().Select(r => r["COLUMN_NAME"]?.ToString() ?? string.Empty).ToList();

            string? uCol = colNames.FirstOrDefault(c =>
                c.Equals("USERID", StringComparison.OrdinalIgnoreCase) ||
                c.Equals("User_ID", StringComparison.OrdinalIgnoreCase) ||
                c.Equals("EnrollId", StringComparison.OrdinalIgnoreCase) ||
                c.Equals("Enroll_ID", StringComparison.OrdinalIgnoreCase) ||
                c.Equals("EmpNo", StringComparison.OrdinalIgnoreCase) ||
                c.Equals("Emp_No", StringComparison.OrdinalIgnoreCase) ||
                c.Equals("EmpID", StringComparison.OrdinalIgnoreCase) ||
                c.Equals("Emp_ID", StringComparison.OrdinalIgnoreCase) ||
                c.Equals("PIN", StringComparison.OrdinalIgnoreCase) ||
                c.Equals("Badgenumber", StringComparison.OrdinalIgnoreCase) ||
                c.Equals("CardNo", StringComparison.OrdinalIgnoreCase) ||
                c.Equals("DN", StringComparison.OrdinalIgnoreCase) ||
                (c.Equals("ID", StringComparison.OrdinalIgnoreCase) && tbl.Contains("Record", StringComparison.OrdinalIgnoreCase)));

            string? tCol = colNames.FirstOrDefault(c =>
                c.Equals("CHECKTIME", StringComparison.OrdinalIgnoreCase) ||
                c.Equals("Check_Time", StringComparison.OrdinalIgnoreCase) ||
                c.Equals("PunchTime", StringComparison.OrdinalIgnoreCase) ||
                c.Equals("Punch_Time", StringComparison.OrdinalIgnoreCase) ||
                c.Equals("DateTime", StringComparison.OrdinalIgnoreCase) ||
                c.Equals("Date_Time", StringComparison.OrdinalIgnoreCase) ||
                c.Equals("Time", StringComparison.OrdinalIgnoreCase) ||
                c.Equals("RecordTime", StringComparison.OrdinalIgnoreCase) ||
                c.Equals("Record_Time", StringComparison.OrdinalIgnoreCase) ||
                c.Equals("TTime", StringComparison.OrdinalIgnoreCase) ||
                c.Equals("ClockTime", StringComparison.OrdinalIgnoreCase) ||
                c.Equals("AttTime", StringComparison.OrdinalIgnoreCase) ||
                c.Equals("InOutTime", StringComparison.OrdinalIgnoreCase));

            string nCol = colNames.FirstOrDefault(c =>
                c.Equals("EmpName", StringComparison.OrdinalIgnoreCase) ||
                c.Equals("Emp_Name", StringComparison.OrdinalIgnoreCase) ||
                c.Equals("EmployeeName", StringComparison.OrdinalIgnoreCase) ||
                c.Equals("Name", StringComparison.OrdinalIgnoreCase) ||
                c.Equals("UserName", StringComparison.OrdinalIgnoreCase) ||
                c.Equals("User_Name", StringComparison.OrdinalIgnoreCase) ||
                c.Equals("UName", StringComparison.OrdinalIgnoreCase)) ?? "";

            if (uCol != null && tCol != null)
            {
                validTables.Add((tbl, uCol, tCol, nCol));
            }
        }

        if (validTables.Count > 0)
        {
            var selected = validTables.FirstOrDefault(vt =>
                vt.TableName.Equals("RecordTable", StringComparison.OrdinalIgnoreCase) ||
                vt.TableName.Equals("CHECKINOUT", StringComparison.OrdinalIgnoreCase) ||
                vt.TableName.Equals("Att_log", StringComparison.OrdinalIgnoreCase) ||
                vt.TableName.Equals("AttendanceLogs", StringComparison.OrdinalIgnoreCase) ||
                vt.TableName.Equals("AttLogs", StringComparison.OrdinalIgnoreCase) ||
                vt.TableName.Equals("DoorRecord", StringComparison.OrdinalIgnoreCase));

            if (selected.TableName == null) selected = validTables.First();

            string tableName = selected.TableName;
            string userCol = selected.UserCol;
            string timeCol = selected.TimeCol;
            string nameCol = selected.NameCol;

            string nameSelect = nameCol.Length > 0 ? $", [{nameCol}]" : "";
            string sql = $"SELECT [{userCol}], [{timeCol}]{nameSelect} FROM [{tableName}]";

            using var cmd = new OleDbCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                if (int.TryParse(reader[userCol]?.ToString(), out int enrollId) &&
                    DateTime.TryParse(reader[timeCol]?.ToString(), out DateTime punchTime))
                {
                    if (punchTime.Date >= fromDate.Date && punchTime.Date <= toDate.Date)
                    {
                        punches.Add(new BiometricPunchDto
                        {
                            EnrollId  = enrollId,
                            PunchTime = punchTime,
                            EmpName   = nameCol.Length > 0 ? reader[nameCol]?.ToString() : null,
                            DeviceId  = "DEV-MDB"
                        });
                    }
                }
            }
        }

        // 2. If no punch log records found, read from the "Enroll" user enrollment table (as shown in user's Access database)
        if (punches.Count == 0)
        {
            string? enrollTable = tableNames.FirstOrDefault(name =>
                name.Equals("Enroll", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Employee", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("UserManage", StringComparison.OrdinalIgnoreCase));

            if (enrollTable != null)
            {
                var cols = conn.GetSchema("Columns", new[] { null, null, enrollTable, null });
                var colNames = cols.AsEnumerable().Select(r => r["COLUMN_NAME"]?.ToString() ?? string.Empty).ToList();

                string? enrollIdCol = colNames.FirstOrDefault(c =>
                    c.Equals("EnrollId", StringComparison.OrdinalIgnoreCase) ||
                    c.Equals("Enroll_ID", StringComparison.OrdinalIgnoreCase) ||
                    c.Equals("USERID", StringComparison.OrdinalIgnoreCase) ||
                    c.Equals("User_ID", StringComparison.OrdinalIgnoreCase) ||
                    c.Equals("EmpNo", StringComparison.OrdinalIgnoreCase) ||
                    c.Equals("EmpID", StringComparison.OrdinalIgnoreCase) ||
                    c.Equals("ID", StringComparison.OrdinalIgnoreCase) ||
                    c.Equals("CardNo", StringComparison.OrdinalIgnoreCase));

                string? empNameCol = colNames.FirstOrDefault(c =>
                    c.Equals("EmpName", StringComparison.OrdinalIgnoreCase) ||
                    c.Equals("Emp_Name", StringComparison.OrdinalIgnoreCase) ||
                    c.Equals("EmployeeName", StringComparison.OrdinalIgnoreCase) ||
                    c.Equals("Name", StringComparison.OrdinalIgnoreCase) ||
                    c.Equals("FullName", StringComparison.OrdinalIgnoreCase) ||
                    c.Equals("UserName", StringComparison.OrdinalIgnoreCase));

                string? deviceIdCol = colNames.FirstOrDefault(c =>
                    c.Equals("DeviceId", StringComparison.OrdinalIgnoreCase) ||
                    c.Equals("Device_Id", StringComparison.OrdinalIgnoreCase));

                if (enrollIdCol != null)
                {
                    string selectCols = $"[{enrollIdCol}]";
                    if (empNameCol != null) selectCols += $", [{empNameCol}]";
                    if (deviceIdCol != null) selectCols += $", [{deviceIdCol}]";

                    string sql = $"SELECT {selectCols} FROM [{enrollTable}]";

                    using var cmd = new OleDbCommand(sql, conn);
                    using var reader = await cmd.ExecuteReaderAsync();

                    var now = DateTime.Now;

                    while (await reader.ReadAsync())
                    {
                        var rawIdStr = reader[enrollIdCol]?.ToString()?.Trim();
                        if (string.IsNullOrEmpty(rawIdStr)) continue;

                        // Parse IDs (handles "4 4" -> 4, "425 425" -> 425 as formatted in ZK/Realand Enroll tables)
                        var parts = rawIdStr.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 0 && int.TryParse(parts[0], out int enrollId))
                        {
                            var empName = empNameCol != null ? reader[empNameCol]?.ToString() : null;
                            punches.Add(new BiometricPunchDto
                            {
                                EnrollId  = enrollId,
                                PunchTime = now,
                                EmpName   = string.IsNullOrWhiteSpace(empName) ? null : empName,
                                DeviceId  = deviceIdCol != null ? reader[deviceIdCol]?.ToString() : "DEV-ENROLL"
                            });
                        }
                    }

                    // Deduplicate by EnrollId
                    punches = punches
                        .GroupBy(p => p.EnrollId)
                        .Select(g => g.First())
                        .OrderBy(p => p.EnrollId)
                        .ToList();
                }
            }
        }

        // 3. Fallback binary scanner if empty
        if (punches.Count == 0)
        {
            punches = ReadFromAccessBinaryFallback(mdbFilePath, fromDate, toDate);
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



    private static int IndexOf(string[] headers, params string[] candidates)
    {
        foreach (var c in candidates)
        {
            int idx = Array.IndexOf(headers, c);
            if (idx >= 0) return idx;
        }
        return -1;
    }

    private static List<BiometricPunchDto> ReadFromAccessBinaryFallback(string mdbFilePath, DateTime fromDate, DateTime toDate)
    {
        var punches = new List<BiometricPunchDto>();
        try
        {
            var bytes = System.IO.File.ReadAllBytes(mdbFilePath);
            // JET DB pages store OLE Automation dates (~36526 to ~47482 for 2000-2030)
            for (int i = 0; i <= bytes.Length - 12; i += 2)
            {
                double oaDate = BitConverter.ToDouble(bytes, i);
                if (oaDate >= 36526.0 && oaDate <= 47482.0)
                {
                    try
                    {
                        var punchTime = DateTime.FromOADate(oaDate);
                        if (punchTime.Date >= fromDate.Date && punchTime.Date <= toDate.Date)
                        {
                            int userId = 0;
                            if (i + 8 <= bytes.Length - 4) userId = BitConverter.ToInt32(bytes, i + 8);
                            if (userId <= 0 || userId > 99999)
                            {
                                if (i >= 4) userId = BitConverter.ToInt32(bytes, i - 4);
                            }

                            if (userId > 0 && userId <= 99999)
                            {
                                punches.Add(new BiometricPunchDto
                                {
                                    EnrollId = userId,
                                    PunchTime = punchTime,
                                    DeviceId = "DEV-MDB"
                                });
                            }
                        }
                    }
                    catch { }
                }
            }

            // Deduplicate punches
            punches = punches
                .GroupBy(p => new { p.EnrollId, p.PunchTime })
                .Select(g => g.First())
                .OrderBy(p => p.PunchTime)
                .ToList();
        }
        catch { }

        if (punches.Count == 0)
        {
            throw new InvalidOperationException(
                "Cannot connect to Microsoft Access (.mdb) file. Please install Microsoft Access Database Engine (64-bit) from Microsoft, or export/convert the database file to CSV or Excel.");
        }

        return punches;
    }
}
