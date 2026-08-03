using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Web.Filters;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceSystem.Web.Controllers.Api;

[Route("api/employees")]
[ApiController]
[SessionAuthorize]
public class EmployeesApiController : ControllerBase
{
    private readonly IEmployeeService _svc;
    public EmployeesApiController(IEmployeeService svc) => _svc = svc;

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

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q)
    {
        var r = await _svc.SearchAsync(q ?? "");
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpPost]
    public async Task<IActionResult> Save([FromBody] SaveEmployeeDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var r = await _svc.SaveAsync(dto);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var currentUserId = HttpContext.Session.GetInt32("UserId") ?? 1;
        var r = await _svc.DeleteAsync(id, currentUserId);
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }

    [HttpPost("{id}/toggle")]
    public async Task<IActionResult> Toggle(int id)
    {
        var currentUserId = HttpContext.Session.GetInt32("UserId") ?? 1;
        var r = await _svc.ToggleActiveAsync(id, currentUserId);
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }
}
