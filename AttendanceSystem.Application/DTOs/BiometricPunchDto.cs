namespace AttendanceSystem.Application.DTOs;

/// <summary>Raw punch record from a biometric device.</summary>
public class BiometricPunchDto
{
    public int EnrollId { get; set; }
    public string? EmpName { get; set; }
    public DateTime PunchTime { get; set; }
    public string? FingerNumber { get; set; }
    public string? DeviceId { get; set; }
    public string? CardNo { get; set; }
}

/// <summary>Summary result after an import operation.</summary>
public class BiometricImportResultDto
{
    public int TotalRead { get; set; }
    public int Inserted { get; set; }

    /// <summary>
    /// Days that already existed and were refreshed from the device — typically a day imported
    /// mid-shift that has since gained its check-out.
    /// </summary>
    public int Updated { get; set; }

    public int Skipped { get; set; }
    public int Failed { get; set; }

    /// <summary>Left alone because somebody had corrected them by hand.</summary>
    public int SkippedManual { get; set; }

    /// <summary>Punches whose enrolment id matches nobody — the usual reason an import "does nothing".</summary>
    public int UnmatchedPunches { get; set; }

    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();

    public string Summary =>
        $"{TotalRead} punch(es) read — {Inserted} day(s) created, {Updated} updated"
        + (SkippedManual > 0 ? $", {SkippedManual} left as manually corrected" : "")
        + (Skipped > 0 ? $", {Skipped} unchanged" : "")
        + (UnmatchedPunches > 0 ? $", {UnmatchedPunches} punch(es) matched no employee" : "")
        + (Failed > 0 ? $", {Failed} failed" : "")
        + ".";
}
