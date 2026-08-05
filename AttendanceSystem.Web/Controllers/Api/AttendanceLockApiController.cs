using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Web.Filters;
using Modules = AttendanceSystem.Common.Constants.AppConstants.Modules;
using Actions = AttendanceSystem.Common.Constants.AppConstants.Actions;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceSystem.Web.Controllers.Api;

/// <summary>
/// Closing an attendance period, and recalculating one.
///
/// Locking and unlocking sit behind <c>Attendance.Delete</c> rather than Edit: closing a month
/// stops everyone else editing it, and reopening one that payroll has already run on is the
/// most consequential action on this screen. Reusing an existing permission rather than adding
/// a module keeps it grantable — a new module in PermissionCatalogue only seeds into a new
/// database, so on this one it would exist for nobody.
/// </summary>
[Route("api/attendance-lock")]
[SessionAuthorize]
public class AttendanceLockApiController : ApiControllerBase
{
    private readonly IAttendanceLockService _svc;
    public AttendanceLockApiController(IAttendanceLockService svc) => _svc = svc;

    [HttpGet]
    [SessionAuthorize(Modules.Attendance, Actions.View)]
    public async Task<IActionResult> Get()
    {
        var r = await _svc.GetLocksAsync();
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpPost]
    [SessionAuthorize(Modules.Attendance, Actions.Delete)]
    public async Task<IActionResult> Lock([FromBody] LockPeriodDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var r = await _svc.LockPeriodAsync(dto);
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }

    [HttpPost("unlock")]
    [SessionAuthorize(Modules.Attendance, Actions.Delete)]
    public async Task<IActionResult> Unlock([FromBody] UnlockPeriodDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var r = await _svc.UnlockPeriodAsync(dto);
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }

    /// <summary>
    /// Recalculates a period from the times already recorded, against current shift settings.
    /// Edit rather than Delete: it changes derived figures, never the punches themselves, and
    /// it refuses locked days outright.
    /// </summary>
    [HttpPost("reprocess")]
    [SessionAuthorize(Modules.Attendance, Actions.Edit)]
    public async Task<IActionResult> Reprocess([FromBody] ReprocessRequestDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var r = await _svc.ReprocessAsync(dto);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }
}
