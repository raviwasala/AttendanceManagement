namespace AttendanceSystem.Common.Session;

/// <summary>Holds the currently logged-in user's session data.</summary>
public static class AppSession
{
    public static int UserId { get; private set; }
    public static string Username { get; private set; } = string.Empty;
    public static string FullName { get; private set; } = string.Empty;
    public static string RoleName { get; private set; } = string.Empty;
    public static int RoleId { get; private set; }
    public static int? EmployeeId { get; private set; }
    public static DateTime LoginTime { get; private set; }
    public static HashSet<string> Permissions { get; private set; } = new();

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
        Permissions = permissions != null
            ? new HashSet<string>(permissions, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    public static void Clear()
    {
        UserId = 0;
        Username = string.Empty;
        FullName = string.Empty;
        RoleName = string.Empty;
        RoleId = 0;
        EmployeeId = null;
        Permissions.Clear();
    }

    public static bool HasPermission(string module, string action) =>
        Permissions.Contains($"{module}.{action}");

    public static bool IsLoggedIn => UserId > 0;
}
