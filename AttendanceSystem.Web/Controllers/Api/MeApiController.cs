using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Web.Filters;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceSystem.Web.Controllers.Api;

/// <summary>
/// Self-service data for the signed-in employee.
///
/// Note the absence of an employee id on every route. The service resolves it from the
/// session, so there is no parameter an employee could change to read a colleague's records.
/// </summary>
[Route("api/me")]
[SessionAuthorize]
public class MeApiController : ApiControllerBase
{
    private readonly ISelfServiceService _self;

    public MeApiController(ISelfServiceService self) => _self = self;

    [HttpGet("profile")]
    public async Task<IActionResult> Profile()
    {
        var r = await _self.GetMyProfileAsync();
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpGet("attendance")]
    public async Task<IActionResult> Attendance([FromQuery] int? year, [FromQuery] int? month)
    {
        var r = await _self.GetMyAttendanceAsync(year ?? DateTime.Today.Year, month ?? DateTime.Today.Month);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpGet("leave")]
    public async Task<IActionResult> Leave()
    {
        var r = await _self.GetMyLeaveAsync();
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }
}
