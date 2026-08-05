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
    public string? Gender { get; set; }
    public string? Address { get; set; }
    public byte[]? Photo { get; set; }
    public int DepartmentId { get; set; }
    public Department Department { get; set; } = null!;
    public int DesignationId { get; set; }
    public Designation Designation { get; set; } = null!;
    public int BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public int? BiometricEnrollId { get; set; }

    public ICollection<EmployeeShift> EmployeeShifts { get; set; } = new List<EmployeeShift>();
    public ICollection<AttendanceLog> AttendanceLogs { get; set; } = new List<AttendanceLog>();
    public ICollection<LeaveRequest> LeaveRequests { get; set; } = new List<LeaveRequest>();
    public User? User { get; set; }
}
