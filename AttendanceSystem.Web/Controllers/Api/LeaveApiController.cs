using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceSystem.Web.Controllers.Api;

[Route("api/leave")]
[ApiController]
public class LeaveApiController : ControllerBase
{
    private readonly ILeaveService _svc;
    public LeaveApiController(ILeaveService svc) => _svc = svc;

    // ── Leave Types ────────────────────────────────────────────────────────

    [HttpGet("types")]
    public async Task<IActionResult> GetTypes()
    {
        var r = await _svc.GetLeaveTypesAsync();
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpPost("types")]
    public async Task<IActionResult> SaveType([FromBody] SaveLeaveTypeDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var r = await _svc.SaveLeaveTypeAsync(dto);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpDelete("types/{id}")]
    public async Task<IActionResult> DeleteType(int id)
    {
        var r = await _svc.DeleteLeaveTypeAsync(id);
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }

    // ── Leave Requests ─────────────────────────────────────────────────────

    [HttpGet("requests")]
    public async Task<IActionResult> GetAll()
    {
        var r = await _svc.GetAllRequestsAsync();
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpGet("requests/pending")]
    public async Task<IActionResult> GetPending()
    {
        var r = await _svc.GetPendingAsync();
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpGet("requests/employee/{employeeId}")]
    public async Task<IActionResult> GetByEmployee(int employeeId)
    {
        var r = await _svc.GetByEmployeeAsync(employeeId);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpPost("requests")]
    public async Task<IActionResult> Apply([FromBody] ApplyLeaveDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var r = await _svc.ApplyLeaveAsync(dto);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpPost("requests/approve")]
    public async Task<IActionResult> Approve([FromBody] ApproveRejectLeaveDto dto,
        [FromQuery] int actionBy)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var r = await _svc.ApproveRejectAsync(dto, actionBy);
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }

    [HttpPost("requests/{id}/cancel")]
    public async Task<IActionResult> Cancel(int id, [FromQuery] int cancelledBy)
    {
        var r = await _svc.CancelAsync(id, cancelledBy);
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }

    [HttpGet("balances/{employeeId}")]
    public async Task<IActionResult> Balances(int employeeId)
    {
        var r = await _svc.GetBalancesAsync(employeeId);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }
}
