using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Web.Filters;
using Modules = AttendanceSystem.Common.Constants.AppConstants.Modules;
using Actions = AttendanceSystem.Common.Constants.AppConstants.Actions;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceSystem.Web.Controllers.Api;

[Route("api/leave")]
[SessionAuthorize]
public class LeaveApiController : ApiControllerBase
{
    private readonly ILeaveService _svc;
    public LeaveApiController(ILeaveService svc) => _svc = svc;

    // â”€â”€ Leave Types â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [HttpGet("types")]
    [SessionAuthorize(Modules.Leave, Actions.View)]
    public async Task<IActionResult> GetTypes()
    {
        var r = await _svc.GetLeaveTypesAsync();
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpPost("types")]
    [SessionAuthorize(Modules.Leave, Actions.Edit)]
    public async Task<IActionResult> SaveType([FromBody] SaveLeaveTypeDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var r = await _svc.SaveLeaveTypeAsync(dto);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpDelete("types/{id}")]
    [SessionAuthorize(Modules.Leave, Actions.Delete)]
    public async Task<IActionResult> DeleteType(int id)
    {
        var r = await _svc.DeleteLeaveTypeAsync(id);
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }

    // â”€â”€ Leave Requests â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [HttpGet("requests")]
    [SessionAuthorize(Modules.Leave, Actions.View)]
    public async Task<IActionResult> GetAll()
    {
        var r = await _svc.GetAllRequestsAsync();
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpGet("requests/pending")]
    [SessionAuthorize(Modules.Leave, Actions.View)]
    public async Task<IActionResult> GetPending()
    {
        var r = await _svc.GetPendingAsync();
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpGet("requests/employee/{employeeId}")]
    [SessionAuthorize(Modules.Leave, Actions.View)]
    public async Task<IActionResult> GetByEmployee(int employeeId)
    {
        var r = await _svc.GetByEmployeeAsync(employeeId);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpPost("requests")]
    [SessionAuthorize(Modules.Leave, Actions.Create)]
    public async Task<IActionResult> Apply([FromBody] ApplyLeaveDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var r = await _svc.ApplyLeaveAsync(dto);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpPost("requests/approve")]
    [SessionAuthorize(Modules.Leave, Actions.Approve)]
    public async Task<IActionResult> Approve([FromBody] ApproveRejectLeaveDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var r = await _svc.ApproveRejectAsync(dto, CurrentUserId);
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }

    [HttpPost("requests/{id}/cancel")]
    [SessionAuthorize(Modules.Leave, Actions.Edit)]
    public async Task<IActionResult> Cancel(int id)
    {
        var r = await _svc.CancelAsync(id, CurrentUserId);
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }

    [HttpGet("balances/{employeeId}")]
    [SessionAuthorize(Modules.Leave, Actions.View)]
    public async Task<IActionResult> Balances(int employeeId)
    {
        var r = await _svc.GetBalancesAsync(employeeId);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }
}
