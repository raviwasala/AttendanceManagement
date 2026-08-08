using System.Globalization;
using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Common.Logging;
using AttendanceSystem.Common.Models;
using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Infrastructure.Data;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.Infrastructure.Services;

/// <summary>
/// Bulk creation and update of employee records from CSV or Excel.
///
/// Preview then apply, for the same reason the punch importer works that way: a file of 240
/// staff with one misspelled department should show that before anything is written, not fail
/// on row 137 with 136 employees already created and no way to tell which.
///
/// Departments, designations and branches are matched **by name**, because that is what the
/// people preparing these files have. An unmatched name is an error on that row, never a
/// silently created lookup — inventing a "Prodution" department from a typo is how a
/// duplicate org chart starts.
/// </summary>
public class EmployeeImportService : IEmployeeImportService
{
    private readonly AttendanceDbContext _db;

    public EmployeeImportService(AttendanceDbContext db) => _db = db;

    /// <summary>Shared with the employee export, so an export can be edited and re-imported.</summary>
    public static readonly string[] TemplateHeader =
    [
        "Employee Code", "User ID", "Full Name", "Last Name", "Name With Initials", "NIC",
        "Department", "Designation", "Branch",
        "Email", "Phone", "Gender", "Date Of Birth", "Joining Date",
        "Biometric Enroll ID", "Address", "Active",
        // Payroll columns are optional — a site running attendance only can delete them and
        // the file still imports. They are here because loading employees and then keying
        // salary, EPF number and bank account by hand afterwards is the same job done twice.
        "Grade Code", "Basic Salary", "EPF Number", "ETF Number", "Bank Branch Code", "Account Number"
    ];

