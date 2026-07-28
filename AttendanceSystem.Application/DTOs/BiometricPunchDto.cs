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
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}
