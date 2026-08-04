using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Web.Filters;
using Microsoft.AspNetCore.Mvc;
using Modules = AttendanceSystem.Common.Constants.AppConstants.Modules;
using Actions = AttendanceSystem.Common.Constants.AppConstants.Actions;

namespace AttendanceSystem.Web.Controllers.Api;

/// <summary>
/// Monthly shift roster. Reading is Shifts.View; changing a day is Shifts.Edit, because a
/// roster change silently alters whether people are recorded as late.
/// </summary>
[Route("api/shift-roster")]
[SessionAuthorize]
public class ShiftRosterApiController : ApiControllerBase
{
    private readonly IShiftRosterService _roster;

    public ShiftRosterApiController(IShiftRosterService roster) => _roster = roster;

    [HttpGet]
    [SessionAuthorize(Modules.Shifts, Actions.View)]
    public async Task<IActionResult> Get([FromQuery] int? year, [FromQuery] int? month, [FromQuery] int? departmentId)
    {
        // Default to the current month so the screen is useful with no query string.
        var y = year ?? DateTime.Today.Year;
        var m = month ?? DateTime.Today.Month;

        var r = await _roster.GetMonthlyRosterAsync(y, m, departmentId);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpPost("day")]
    [SessionAuthorize(Modules.Shifts, Actions.Edit)]
    public async Task<IActionResult> SetDay([FromBody] SetRosterDayDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var r = await _roster.SetDayAsync(dto);
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }

    [HttpPost("range")]
    [SessionAuthorize(Modules.Shifts, Actions.Edit)]
    public async Task<IActionResult> SetRange([FromBody] SetRosterRangeDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var r = await _roster.SetRangeAsync(dto);
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }
}
