using System.ComponentModel.DataAnnotations;
using AttendanceSystem.Domain.Enums;

namespace AttendanceSystem.Application.DTOs;

// ── Rules ───────────────────────────────────────────────────────────────────────

public class OvertimeRuleDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public int Priority { get; set; } = 100;

    public int? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public int? ShiftId { get; set; }
    public string? ShiftName { get; set; }

    public OvertimeDayType DayType { get; set; }
    public string DayTypeDisplay => DayType switch
    {
        OvertimeDayType.WorkingDay => "Working day",
        OvertimeDayType.WeeklyOff  => "Weekly off",
        OvertimeDayType.Holiday    => "Holiday",
        _ => "Any day"
    };

    public decimal RateMultiplier { get; set; } = 1.5m;
    public int MinimumMinutes { get; set; } = 30;
    public int? MaxMinutesPerDay { get; set; }
    public int RoundToMinutes { get; set; } = 15;
    public bool RequiresApproval { get; set; } = true;

    /// <summary>Plain-language summary of the scope, so the list reads without decoding columns.</summary>
    public string ScopeDisplay =>
        string.Join(" · ", new[]
        {
            DepartmentName ?? "All departments",
            ShiftName ?? "All shifts",
            DayTypeDisplay
        });
}

public class SaveOvertimeRuleDto
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    [Range(1, 9999)]
    public int Priority { get; set; } = 100;

    public int? DepartmentId { get; set; }
    public int? ShiftId { get; set; }
    public OvertimeDayType DayType { get; set; } = OvertimeDayType.Any;

    [Range(typeof(decimal), "0.5", "10")]
    public decimal RateMultiplier { get; set; } = 1.5m;

    [Range(0, 480)]
    public int MinimumMinutes { get; set; } = 30;

    [Range(0, 1440)]
    public int? MaxMinutesPerDay { get; set; }

    [Range(0, 120)]
    public int RoundToMinutes { get; set; } = 15;

    public bool RequiresApproval { get; set; } = true;
}

// ── Register / approval rows ────────────────────────────────────────────────────

public class OvertimeRecordDto
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;

    public DateTime OvertimeDate { get; set; }
    public string DateDisplay => OvertimeDate.ToString("dd-MMM-yyyy");
    public string DayName => OvertimeDate.DayOfWeek.ToString();

    public string? ShiftName { get; set; }
    public string? CheckInDisplay { get; set; }
    public string? CheckOutDisplay { get; set; }

    public int RawMinutes { get; set; }
    public int ClaimedMinutes { get; set; }
    public int? ApprovedMinutes { get; set; }

    public string? RuleName { get; set; }
    public decimal RateMultiplier { get; set; }
    public OvertimeDayType DayType { get; set; }
    public string DayTypeDisplay => DayType switch
    {
        OvertimeDayType.WeeklyOff => "Weekly off",
        OvertimeDayType.Holiday   => "Holiday",
        _ => "Working day"
    };

    public OvertimeStatus Status { get; set; }
    public string StatusDisplay => Status.ToString();
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? Remarks { get; set; }
    public string? RejectionReason { get; set; }
    public bool IsManual { get; set; }

    public string ClaimedDisplay => Fmt(ClaimedMinutes);
    public string ApprovedDisplay => ApprovedMinutes.HasValue ? Fmt(ApprovedMinutes.Value) : "—";

    public decimal WeightedHours =>
        Math.Round((ApprovedMinutes ?? 0) / 60m * RateMultiplier, 2);

    internal static string Fmt(int minutes) =>
        minutes <= 0 ? "0h" : $"{minutes / 60}h {minutes % 60:00}m";
}

