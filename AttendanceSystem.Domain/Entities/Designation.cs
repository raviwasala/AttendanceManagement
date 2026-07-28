namespace AttendanceSystem.Domain.Entities;

/// <summary>Employee job designation / title.</summary>
public class Designation : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Employee> Employees { get; set; } = new List<Employee>();
}
