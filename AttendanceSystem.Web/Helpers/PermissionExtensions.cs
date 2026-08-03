using AttendanceSystem.Common.Session;
using AttendanceSystem.Web.Session;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AttendanceSystem.Web.Helpers;

public static class PermissionExtensions
{
    /// <summary>
    /// Checks whether the signed-in user holds the <c>{module}.{action}</c> permission.
    ///
    /// This denies by default. The previous implementation ended in an unconditional
    /// <c>return true</c>, which made every permission check in the application — filters,
    /// controllers and views alike — a no-op.
    /// </summary>
    public static bool HasPermission(this HttpContext context, string module, string action)
    {
        var user = context.RequestServices.GetService<ICurrentUserContext>();
        if (user is null || !user.IsAuthenticated) return false;

        return user.HasPermission(module, action);
    }

    /// <summary>
    /// Helper extension for Razor views: <c>@ViewContext.HasPermission("Employees", "Create")</c>.
    /// Use it to hide controls the user cannot use — but note it is a display concern only;
    /// the authoritative check is the one on the endpoint.
    /// </summary>
    public static bool HasPermission(this ViewContext viewContext, string module, string action) =>
        viewContext.HttpContext.HasPermission(module, action);
}
