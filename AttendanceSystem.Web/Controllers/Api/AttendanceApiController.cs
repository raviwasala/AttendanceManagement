using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Web.Filters;
using Modules = AttendanceSystem.Common.Constants.AppConstants.Modules;
using Actions = AttendanceSystem.Common.Constants.AppConstants.Actions;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceSystem.Web.Controllers.Api;

[Route("api/attendance")]
[SessionAuthorize]
public class AttendanceApiController : ApiControllerBase
{
    private readonly IAttendanceService _svc;
    public AttendanceApiController(IAttendanceService svc) => _svc = svc;

    [HttpGet("today")]
    [SessionAuthorize(Modules.Attendance, Actions.View)]
    public async Task<IActionResult> Today()
    {
        var r = await _svc.GetTodayAsync();
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpGet("dashboard")]
    [SessionAuthorize(Modules.Dashboard, Actions.View)]
    public async Task<IActionResult> Dashboard()
    {
        var r = await _svc.GetDashboardStatsAsync();
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpGet("employee/{employeeId}")]
    [SessionAuthorize(Modules.Attendance, Actions.View)]
    public async Task<IActionResult> ByEmployee(int employeeId,
        [FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        var r = await _svc.GetByEmployeeAndDateRangeAsync(employeeId, from, to);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpGet("monthly")]
    [SessionAuthorize(Modules.Attendance, Actions.View)]
    public async Task<IActionResult> Monthly([FromQuery] int month, [FromQuery] int year)
    {
        var r = await _svc.GetMonthlySummaryAsync(month, year);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpPost("checkin")]
    [SessionAuthorize(Modules.Attendance, Actions.Create)]
    public async Task<IActionResult> CheckIn([FromBody] CheckInDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var r = await _svc.CheckInAsync(dto);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpPost("checkout")]
    [SessionAuthorize(Modules.Attendance, Actions.Create)]
    public async Task<IActionResult> CheckOut([FromBody] CheckOutDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var r = await _svc.CheckOutAsync(dto);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpPut("{id}")]
    [SessionAuthorize(Modules.Attendance, Actions.Edit)]
    public async Task<IActionResult> Edit(int id, [FromBody] EditAttendanceDto dto)
    {
        dto.Id = id;
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var r = await _svc.EditAsync(dto, CurrentUserId);
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }

    [HttpDelete("{id}")]
    [SessionAuthorize(Modules.Attendance, Actions.Delete)]
    public async Task<IActionResult> Delete(int id)
    {
        var r = await _svc.DeleteAsync(id, CurrentUserId);
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }
}
