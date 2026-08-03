using AttendanceSystem.Common.Session;
using Microsoft.AspNetCore.Http.Features;

namespace AttendanceSystem.Web.Session;

/// <summary>
/// Per-request <see cref="ICurrentUserContext"/> backed by <c>HttpContext.Session</c>.
///
/// Registered as scoped, so each request sees only its own user. It reads through to the
/// session on every access rather than caching at construction, so a sign-in or sign-out
/// that happens mid-request is reflected immediately.
/// </summary>
public sealed class HttpSessionUserContext : ICurrentUserContext
{
    private readonly IHttpContextAccessor _accessor;

    public HttpSessionUserContext(IHttpContextAccessor accessor) => _accessor = accessor;

    private ISession? Session
    {
        get
        {
            var http = _accessor.HttpContext;
            // Session is unavailable outside the request pipeline (e.g. startup, background work).
            return http?.Features.Get<ISessionFeature>() is not null && http.Session.IsAvailable
                ? http.Session
                : null;
        }
    }

    public int? UserId
    {
        get
        {
            var id = Session?.GetInt32(WebSessionKeys.UserId);
            return id > 0 ? id : null;
        }
    }

    public string Username => Session?.GetString(WebSessionKeys.Username) ?? string.Empty;
    public string FullName => Session?.GetString(WebSessionKeys.FullName) ?? string.Empty;
    public string RoleName => Session?.GetString(WebSessionKeys.RoleName) ?? string.Empty;
    public int RoleId => Session?.GetInt32(WebSessionKeys.RoleId) ?? 0;
    public int? EmployeeId => Session?.GetInt32(WebSessionKeys.EmployeeId);

    public IReadOnlyCollection<string> Permissions => ReadPermissions();

    public bool IsAuthenticated => UserId.HasValue;

    public bool HasPermission(string module, string action) =>
        IsAuthenticated && ReadPermissions().Contains(PermissionKey.For(module, action));

    private HashSet<string> ReadPermissions()
    {
        var raw = Session?.GetString(WebSessionKeys.Permissions);
        if (string.IsNullOrEmpty(raw)) return PermissionKey.NewSet();

        return PermissionKey.NewSet(raw.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    /// <summary>Writes the authenticated user's details into the session. Called on sign-in.</summary>
    public static void SignIn(HttpContext http, int userId, string username, string fullName,
        string email, int roleId, string roleName, int? employeeId, IEnumerable<string> permissions)
    {
        // Drop anything the pre-authentication session was carrying, so no state planted
        // before sign-in survives into the authenticated one.
        //
        // NOTE: this does not rotate the session *identifier* — ASP.NET Core's session
        // middleware has no supported way to do that, so a full session-fixation defence
        // needs cookie authentication rather than raw session state. Tracked separately.
        http.Session.Clear();

        http.Session.SetInt32(WebSessionKeys.UserId, userId);
        http.Session.SetString(WebSessionKeys.Username, username);
        http.Session.SetString(WebSessionKeys.FullName, fullName);
        http.Session.SetString(WebSessionKeys.Email, email);
        http.Session.SetInt32(WebSessionKeys.RoleId, roleId);
        http.Session.SetString(WebSessionKeys.RoleName, roleName);
        if (employeeId.HasValue) http.Session.SetInt32(WebSessionKeys.EmployeeId, employeeId.Value);

        http.Session.SetString(WebSessionKeys.Permissions, string.Join('\n', permissions));
    }
}
