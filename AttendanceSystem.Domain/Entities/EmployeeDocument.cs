using AttendanceSystem.Domain.Enums;

namespace AttendanceSystem.Domain.Entities;

/// <summary>
/// A file held against an employee: NIC copy, certificate, contract.
///
/// Bytes live on the row, exactly as <see cref="Employee.Photo"/> already does. That keeps the
/// backup archive self-contained — a restore onto another server brings the documents with it,
/// and deleting an employee cannot orphan a file on disk. The cost is row size, which is why
/// uploads are capped rather than unbounded.
/// </summary>
public class EmployeeDocument : BaseEntity
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public EmployeeDocumentType DocumentType { get; set; }

    /// <summary>What this is, in the operator's words — "NIC (front)", "NVQ Level 4".</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>The name as uploaded, so a download hands back something recognisable.</summary>
    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;
    public byte[] Content { get; set; } = [];
    public long SizeBytes { get; set; }

    /// <summary>
    /// For documents that lapse — a work permit, a medical certificate. Null when the document
    /// does not expire. Kept so a report can find what is about to run out.
    /// </summary>
    public DateTime? ExpiryDate { get; set; }

    public string? Notes { get; set; }
}
