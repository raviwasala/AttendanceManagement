using Microsoft.AspNetCore.Mvc;

namespace AttendanceSystem.Web.Controllers;

/// <summary>
/// Base controller — provides session-backed current-user helpers.
/// </summary>
public abstract class BaseController : Controller
{
    protected const string SessionUserId   = "UserId";
    protected const string SessionUsername = "Username";
    protected const string SessionFullName = "FullName";
    protected const string SessionRoleId   = "RoleId";
    protected const string SessionEmail    = "Email";
    protected const string SessionRoleName = "RoleName";

    protected int? CurrentUserId =>
        HttpContext.Session.GetInt32(SessionUserId);

    protected string CurrentUsername =>
        HttpContext.Session.GetString(SessionUsername) ?? "System";

    protected bool IsAuthenticated => CurrentUserId.HasValue;

    protected IActionResult RequireAuth()
    {
        if (!IsAuthenticated)
            return RedirectToAction("Login", "Auth");
        return null!;
    }
}
