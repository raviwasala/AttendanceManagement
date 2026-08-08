namespace AttendanceSystem.Application.DTOs;

/// <summary>A file produced for download: its bytes, its name and its content type.</summary>
public record ExportFileDto(byte[] Content, string FileName, string ContentType);

/// <summary>
/// The result of a SQL Server BACKUP DATABASE.
///
/// Returns a path rather than bytes: a .bak of a real attendance database runs to hundreds
/// of megabytes, and reading that into a byte[] to hand back would take the memory twice
/// over. The controller streams it from disk instead.
///
/// <see cref="CanStream"/> is false when SQL Server is on another machine — the file is then
/// written on that machine, and the only useful thing to report is where it landed.
/// </summary>
public class SqlBackupDto
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public bool CanStream { get; set; }
    public string? SqlMachine { get; set; }
    public List<string> Warnings { get; set; } = new();
}

/// <summary>What a dataset export covers. Ranged sets need dates; the others ignore them.</summary>
public enum ExportDataset
{
    Employees,
    Attendance,
    Leave,
    Overtime
}

// ──────────────────────────────────────────────────────────────────────────────
// Employee import
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>
/// One row read from an employee import file, plus what the system made of it.
///
/// Carries the resolved foreign keys as well as the raw text so the preview can show the
/// operator that "Prodution" matched nothing before anything is written, rather than failing
/// halfway through with 60 rows already created.
/// </summary>
public class EmployeeImportRowDto
{
    public int RowNumber { get; set; }

    public string? EmployeeCode { get; set; }
    public string? UserCode { get; set; }
    public string? FullName { get; set; }
    public string? LastName { get; set; }
    public string? NameWithInitials { get; set; }
    public string? Nic { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Gender { get; set; }
    public string? Address { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public DateTime? JoiningDate { get; set; }
    public int? BiometricEnrollId { get; set; }
    public bool IsActive { get; set; } = true;

    public string? DepartmentName { get; set; }
    public string? DesignationName { get; set; }
    public string? BranchName { get; set; }

    public int? DepartmentId { get; set; }
    public int? DesignationId { get; set; }
    public int? BranchId { get; set; }

    // ── Payroll ───────────────────────────────────────────────────────────────
    //
    // Optional throughout: an employee record is valid without them, and sites that run
    // attendance only should not be forced to invent salary data. Present, though, because
    // loading a few hundred employees and then keying their salary, EPF number and bank
    // account by hand afterwards is the same job done twice.

    public string? GradeCode { get; set; }
    public decimal? BasicSalary { get; set; }
    public string? EpfNumber { get; set; }
    public string? EtfNumber { get; set; }
    public string? BankBranchCode { get; set; }
    public string? AccountNumber { get; set; }

    public int? SalaryGradeId { get; set; }
    public int? BankBranchId { get; set; }

    /// <summary>True when the row carries any payroll field, so the commit knows to write one.</summary>
    public bool HasPayrollData =>
        SalaryGradeId.HasValue || BasicSalary.HasValue ||
        !string.IsNullOrWhiteSpace(EpfNumber) || !string.IsNullOrWhiteSpace(EtfNumber) ||
        BankBranchId.HasValue || !string.IsNullOrWhiteSpace(AccountNumber);

    /// <summary>Set when the row matches an existing employee — the import updates rather than inserts.</summary>
    public int? ExistingEmployeeId { get; set; }

    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();

    /// <summary>A row with errors is never written, whatever else the operator selects.</summary>
    public bool IsValid => Errors.Count == 0;

    public string Action => !IsValid ? "Skip" : ExistingEmployeeId.HasValue ? "Update" : "Create";
}

public class EmployeeImportPreviewDto
{
    public List<EmployeeImportRowDto> Rows { get; set; } = new();
    public int TotalRead { get; set; }
    public int ToCreate { get; set; }
    public int ToUpdate { get; set; }
    public int Invalid { get; set; }

    /// <summary>Names in the file that match no department/designation/branch on record.</summary>
    public List<string> UnknownLookups { get; set; } = new();
    public List<string> FileWarnings { get; set; } = new();
}

public class EmployeeImportResultDto
{
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }

    /// <summary>
    /// How many rows also carried salary, EPF or bank details. Reported separately because
    /// "239 employees imported" says nothing about whether any of them can be paid.
    /// </summary>
    public int PayrollRecordsWritten { get; set; }

    public List<string> Errors { get; set; } = new();
}

// ──────────────────────────────────────────────────────────────────────────────
// Backup / restore
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>One table inside a backup archive.</summary>
public class BackupTableInfoDto
{
    public string Table { get; set; } = string.Empty;
    public int RowsInFile { get; set; }
    public int RowsInDatabase { get; set; }

    /// <summary>False when the archive holds a table this build does not know about.</summary>
    public bool Recognised { get; set; } = true;
}

/// <summary>
/// What a restore would do, shown before anything is written.
///
/// A restore replaces data. Showing row counts on both sides first is the difference between
/// an operator noticing they picked last year's archive and finding out afterwards.
/// </summary>
public class RestorePreviewDto
{
    public string? CreatedAtUtc { get; set; }
    public string? SourceVersion { get; set; }
    public List<BackupTableInfoDto> Tables { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public bool CanRestore => Errors.Count == 0;
}

public class RestoreResultDto
{
    public List<BackupTableInfoDto> Tables { get; set; } = new();
    public int TotalRowsWritten { get; set; }
    public List<string> Warnings { get; set; } = new();
}
