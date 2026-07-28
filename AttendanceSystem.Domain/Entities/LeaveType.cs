namespace AttendanceSystem.Domain.Entities;

/// <summary>Type of leave (Annual, Sick, Casual, etc.).</summary>
public class LeaveType : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public int TotalDays { get; set; }
    public bool IsPaid { get; set; } = true;
    public bool IsActive { get; set; } = true;

    public ICollection<LeaveRequest> LeaveRequests { get; set; } = new List<LeaveRequest>();
}
