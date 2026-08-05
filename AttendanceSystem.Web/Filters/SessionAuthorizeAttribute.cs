using AttendanceSystem.Common.Session;
using AttendanceSystem.Web.Session;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AttendanceSystem.Web.Filters;

/// <summary>
/// Requires a valid session, and optionally a specific <c>{module}.{action}</c> permission.
///
/// Apply the permission-carrying form to every endpoint that reads or changes data. The
/// parameterless form only proves that <em>someone</em> is signed in, which is not an
/// authorization decision — an Employee-role session satisfies it just as well as an admin.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class SessionAuthorizeAttribute : Attribute, IAuthorizationFilter
{
    private readonly string? _module;
    private readonly string? _action;

    public SessionAuthorizeAttribute() { }

    public SessionAuthorizeAttribute(string module, string action)
    {
        _module = module;
        _action = action;
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var httpContext = context.HttpContext;
        var user = httpContext.RequestServices.GetService<ICurrentUserContext>();

        if (user is null || !user.IsAuthenticated)
        {
            context.Result = IsApiRequest(httpContext)
                ? new UnauthorizedObjectResult(new
                {
                    isSuccess = false,
                    errorMessage = "Unauthorized access: Session expired or not logged in."
                })
                : new RedirectToActionResult("Login", "Auth", null);
            return;
        }

        // A locked screen refuses everything, before any permission is considered. This check
        // living here is the whole point: a lock enforced only in the browser would be undone
        // by a refresh, a typed URL or a direct API call.
        //
        // The unlock endpoints are exempt, or there would be no way back in.
        if (httpContext.Session?.GetInt32(WebSessionKeys.Locked) == 1 && !IsUnlockPath(httpContext))
        {
            context.Result = IsApiRequest(httpContext)
                ? new ObjectResult(new
                {
                    isSuccess = false,
                    isLocked = true,
                    errorMessage = "Screen locked. Enter your password to continue."
                })
                { StatusCode = StatusCodes.Status423Locked }
                : new RedirectToActionResult("Lock", "Auth", null);
            return;
        }

        if (string.IsNullOrEmpty(_module) || string.IsNullOrEmpty(_action)) return;

        if (!user.HasPermission(_module, _action))
        {
            context.Result = IsApiRequest(httpContext)
                ? new ObjectResult(new
                {
                    isSuccess = false,
                    errorMessage = $"Access Denied: Required permission '{PermissionKey.For(_module, _action)}' is missing."
                })
                { StatusCode = StatusCodes.Status403Forbidden }
                // Deliberately not a redirect to the dashboard: a user who lacks dashboard
                // access would bounce between the two forever.
                : new RedirectToActionResult("AccessDenied", "Auth", null);
        }
    }

    /// <summary>
    /// The endpoints that must stay reachable while locked: the lock screen itself, unlocking,
    /// and signing out — somebody who cannot remember their password needs a way out that is
    /// not closing the browser.
    /// </summary>
    private static bool IsUnlockPath(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        return path.StartsWith("/Auth/Lock", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/Auth/Unlock", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/Auth/Logout", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsApiRequest(HttpContext context) =>
        context.Request.Path.StartsWithSegments("/api") ||
        context.Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
        context.Request.Headers["Accept"].ToString().Contains("application/json");
}
