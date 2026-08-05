namespace AttendanceSystem.Web.Session;

/// <summary>Keys under which the signed-in user's details live in <c>HttpContext.Session</c>.</summary>
public static class WebSessionKeys
{
    public const string UserId = "UserId";
    public const string Username = "Username";
    public const string FullName = "FullName";
    public const string Email = "Email";
    public const string RoleId = "RoleId";
    public const string RoleName = "RoleName";
    public const string EmployeeId = "EmployeeId";

    /// <summary>Newline-separated list of granted <c>"{Module}.{Action}"</c> keys.</summary>
    public const string Permissions = "Permissions";

    /// <summary>
    /// Set to 1 while the screen is locked.
    ///
    /// Held in the session rather than the browser because a lock that lives only in the page
    /// is decoration: refreshing, typing a URL or calling the API directly would all still
    /// work. Every request is refused while this is set, which is what makes it a lock.
    /// </summary>
    public const string Locked = "Locked";
}
