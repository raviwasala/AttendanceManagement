using AttendanceSystem.Domain.Enums;

namespace AttendanceSystem.Domain.Entities;

/// <summary>Employee record.</summary>
public class Employee : BaseEntity
{
    /// <summary>System-generated unique code. Not the same as <see cref="UserCode"/>.</summary>
    public string EmployeeCode { get; set; } = string.Empty;

    /// <summary>
    /// The site's own identifier — "5 T", "89(W) 7 (E)", "Welewatta".
    ///
    /// Deliberately not the primary code: the supplied values repeat (dozens of people share
    /// "Welewatta") and many are blank, so it cannot carry a unique constraint. Kept because it
    /// is what staff actually recognise on a payslip or a roster.
    /// </summary>
    public string? UserCode { get; set; }

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}".Trim();

    /// <summary>
    /// Name in the abbreviated form the company uses — "W H A U Dinakshi". Sri Lankan names do
    /// not split reliably into first/last, so this is the form shown wherever space is short.
    /// </summary>
    public string? NameWithInitials { get; set; }

    /// <summary>National Identity Card number. Old 10-character or new 12-digit format.</summary>
    public string? Nic { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public DateTime JoiningDate { get; set; }

    /// <summary>
    /// When probation ended and employment was confirmed. Null while still on probation.
    ///
    /// Kept because entitlements commonly start from confirmation rather than joining —
    /// gratuity and some leave types among them.
    /// </summary>
    public DateTime? ConfirmedDate { get; set; }

    /// <summary>Married or Unmarried. Free text for the same reason as Gender.</summary>
    public string? CivilStatus { get; set; }
    public string? Gender { get; set; }
    public string? Address { get; set; }
    public byte[]? Photo { get; set; }
    public int DepartmentId { get; set; }
    public Department Department { get; set; } = null!;
    public int DesignationId { get; set; }
    public Designation Designation { get; set; } = null!;
    public int BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    /// <summary>
    /// Shorthand for "currently employed and working". Every existing list and report filters
    /// on this, so it stays — but it is now derived from <see cref="Status"/> rather than set
    /// on its own, so the two cannot disagree.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Why the employee is or is not working. IsActive could only say "not working"; this says
    /// whether they resigned, were suspended, or are on long leave — which decides whether the
    /// record is expected back and whether a punch arriving for them is an error.
    /// </summary>
    public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;

    /// <summary>
    /// Last working day. Attendance after this date is not expected, and a biometric import
    /// carrying punches past it is importing somebody else's finger on a shared enrol id.
    /// </summary>
    public DateTime? ResignationDate { get; set; }

    public string? ResignationReason { get; set; }

    public int? BiometricEnrollId { get; set; }

    public ICollection<EmployeeHistory> History { get; set; } = new List<EmployeeHistory>();
    public ICollection<EmployeeDocument> Documents { get; set; } = new List<EmployeeDocument>();

    public ICollection<EmployeeShift> EmployeeShifts { get; set; } = new List<EmployeeShift>();
    public ICollection<AttendanceLog> AttendanceLogs { get; set; } = new List<AttendanceLog>();
    public ICollection<LeaveRequest> LeaveRequests { get; set; } = new List<LeaveRequest>();
    public User? User { get; set; }
}
