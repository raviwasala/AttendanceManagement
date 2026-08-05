using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Web.Filters;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceSystem.Web.Controllers.Api;

/// <summary>
/// Header notifications.
///
/// Only requires a signed-in session: the service itself filters each item by the permission
/// needed to act on it, so an employee gets an empty list rather than a 403 on every page.
/// </summary>
[Route("api/notifications")]
[SessionAuthorize]
public class NotificationsApiController : ApiControllerBase
{
    private readonly INotificationService _notifications;

    public NotificationsApiController(INotificationService notifications) => _notifications = notifications;

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var r = await _notifications.GetAsync();
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }
}
