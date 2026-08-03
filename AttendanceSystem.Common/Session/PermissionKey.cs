namespace AttendanceSystem.Common.Session;

/// <summary>
/// Single source of truth for the string form of a permission. Every layer that stores,
/// compares or serialises permissions goes through here so the formats cannot drift apart.
/// </summary>
public static class PermissionKey
{
    public static string For(string module, string action) => $"{module}.{action}";

    /// <summary>Comparer used for every permission set in the system — permission keys are case-insensitive.</summary>
    public static StringComparer Comparer => StringComparer.OrdinalIgnoreCase;

    public static HashSet<string> NewSet(IEnumerable<string>? keys = null) =>
        keys is null ? new HashSet<string>(Comparer) : new HashSet<string>(keys, Comparer);
}

/// <summary>An <see cref="ICurrentUserContext"/> representing "nobody" — used for unattributed operations.</summary>
public sealed class AnonymousUserContext : ICurrentUserContext
{
    public static readonly AnonymousUserContext Instance = new();

    public int? UserId => null;
    public string Username => string.Empty;
    public string FullName => string.Empty;
    public string RoleName => string.Empty;
    public int RoleId => 0;
    public int? EmployeeId => null;
    public IReadOnlyCollection<string> Permissions => Array.Empty<string>();
    public bool IsAuthenticated => false;
    public bool HasPermission(string module, string action) => false;
}
