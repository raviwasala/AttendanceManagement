using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceSystem.Web.Controllers.Api;

[Route("api/shifts")]
[ApiController]
public class ShiftsApiController : ControllerBase
{
    private readonly IShiftService _svc;
    public ShiftsApiController(IShiftService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var r = await _svc.GetAllAsync();
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var r = await _svc.GetByIdAsync(id);
        return r.IsSuccess ? Ok(r.Data) : NotFound(r.ErrorMessage);
    }

    [HttpPost]
    public async Task<IActionResult> Save([FromBody] SaveShiftDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var r = await _svc.SaveAsync(dto);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id, [FromQuery] int deletedBy)
    {
        var r = await _svc.DeleteAsync(id, deletedBy);
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }

    [HttpGet("assignments")]
    public async Task<IActionResult> GetAssignments()
    {
        var r = await _svc.GetEmployeeShiftsAsync();
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpPost("assign")]
    public async Task<IActionResult> Assign([FromBody] AssignShiftDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var r = await _svc.AssignShiftAsync(dto);
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }
}
