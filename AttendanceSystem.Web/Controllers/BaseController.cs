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

    protected bool UserHasPermission(string module, string action)
    {
        return Helpers.PermissionExtensions.HasPermission(HttpContext, module, action);
    }

    protected IActionResult? RequirePermission(string module, string action)
    {
        var authResult = RequireAuth();
        if (authResult != null) return authResult;

        if (!UserHasPermission(module, action))
        {
            TempData["Error"] = "Access Denied: You do not have sufficient permissions to perform this action.";
            return RedirectToAction("Index", "Admin");
        }
        return null;
    }
}
