using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Web.Filters;
using Modules = AttendanceSystem.Common.Constants.AppConstants.Modules;
using Actions = AttendanceSystem.Common.Constants.AppConstants.Actions;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceSystem.Web.Controllers.Api;

[Route("api/shifts")]
[SessionAuthorize]
public class ShiftsApiController : ApiControllerBase
{
    private readonly IShiftService _svc;
    public ShiftsApiController(IShiftService svc) => _svc = svc;

    [HttpGet]
    [SessionAuthorize(Modules.Shifts, Actions.View)]
    public async Task<IActionResult> GetAll()
    {
        var r = await _svc.GetAllAsync();
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpGet("{id}")]
    [SessionAuthorize(Modules.Shifts, Actions.View)]
    public async Task<IActionResult> GetById(int id)
    {
        var r = await _svc.GetByIdAsync(id);
        return r.IsSuccess ? Ok(r.Data) : NotFound(r.ErrorMessage);
    }

    [HttpPost]
    [SessionAuthorize(Modules.Shifts, Actions.Edit)]
    public async Task<IActionResult> Save([FromBody] SaveShiftDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var r = await _svc.SaveAsync(dto);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpDelete("{id}")]
    [SessionAuthorize(Modules.Shifts, Actions.Delete)]
    public async Task<IActionResult> Delete(int id)
    {
        var r = await _svc.DeleteAsync(id, CurrentUserId);
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }

    [HttpGet("assignments")]
    [SessionAuthorize(Modules.Shifts, Actions.View)]
    public async Task<IActionResult> GetAssignments()
    {
        var r = await _svc.GetEmployeeShiftsAsync();
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpPost("assign")]
    [SessionAuthorize(Modules.Shifts, Actions.Edit)]
    public async Task<IActionResult> Assign([FromBody] AssignShiftDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var r = await _svc.AssignShiftAsync(dto);
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }
}
