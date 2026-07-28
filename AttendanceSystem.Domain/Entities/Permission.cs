namespace AttendanceSystem.Domain.Entities;

/// <summary>Permission entity for granular access control.</summary>
public class Permission : BaseEntity
{
    public string Module { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
