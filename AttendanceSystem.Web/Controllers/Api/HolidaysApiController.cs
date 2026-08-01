using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceSystem.Web.Controllers.Api;

[Route("api/holidays")]
[ApiController]
public class HolidaysApiController : ControllerBase
{
    private readonly IHolidayService _svc;
    public HolidaysApiController(IHolidayService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var r = await _svc.GetAllAsync();
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpGet("year/{year}")]
    public async Task<IActionResult> GetByYear(int year)
    {
        var r = await _svc.GetByYearAsync(year);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpPost]
    public async Task<IActionResult> Save([FromBody] SaveHolidayDto dto)
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
}
