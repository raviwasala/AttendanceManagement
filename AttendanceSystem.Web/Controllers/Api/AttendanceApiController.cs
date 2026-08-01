using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceSystem.Web.Controllers.Api;

[Route("api/attendance")]
[ApiController]
public class AttendanceApiController : ControllerBase
{
    private readonly IAttendanceService _svc;
    public AttendanceApiController(IAttendanceService svc) => _svc = svc;

    [HttpGet("today")]
    public async Task<IActionResult> Today()
    {
        var r = await _svc.GetTodayAsync();
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        var r = await _svc.GetDashboardStatsAsync();
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpGet("employee/{employeeId}")]
    public async Task<IActionResult> ByEmployee(int employeeId,
        [FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        var r = await _svc.GetByEmployeeAndDateRangeAsync(employeeId, from, to);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpGet("monthly")]
    public async Task<IActionResult> Monthly([FromQuery] int month, [FromQuery] int year)
    {
        var r = await _svc.GetMonthlySummaryAsync(month, year);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpPost("checkin")]
    public async Task<IActionResult> CheckIn([FromBody] CheckInDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var r = await _svc.CheckInAsync(dto);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpPost("checkout")]
    public async Task<IActionResult> CheckOut([FromBody] CheckOutDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var r = await _svc.CheckOutAsync(dto);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Edit(int id, [FromBody] EditAttendanceDto dto,
        [FromQuery] int modifiedBy)
    {
        dto.Id = id;
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var r = await _svc.EditAsync(dto, modifiedBy);
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id, [FromQuery] int deletedBy)
    {
        var r = await _svc.DeleteAsync(id, deletedBy);
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }
}
