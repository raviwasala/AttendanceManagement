namespace AttendanceSystem.Common.Session;

/// <summary>
/// Ambient information about the user on whose behalf the current operation runs.
///
/// Implementations MUST be scoped to a single logical operation — in a web host that
/// means per-request (backed by <c>HttpContext</c>); in the desktop host the process
/// only ever serves one user, so a process-wide implementation is correct there.
/// Never reintroduce process-wide mutable state here: it is shared by every concurrent
/// request in the web host and silently misattributes audit data.
/// </summary>
public interface ICurrentUserContext
{
    /// <summary>Id of the signed-in user, or <c>null</c> when the operation is unattributed.</summary>
    int? UserId { get; }

    string Username { get; }
    string FullName { get; }
    string RoleName { get; }
    int RoleId { get; }

    /// <summary>Employee record linked to this user, when the user is also an employee.</summary>
    int? EmployeeId { get; }

    /// <summary>Granted permissions as <c>"{Module}.{Action}"</c> keys.</summary>
    IReadOnlyCollection<string> Permissions { get; }

    bool IsAuthenticated { get; }

    /// <summary>True when the user holds the <c>{module}.{action}</c> permission.</summary>
    bool HasPermission(string module, string action);
}
