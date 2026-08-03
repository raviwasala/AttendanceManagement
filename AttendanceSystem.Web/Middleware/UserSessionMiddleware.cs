using AttendanceSystem.Common.Session;

namespace AttendanceSystem.Web.Middleware;

/// <summary>
/// Middleware that synchronizes Web HttpContext.Session values with AppSession ambient context
/// on every request so EF Core and Infrastructure services have access to current user ID.
/// </summary>
public class UserSessionMiddleware
{
    private readonly RequestDelegate _next;

    public UserSessionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Session.IsAvailable)
        {
            var userId = context.Session.GetInt32("UserId");
            if (userId.HasValue && userId.Value > 0)
            {
                var username = context.Session.GetString("Username") ?? "System";
                var fullName = context.Session.GetString("FullName") ?? "System User";
                var roleName = context.Session.GetString("RoleName") ?? "User";
                var roleId   = context.Session.GetInt32("RoleId") ?? 0;
                var empId    = context.Session.GetInt32("EmployeeId");

                AppSession.SetSession(userId.Value, username, fullName, roleName, roleId, empId);
            }
        }

        await _next(context);
    }
}

public static class UserSessionMiddlewareExtensions
{
    public static IApplicationBuilder UseUserSessionSync(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<UserSessionMiddleware>();
    }
}
