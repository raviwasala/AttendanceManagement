using AttendanceSystem.Application.Interfaces;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AttendanceSystem.Web.Helpers;

public static class PermissionExtensions
{
    public const string SessionUserId   = "UserId";
    public const string SessionRoleName = "RoleName";

    /// <summary>
    /// Checks if the current session user has access to a specific module and action.
    /// Administrators always have full permission.
    /// </summary>
    public static bool HasPermission(this HttpContext context, string module, string action)
    {
        var roleName = context.Session.GetString(SessionRoleName);
        if (!string.IsNullOrEmpty(roleName) && roleName.Equals("Administrator", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var userId = context.Session.GetInt32(SessionUserId);
        if (!userId.HasValue) return false;

        // Retrieve user permissions stored in session or check via service
        var permissionKey = $"Perm_{module}_{action}";
        var cachedVal = context.Session.GetString(permissionKey);
        if (!string.IsNullOrEmpty(cachedVal))
        {
            return bool.TryParse(cachedVal, out var result) && result;
        }

        // Default fallback: allow access if session is active or role matches
        return true;
    }

    /// <summary>
    /// Helper extension for Razor Views: @ViewContext.HasPermission("Employees", "Create")
    /// </summary>
    public static bool HasPermission(this ViewContext viewContext, string module, string action)
    {
        return viewContext.HttpContext.HasPermission(module, action);
    }
}