public class OvertimeRegisterDto
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public List<OvertimeRecordDto> Rows { get; set; } = [];

    public int PendingCount   => Rows.Count(r => r.Status == OvertimeStatus.Pending);
    public int ApprovedCount  => Rows.Count(r => r.Status == OvertimeStatus.Approved);
    public int RejectedCount  => Rows.Count(r => r.Status == OvertimeStatus.Rejected);

    public int TotalClaimedMinutes  => Rows.Sum(r => r.ClaimedMinutes);
    public int TotalApprovedMinutes => Rows.Sum(r => r.ApprovedMinutes ?? 0);
    public decimal TotalWeightedHours => Math.Round(Rows.Sum(r => r.WeightedHours), 2);

    public string TotalClaimedDisplay  => OvertimeRecordDto.Fmt(TotalClaimedMinutes);
    public string TotalApprovedDisplay => OvertimeRecordDto.Fmt(TotalApprovedMinutes);
}

// ── Decisions ───────────────────────────────────────────────────────────────────

public class OvertimeDecisionDto
{
    [Required, MinLength(1)]
    public List<int> Ids { get; set; } = [];

    public bool Approve { get; set; }

    /// <summary>
    /// Grant fewer minutes than claimed. Null keeps each row's own claimed figure, which is
    /// what a bulk approval means; a value only makes sense for a single row.
    /// </summary>
    [Range(0, 1440)]
    public int? ApprovedMinutes { get; set; }

    [StringLength(500)]
    public string? Reason { get; set; }
}

public class GenerateOvertimeDto
{
    [Required] public DateTime From { get; set; }
    [Required] public DateTime To { get; set; }
    public int? DepartmentId { get; set; }
    public int? EmployeeId { get; set; }
}

public class GenerateOvertimeResultDto
{
    public int Scanned { get; set; }
    public int Created { get; set; }
    public int Updated { get; set; }
    public int AutoApproved { get; set; }
    public int SkippedBelowMinimum { get; set; }
    public int SkippedAlreadyDecided { get; set; }
    public int SkippedNoRule { get; set; }

    public string Summary =>
        $"{Scanned} day(s) with overtime scanned — {Created} new, {Updated} updated"
        + (AutoApproved > 0 ? $", {AutoApproved} auto-approved" : "")
        + (SkippedBelowMinimum > 0 ? $", {SkippedBelowMinimum} below the minimum" : "")
        + (SkippedAlreadyDecided > 0 ? $", {SkippedAlreadyDecided} already decided" : "")
        + (SkippedNoRule > 0 ? $", {SkippedNoRule} with no matching rule" : "")
        + ".";
}

// ── Summary ─────────────────────────────────────────────────────────────────────

public class OvertimeSummaryRowDto
{
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;

    public int Days { get; set; }
    public int PendingMinutes { get; set; }
    public int ApprovedMinutes { get; set; }
    public int RejectedMinutes { get; set; }
    public decimal WeightedHours { get; set; }

    /// <summary>Approved minutes that fell on a weekly off or a holiday — usually the dearest.</summary>
    public int PremiumMinutes { get; set; }

    public string PendingDisplay  => OvertimeRecordDto.Fmt(PendingMinutes);
    public string ApprovedDisplay => OvertimeRecordDto.Fmt(ApprovedMinutes);
    public string RejectedDisplay => OvertimeRecordDto.Fmt(RejectedMinutes);
    public string PremiumDisplay  => OvertimeRecordDto.Fmt(PremiumMinutes);
}

public class OvertimeSummaryDto
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public string PeriodDisplay => $"{From:dd-MMM-yyyy} – {To:dd-MMM-yyyy}";

    public List<OvertimeSummaryRowDto> Rows { get; set; } = [];

    public int EmployeesWithOvertime => Rows.Count;
    public int TotalPendingMinutes  => Rows.Sum(r => r.PendingMinutes);
    public int TotalApprovedMinutes => Rows.Sum(r => r.ApprovedMinutes);
    public decimal TotalWeightedHours => Math.Round(Rows.Sum(r => r.WeightedHours), 2);

    public string TotalPendingDisplay  => OvertimeRecordDto.Fmt(TotalPendingMinutes);
    public string TotalApprovedDisplay => OvertimeRecordDto.Fmt(TotalApprovedMinutes);
}
