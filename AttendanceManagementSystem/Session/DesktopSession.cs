using AttendanceSystem.Common.Session;

namespace AttendanceManagementSystem.Session;

/// <summary>
/// Signed-in user for the desktop client.
///
/// This is deliberately process-wide state: the WinForms client is a single-user
/// application, so "the current user" really is a property of the process. The web
/// host must NEVER use this — it resolves <see cref="ICurrentUserContext"/> from DI
/// per request instead. That is why this type lives in the desktop executable and
/// not in a shared library.
/// </summary>
public static class DesktopSession
{
    private static HashSet<string> _permissions = PermissionKey.NewSet();

    public static int UserId { get; private set; }
    public static string Username { get; private set; } = string.Empty;
    public static string FullName { get; private set; } = string.Empty;
    public static string RoleName { get; private set; } = string.Empty;
    public static int RoleId { get; private set; }
    public static int? EmployeeId { get; private set; }
    public static DateTime LoginTime { get; private set; }

    public static IReadOnlyCollection<string> Permissions => _permissions;

    public static bool IsLoggedIn => UserId > 0;

    public static void SetSession(int userId, string username, string fullName,
        string roleName, int roleId, int? employeeId, IEnumerable<string>? permissions = null)
    {
        UserId = userId;
        Username = username;
        FullName = fullName;
        RoleName = roleName;
        RoleId = roleId;
        EmployeeId = employeeId;
        LoginTime = DateTime.Now;
        _permissions = PermissionKey.NewSet(permissions);
    }

    public static void Clear()
    {
        UserId = 0;
        Username = string.Empty;
        FullName = string.Empty;
        RoleName = string.Empty;
        RoleId = 0;
        EmployeeId = null;
        _permissions = PermissionKey.NewSet();
    }

    public static bool HasPermission(string module, string action) =>
        _permissions.Contains(PermissionKey.For(module, action));
}

/// <summary>Adapts <see cref="DesktopSession"/> to the DI-resolved <see cref="ICurrentUserContext"/>.</summary>
public sealed class DesktopUserContext : ICurrentUserContext
{
    public int? UserId => DesktopSession.IsLoggedIn ? DesktopSession.UserId : null;
    public string Username => DesktopSession.Username;
    public string FullName => DesktopSession.FullName;
    public string RoleName => DesktopSession.RoleName;
    public int RoleId => DesktopSession.RoleId;
    public int? EmployeeId => DesktopSession.EmployeeId;
    public IReadOnlyCollection<string> Permissions => DesktopSession.Permissions;
    public bool IsAuthenticated => DesktopSession.IsLoggedIn;
    public bool HasPermission(string module, string action) => DesktopSession.HasPermission(module, action);
}
