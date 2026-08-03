using AttendanceSystem.Common.Session;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceSystem.Web.Controllers.Api;

/// <summary>
/// Base for the JSON API controllers.
///
/// Every derived controller is decorated with <c>[SessionAuthorize]</c>, so by the time an
/// action runs the caller is known. That is why <see cref="CurrentUserId"/> throws rather
/// than defaulting: the actions previously fell back to <c>?? 1</c>, which quietly recorded
/// privileged changes as though the built-in administrator had made them.
/// </summary>
[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected ICurrentUserContext CurrentUser =>
        HttpContext.RequestServices.GetRequiredService<ICurrentUserContext>();

    protected int CurrentUserId =>
        CurrentUser.UserId
        ?? throw new InvalidOperationException(
            "No authenticated user on this request. Actions reaching this point must be guarded by [SessionAuthorize].");
}
