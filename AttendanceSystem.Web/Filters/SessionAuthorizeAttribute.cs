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

    private static bool IsApiRequest(HttpContext context) =>
        context.Request.Path.StartsWithSegments("/api") ||
        context.Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
        context.Request.Headers["Accept"].ToString().Contains("application/json");
}
