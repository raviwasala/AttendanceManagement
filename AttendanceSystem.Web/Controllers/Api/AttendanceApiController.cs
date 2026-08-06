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

    // Employees.View: this lists every employee's times. Attendance.View is what the
    // Employee role holds to see their own records via /api/me/attendance.
    [HttpGet("today")]
    [SessionAuthorize(Modules.Employees, Actions.View)]
    public async Task<IActionResult> Today()
    {
        var r = await _svc.GetTodayAsync();
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    // Employees.View, for exactly the reason given above "today": this returns company
    // headcount and RecentAttendance, which names individual employees and their times.
    // It was gated on Dashboard.View — a permission the Employee role holds — so every
    // member of staff could read the whole company's daily attendance roll.
    [HttpGet("dashboard")]
    [SessionAuthorize(Modules.Employees, Actions.View)]
    public async Task<IActionResult> Dashboard()
    {
        var r = await _svc.GetDashboardStatsAsync();
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    // Takes an arbitrary employee id, so it must require permission to see other people.
    [HttpGet("employee/{employeeId}")]
    [SessionAuthorize(Modules.Employees, Actions.View)]
    public async Task<IActionResult> ByEmployee(int employeeId,
        [FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        var r = await _svc.GetByEmployeeAndDateRangeAsync(employeeId, from, to);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpGet("monthly")]
    [SessionAuthorize(Modules.Employees, Actions.View)]
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
