using AttendanceSystem.Common.Session;
using AttendanceSystem.Web.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceSystem.Web.Controllers;

/// <summary>
/// Base controller — exposes the current user via the request-scoped
/// <see cref="ICurrentUserContext"/> rather than reading session keys ad hoc.
/// </summary>
public abstract class BaseController : Controller
{
    private ICurrentUserContext CurrentUser =>
        HttpContext.RequestServices.GetRequiredService<ICurrentUserContext>();

    protected int? CurrentUserId => CurrentUser.UserId;

    protected string CurrentUsername => CurrentUser.Username;

    protected bool IsAuthenticated => CurrentUser.IsAuthenticated;

    protected IActionResult? RequireAuth() =>
        IsAuthenticated ? null : RedirectToAction("Login", "Auth");

    protected bool UserHasPermission(string module, string action) =>
        HttpContext.HasPermission(module, action);

    protected IActionResult? RequirePermission(string module, string action)
    {
        var authResult = RequireAuth();
        if (authResult != null) return authResult;

        if (!UserHasPermission(module, action))
        {
            TempData["Error"] = "Access Denied: You do not have sufficient permissions to perform this action.";
            return RedirectToAction("AccessDenied", "Auth");
        }
        return null;
    }
}
