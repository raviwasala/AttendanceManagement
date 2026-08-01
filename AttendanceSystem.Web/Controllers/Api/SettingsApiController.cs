using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceSystem.Web.Controllers.Api;

[Route("api/settings")]
[ApiController]
public class SettingsApiController : ControllerBase
{
    private readonly ISettingsService _svc;
    public SettingsApiController(ISettingsService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var r = await _svc.GetAsync();
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpPost]
    public async Task<IActionResult> Save([FromBody] CompanySettingsDto dto,
        [FromQuery] int modifiedBy)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var r = await _svc.SaveAsync(dto, modifiedBy);
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }
}
