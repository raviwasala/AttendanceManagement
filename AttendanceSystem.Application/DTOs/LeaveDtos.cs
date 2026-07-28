using AttendanceSystem.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace AttendanceSystem.Application.DTOs;

public class LeaveTypeDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int TotalDays { get; set; }
    public bool IsPaid { get; set; }
    public bool IsActive { get; set; }
}

public class SaveLeaveTypeDto
{
    public int Id { get; set; }
    [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;
    [Range(1, 365)] public int TotalDays { get; set; }
    public bool IsPaid { get; set; } = true;
    public bool IsActive { get; set; } = true;
}

public class LeaveRequestDto
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeCode { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public int LeaveTypeId { get; set; }
    public string LeaveTypeName { get; set; } = string.Empty;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int TotalDays { get; set; }
    public string Reason { get; set; } = string.Empty;
    public LeaveStatus Status { get; set; }
    public string StatusDisplay => Status.ToString();
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectionReason { get; set; }
}

public class ApplyLeaveDto
{
    [Required] public int EmployeeId { get; set; }
    [Required] public int LeaveTypeId { get; set; }
    [Required] public DateTime FromDate { get; set; }
    [Required] public DateTime ToDate { get; set; }
    [Required, MaxLength(1000)] public string Reason { get; set; } = string.Empty;
}

public class ApproveRejectLeaveDto
{
    [Required] public int LeaveRequestId { get; set; }
    [Required] public bool IsApproved { get; set; }
    public string? RejectionReason { get; set; }
}

public class LeaveBalanceDto
{
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string LeaveTypeName { get; set; } = string.Empty;
    public int TotalAllowed { get; set; }
    public int UsedDays { get; set; }
    public int RemainingDays => TotalAllowed - UsedDays;
}