    public Result<ExportFileDto> GetTemplate()
    {
        // One example row: a blank template leaves people guessing at date format, and the
        // date format is the single most common reason a bulk import fails.
        var example = new List<object?[]>
        {
            new object?[]
            {
                "", "513 T", "Kamal Perera", "Perera", "K Perera", "199912345678",
                "Production", "Machine Operator", "Head Office",
                "kamal@example.com", "0771234567", "Male", "1999-05-12", "2024-01-15",
                "1042", "12 Main Street, Colombo", "Yes",
                "G1", "45000.00", "A/12345", "A/12345", "001-7056", "1234567890"
            }
        };

        var csv = CsvWriter.Build(TemplateHeader, example);
        return Result<ExportFileDto>.Success(
            new ExportFileDto(CsvWriter.ToBytes(csv), "employee-import-template.csv", "text/csv"));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Preview
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<Result<EmployeeImportPreviewDto>> PreviewAsync(Stream file, string fileName)
    {
        try
        {
            var ext = Path.GetExtension(fileName).ToLowerInvariant();

            List<List<string>> lines = ext switch
            {
                ".csv" or ".txt" => ReadCsv(file),
                ".xlsx" or ".xls" => ReadExcel(file),
                _ => throw new NotSupportedException(
                        $"File type '{ext}' is not supported. Upload a CSV or Excel file.")
            };

            var preview = new EmployeeImportPreviewDto();

            if (lines.Count < 2)
            {
                preview.FileWarnings.Add("The file has a header but no data rows.");
                return Result<EmployeeImportPreviewDto>.Success(preview);
            }

            var map = MapColumns(lines[0], preview);

            // Loaded once rather than per row: 240 rows would otherwise be 720 lookups.
            var depts   = await _db.Departments.Where(d => !d.IsDeleted).ToDictionaryAsync(d => d.Name.Trim(), d => d.Id, StringComparer.OrdinalIgnoreCase);
            var desigs  = await _db.Designations.Where(d => !d.IsDeleted).ToDictionaryAsync(d => d.Name.Trim(), d => d.Id, StringComparer.OrdinalIgnoreCase);
            var branches= await _db.Branches.Where(b => !b.IsDeleted).ToDictionaryAsync(b => b.Name.Trim(), b => b.Id, StringComparer.OrdinalIgnoreCase);

            var grades = await _db.SalaryGrades.Where(g => !g.IsDeleted)
                .ToDictionaryAsync(g => g.Code.Trim(), g => g.Id, StringComparer.OrdinalIgnoreCase);

            // Keyed on the combined bank-and-branch code, which is what a bank transfer file
            // uses and therefore what a payroll export from another system will carry.
            var bankBranches = (await _db.BankBranches.Where(b => !b.IsDeleted)
                    .Include(b => b.Bank).ToListAsync())
                .GroupBy(b => $"{b.Bank!.Code}-{b.Code}".Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

            var existing = await _db.Employees.Where(e => !e.IsDeleted)
                .Select(e => new { e.Id, e.EmployeeCode, e.BiometricEnrollId })
                .ToListAsync();

            var byCode   = existing.Where(e => !string.IsNullOrWhiteSpace(e.EmployeeCode))
                                   .ToDictionary(e => e.EmployeeCode.Trim(), e => e.Id, StringComparer.OrdinalIgnoreCase);
            var byEnroll = existing.Where(e => e.BiometricEnrollId.HasValue)
                                   .GroupBy(e => e.BiometricEnrollId!.Value)
                                   .ToDictionary(g => g.Key, g => g.First().Id);

            var unknown = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenEnroll = new HashSet<int>();

            for (int i = 1; i < lines.Count; i++)
            {
                var row = ParseRow(lines[i], map, i + 1);

                ResolveLookups(row, depts, desigs, branches, unknown);
                ResolvePayrollLookups(row, grades, bankBranches, unknown);
                MatchExisting(row, byCode, byEnroll);
                Validate(row, seenCodes, seenEnroll, byEnroll);

                preview.Rows.Add(row);
            }

            preview.TotalRead = preview.Rows.Count;
            preview.ToCreate  = preview.Rows.Count(r => r.IsValid && !r.ExistingEmployeeId.HasValue);
            preview.ToUpdate  = preview.Rows.Count(r => r.IsValid && r.ExistingEmployeeId.HasValue);
            preview.Invalid   = preview.Rows.Count(r => !r.IsValid);
            preview.UnknownLookups = unknown.OrderBy(x => x).ToList();

            return Result<EmployeeImportPreviewDto>.Success(preview);
        }
        catch (NotSupportedException ex)
        {
            return Result<EmployeeImportPreviewDto>.Failure(ex.Message);
        }
        catch (Exception ex)
        {
            AppLogger.Error("EmployeeImportService.PreviewAsync", ex);
            return Result<EmployeeImportPreviewDto>.Failure("Could not read that file. See the log for details.");
        }
    }

    private static List<List<string>> ReadCsv(Stream file)
    {
        using var r = new StreamReader(file, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return CsvWriter.Parse(r.ReadToEnd());
    }

    private static List<List<string>> ReadExcel(Stream file)
    {
        using var wb = new XLWorkbook(file);
        var ws = wb.Worksheet(1);
        var last = ws.LastRowUsed()?.RowNumber() ?? 0;
        var lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 0;

        var lines = new List<List<string>>();
        for (int r = 1; r <= last; r++)
        {
            var cells = new List<string>();
            for (int c = 1; c <= lastCol; c++)
            {
                var cell = ws.Cell(r, c);
                // A date cell must not come back as "45231"; ask for the typed value first.
                cells.Add(cell.DataType == XLDataType.DateTime && cell.TryGetValue<DateTime>(out var d)
                    ? d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                    : cell.GetString().Trim());
            }
            if (cells.Any(x => x.Length > 0)) lines.Add(cells);
        }
        return lines;
    }

    /// <summary>
    /// Maps header text to column positions, tolerating spacing and common alternatives, so a
    /// file exported from another system does not have to be renamed by hand first.
    /// </summary>
    private static Dictionary<string, int> MapColumns(List<string> header, EmployeeImportPreviewDto preview)
    {
        string Norm(string s) => new(s.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

        var aliases = new Dictionary<string, string[]>
        {
            ["code"]      = ["employeecode", "empcode", "code", "empno"],
            ["usercode"]  = ["userid", "usercode", "siteid"],
            ["fullname"]  = ["fullname", "name", "employeename", "firstname"],
            ["lastname"]  = ["lastname", "surname"],
            ["initials"]  = ["namewithinitials", "initials", "shortname"],
            ["nic"]       = ["nic", "nicnumber", "nationalid"],
            ["dept"]      = ["department", "dept"],
            ["desig"]     = ["designation", "jobtitle", "title", "position"],
            ["branch"]    = ["branch", "location", "site"],
            ["email"]     = ["email", "emailaddress"],
            ["phone"]     = ["phone", "mobile", "contact", "telephone"],
            ["gender"]    = ["gender", "sex"],
            ["dob"]       = ["dateofbirth", "dob", "birthdate"],
            ["joined"]    = ["joiningdate", "joined", "datejoined", "hiredate"],
            ["enroll"]    = ["biometricenrollid", "enrollid", "biometricid", "fingerprintid", "deviceuserid"],
            ["address"]   = ["address"],
            ["active"]    = ["active", "isactive", "status"],

            ["grade"]     = ["gradecode", "grade", "salarygrade"],
            ["basic"]     = ["basicsalary", "basic", "salary", "basicpay"],
            ["epfno"]     = ["epfnumber", "epfno", "epf", "epfmemberno"],
            ["etfno"]     = ["etfnumber", "etfno", "etf", "etfmemberno"],
            ["branchcode"] = ["bankbranchcode", "branchcode", "bankcode", "bankbranch"],
            ["accountno"] = ["accountnumber", "accountno", "acno", "bankaccount", "acctno"]
        };

        var normalised = header.Select(Norm).ToList();
        var map = new Dictionary<string, int>();

        foreach (var (key, names) in aliases)
        {
            var idx = normalised.FindIndex(h => names.Contains(h));
            if (idx >= 0) map[key] = idx;
        }

        if (!map.ContainsKey("fullname"))
            preview.FileWarnings.Add("No 'Full Name' column was found — every row will be rejected.");
        foreach (var required in new[] { "dept", "desig", "branch" })
            if (!map.ContainsKey(required))
                preview.FileWarnings.Add($"No '{required}' column was found; rows will be rejected unless one is added.");

        return map;
    }

    private static EmployeeImportRowDto ParseRow(List<string> cells, Dictionary<string, int> map, int rowNumber)
    {
        string? Get(string key) =>
            map.TryGetValue(key, out var i) && i < cells.Count && cells[i].Length > 0 ? cells[i].Trim() : null;

        var row = new EmployeeImportRowDto
        {
            RowNumber        = rowNumber,
            EmployeeCode     = Get("code"),
            UserCode         = Get("usercode"),
            FullName         = Get("fullname"),
            LastName         = Get("lastname"),
            NameWithInitials = Get("initials"),
            Nic              = Get("nic"),
            DepartmentName   = Get("dept"),
            DesignationName  = Get("desig"),
            BranchName       = Get("branch"),
            Email            = Get("email"),
            Phone            = Get("phone"),
            Gender           = Get("gender"),
            Address          = Get("address"),

            GradeCode        = Get("grade"),
            EpfNumber        = Get("epfno"),
            EtfNumber        = Get("etfno"),
            BankBranchCode   = Get("branchcode"),
            AccountNumber    = Get("accountno")
        };

        // Rejected rather than silently dropped. A salary that failed to parse and quietly
        // became nothing is somebody unpaid, and the file gives no hint which row it was.
        var basic = Get("basic");
        if (basic != null)
        {
            var cleaned = basic.Replace(",", "").Trim();
            if (decimal.TryParse(cleaned, System.Globalization.NumberStyles.Any,
                                 System.Globalization.CultureInfo.InvariantCulture, out var amount)
                && amount >= 0)
                row.BasicSalary = amount;
            else
                row.Errors.Add($"Basic Salary '{basic}' is not a valid amount.");
        }

        row.DateOfBirth = ParseDate(Get("dob"));
        row.JoiningDate = ParseDate(Get("joined"));

        var enroll = Get("enroll");
        if (enroll != null)
        {
            if (int.TryParse(enroll, out var id) && id > 0) row.BiometricEnrollId = id;
            else row.Errors.Add($"Biometric Enroll ID '{enroll}' is not a positive whole number.");
        }

        var active = Get("active");
        row.IsActive = active == null
            || active.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || active.Equals("true", StringComparison.OrdinalIgnoreCase)
            || active.Equals("1", StringComparison.Ordinal)
            || active.Equals("active", StringComparison.OrdinalIgnoreCase);

        return row;
    }

    /// <summary>
    /// Accepts the formats these files actually arrive in. ISO first, then day-first — a Sri
    /// Lankan roster writes 05/08/2026 meaning 5 August, and month-first parsing would move
    /// somebody's joining date by three months without complaining.
    /// </summary>
    private static DateTime? ParseDate(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;

        string[] formats =
        [
            "yyyy-MM-dd", "yyyy/MM/dd", "dd/MM/yyyy", "dd-MM-yyyy",
            "dd.MM.yyyy", "d/M/yyyy", "yyyy-MM-dd HH:mm:ss"
        ];

        return DateTime.TryParseExact(s, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d.Date
            : DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var loose)
                ? loose.Date
                : null;
    }

    private static void ResolveLookups(
        EmployeeImportRowDto row,
        Dictionary<string, int> depts, Dictionary<string, int> desigs, Dictionary<string, int> branches,
        HashSet<string> unknown)
    {
        row.DepartmentId  = Lookup(row.DepartmentName,  depts,    "Department",  row, unknown);
        row.DesignationId = Lookup(row.DesignationName, desigs,   "Designation", row, unknown);
        row.BranchId      = Lookup(row.BranchName,      branches, "Branch",      row, unknown);

        static int? Lookup(string? name, Dictionary<string, int> source, string label,
                           EmployeeImportRowDto row, HashSet<string> unknown)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                row.Errors.Add($"{label} is required.");
                return null;
            }
            if (source.TryGetValue(name.Trim(), out var id)) return id;

            row.Errors.Add($"{label} '{name}' does not exist. Create it first, or correct the spelling.");
            unknown.Add($"{label}: {name}");
            return null;
        }
    }

    /// <summary>
    /// Resolves the optional payroll columns.
    ///
    /// A blank column is silence, not an error — a site running attendance only deletes these
    /// columns and the file still imports. A value that does not resolve IS an error, because
    /// a grade code nobody recognises means the employee lands with no salary, and that is
    /// discovered on payday.
    /// </summary>
    private static void ResolvePayrollLookups(
        EmployeeImportRowDto row,
        Dictionary<string, int> grades, Dictionary<string, int> bankBranches,
        HashSet<string> unknown)
    {
        if (!string.IsNullOrWhiteSpace(row.GradeCode))
        {
            if (grades.TryGetValue(row.GradeCode.Trim(), out var gid)) row.SalaryGradeId = gid;
            else
            {
                row.Errors.Add($"Grade Code '{row.GradeCode}' does not exist. "
                             + "Add it under Payroll Setup first.");
                unknown.Add($"Grade: {row.GradeCode}");
            }
        }

        if (!string.IsNullOrWhiteSpace(row.BankBranchCode))
        {
            if (bankBranches.TryGetValue(row.BankBranchCode.Trim(), out var bid)) row.BankBranchId = bid;
            else
            {
                row.Errors.Add($"Bank Branch Code '{row.BankBranchCode}' does not exist. "
                             + "Add the bank and branch under Payroll Setup first.");
                unknown.Add($"Bank Branch: {row.BankBranchCode}");
            }
        }

        // Warned rather than rejected: it is legitimate at go-live to load salaries before
        // the bank details arrive. It is not legitimate to forget, so it is called out.
        if (!string.IsNullOrWhiteSpace(row.AccountNumber) && !row.BankBranchId.HasValue)
            row.Warnings.Add("An account number with no bank branch — the transfer file "
                           + "cannot be built for this employee.");

        if (row.SalaryGradeId.HasValue && row.BasicSalary.HasValue)
            row.Warnings.Add("Both a grade and a basic salary were given; the salary wins and "
                           + "this employee will not follow the grade.");
    }

    private static void MatchExisting(
        EmployeeImportRowDto row, Dictionary<string, int> byCode, Dictionary<int, int> byEnroll)
    {
        // Employee Code identifies a person; the enrol id is the fallback, because a file
        // pulled off the device has the enrol id and no code.
        if (!string.IsNullOrWhiteSpace(row.EmployeeCode) && byCode.TryGetValue(row.EmployeeCode.Trim(), out var id))
        {
            row.ExistingEmployeeId = id;
            return;
        }

        if (row.BiometricEnrollId.HasValue && byEnroll.TryGetValue(row.BiometricEnrollId.Value, out var id2))
        {
            row.ExistingEmployeeId = id2;
            row.Warnings.Add("Matched an existing employee by Biometric Enroll ID rather than Employee Code.");
        }
    }

    private static void Validate(
        EmployeeImportRowDto row, HashSet<string> seenCodes, HashSet<int> seenEnroll, Dictionary<int, int> byEnroll)
    {
        if (string.IsNullOrWhiteSpace(row.FullName))
            row.Errors.Add("Full Name is required.");

        if (row.JoiningDate == null)
            row.Errors.Add("Joining Date is required, and must be a recognisable date.");

        if (!string.IsNullOrWhiteSpace(row.EmployeeCode) && !seenCodes.Add(row.EmployeeCode.Trim()))
            row.Errors.Add($"Employee Code '{row.EmployeeCode}' appears more than once in this file.");

        if (row.BiometricEnrollId.HasValue)
        {
            var enroll = row.BiometricEnrollId.Value;

            if (!seenEnroll.Add(enroll))
                row.Errors.Add($"Biometric Enroll ID {enroll} appears more than once in this file.");

            // The same clash the single-employee form rejects: two people sharing an enrol id
            // means one person's punches are attributed to the other.
            else if (byEnroll.TryGetValue(enroll, out var ownerId) && ownerId != row.ExistingEmployeeId)
                row.Errors.Add($"Biometric Enroll ID {enroll} already belongs to a different employee.");
        }

        if (!string.IsNullOrWhiteSpace(row.Email) && !row.Email.Contains('@'))
            row.Warnings.Add($"'{row.Email}' does not look like an email address.");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Apply
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<Result<EmployeeImportResultDto>> ImportAsync(List<EmployeeImportRowDto> rows)
    {
        var result = new EmployeeImportResultDto();
        if (rows == null || rows.Count == 0)
            return Result<EmployeeImportResultDto>.Failure("No rows were submitted.");

        await using var tx = await _db.Database.BeginTransactionAsync();

        try
        {
            // Re-check server-side. The preview came back through the browser, where the
            // validation flags could have been edited; trusting them would let an invalid row
            // through the one place that is supposed to stop it.
            var byCode = await _db.Employees.Where(e => !e.IsDeleted)
                .ToDictionaryAsync(e => e.EmployeeCode, e => e, StringComparer.OrdinalIgnoreCase);

            // Rows carrying payroll data, paired with the employee they were written to.
            // Collected here and applied after the first save, when new employees have Ids.
            var written = new List<(EmployeeImportRowDto Row, Employee Employee)>();

            foreach (var row in rows)
            {
                if (!row.IsValid || row.DepartmentId == null || row.DesignationId == null || row.BranchId == null
                    || string.IsNullOrWhiteSpace(row.FullName) || row.JoiningDate == null)
                {
                    result.Skipped++;
                    continue;
                }

                Employee? emp = null;
                if (row.ExistingEmployeeId.HasValue)
                    emp = await _db.Employees.FirstOrDefaultAsync(e => e.Id == row.ExistingEmployeeId.Value && !e.IsDeleted);

                var isNew = emp == null;
                if (isNew)
                {
                    emp = new Employee
                    {
                        EmployeeCode = string.IsNullOrWhiteSpace(row.EmployeeCode)
                            ? await NextCodeAsync(byCode)
                            : row.EmployeeCode.Trim(),
                        CreatedAt = DateTime.Now
                    };
                    _db.Employees.Add(emp);
                }

                emp!.FirstName        = row.FullName!.Trim();
                emp.LastName          = row.LastName?.Trim() ?? string.Empty;
                emp.NameWithInitials  = row.NameWithInitials?.Trim();
                emp.Nic               = row.Nic?.Trim();
                emp.UserCode          = row.UserCode?.Trim();
                emp.Email             = row.Email?.Trim();
                emp.Phone             = row.Phone?.Trim();
                emp.Gender            = row.Gender?.Trim();
                emp.Address           = row.Address?.Trim();
                emp.DateOfBirth       = row.DateOfBirth;
                emp.JoiningDate       = row.JoiningDate!.Value;
                emp.DepartmentId      = row.DepartmentId!.Value;
                emp.DesignationId     = row.DesignationId!.Value;
                emp.BranchId          = row.BranchId!.Value;
                emp.IsActive          = row.IsActive;
                emp.ModifiedAt        = DateTime.Now;

                // Only set when supplied: a file without the column must not wipe the enrol
                // ids already recorded, which would break every future punch import.
                if (row.BiometricEnrollId.HasValue) emp.BiometricEnrollId = row.BiometricEnrollId;

                if (isNew) { byCode[emp.EmployeeCode] = emp; result.Created++; }
                else result.Updated++;

                if (row.HasPayrollData) written.Add((row, emp));
            }

            // Saved before the payroll pass because a newly created employee has no Id until
            // it is, and EmployeePayrollInfo is keyed on it. Both are inside the same
            // transaction, so a failure in the second pass still rolls the first one back —
            // employees loaded without their salaries would be the worst outcome of the two.
            await _db.SaveChangesAsync();

            if (written.Any())
            {
                var empIds = written.Select(w => w.Employee.Id).ToList();
                var infos = await _db.EmployeePayrollInfos
                    .Where(i => empIds.Contains(i.EmployeeId) && !i.IsDeleted)
                    .ToDictionaryAsync(i => i.EmployeeId);

                foreach (var (row, emp) in written)
                {
                    if (!infos.TryGetValue(emp.Id, out var info))
                    {
                        info = new EmployeePayrollInfo { EmployeeId = emp.Id, CreatedAt = DateTime.Now };
                        _db.EmployeePayrollInfos.Add(info);
                        infos[emp.Id] = info;
                    }

                    // Each field is written only when the column carried a value. A file with
                    // salary columns and blank bank details must not erase bank details that
                    // are already on the system — a re-import to fix one typo would otherwise
                    // silently clear everything the file did not mention.
                    if (row.SalaryGradeId.HasValue) info.SalaryGradeId = row.SalaryGradeId;
                    if (row.BasicSalary.HasValue) info.BasicSalaryOverride = row.BasicSalary > 0 ? row.BasicSalary : null;
                    if (!string.IsNullOrWhiteSpace(row.EpfNumber)) info.EpfNumber = row.EpfNumber.Trim();
                    if (!string.IsNullOrWhiteSpace(row.EtfNumber)) info.EtfNumber = row.EtfNumber.Trim();
                    if (row.BankBranchId.HasValue) info.BankBranchId = row.BankBranchId;
                    if (!string.IsNullOrWhiteSpace(row.AccountNumber)) info.AccountNumber = row.AccountNumber.Trim();

                    info.ModifiedAt = DateTime.Now;
                    result.PayrollRecordsWritten++;
                }

                await _db.SaveChangesAsync();
            }

            await tx.CommitAsync();
            return Result<EmployeeImportResultDto>.Success(result);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            AppLogger.Error("EmployeeImportService.ImportAsync", ex);
            return Result<EmployeeImportResultDto>.Failure(
                "Import failed and nothing was saved — the whole batch was rolled back. " + ex.Message);
        }
    }

    private static Task<string> NextCodeAsync(Dictionary<string, Employee> taken)
    {
        // Matches the existing EMP-00239 shape rather than inventing a second scheme.
        var max = taken.Keys
            .Select(c => c.StartsWith("EMP-", StringComparison.OrdinalIgnoreCase)
                      && int.TryParse(c[4..], out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max();

        string code;
        do { code = $"EMP-{++max:D5}"; } while (taken.ContainsKey(code));
        return Task.FromResult(code);
    }
}
