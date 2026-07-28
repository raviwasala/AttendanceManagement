namespace AttendanceSystem.Domain.Entities;

/// <summary>Employee record.</summary>
public class Employee : BaseEntity
{
    public string EmployeeCode { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}";
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
