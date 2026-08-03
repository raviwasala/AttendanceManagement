using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Web.Filters;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceSystem.Web.Controllers.Api;

[Route("api/designations")]
[ApiController]
[SessionAuthorize]
public class DesignationsApiController : ControllerBase
{
    private readonly IDesignationService _svc;
    public DesignationsApiController(IDesignationService svc) => _svc = svc;

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
    public async Task<IActionResult> Save([FromBody] SaveDesignationDto dto)
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
}
