using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AttendanceSystem.Web.Filters;

/// <summary>
/// Action filter that verifies that a valid user session exists.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
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
        var userId = httpContext.Session.GetInt32("UserId");

        if (!userId.HasValue || userId.Value <= 0)
        {
            if (IsApiRequest(httpContext))
            {
                context.Result = new UnauthorizedObjectResult(new 
                { 
                    isSuccess = false, 
                    errorMessage = "Unauthorized access: Session expired or not logged in." 
                });
            }
            else
            {
                context.Result = new RedirectToActionResult("Login", "Auth", null);
            }
            return;
        }

        // Optional module permission check
        if (!string.IsNullOrEmpty(_module) && !string.IsNullOrEmpty(_action))
        {
            var hasPermission = Helpers.PermissionExtensions.HasPermission(httpContext, _module, _action);
            if (!hasPermission)
            {
                if (IsApiRequest(httpContext))
                {
                    context.Result = new ObjectResult(new 
                    { 
                        isSuccess = false, 
                        errorMessage = $"Access Denied: Required permission '{_module}.{_action}' is missing." 
                    }) { StatusCode = StatusCodes.Status403Forbidden };
                }
                else
                {
                    context.Result = new RedirectToActionResult("Index", "Admin", null);
                }
            }
        }
    }

    private static bool IsApiRequest(HttpContext context)
    {
        return context.Request.Path.StartsWithSegments("/api") ||
               context.Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
               context.Request.Headers["Accept"].ToString().Contains("application/json");
    }
}
