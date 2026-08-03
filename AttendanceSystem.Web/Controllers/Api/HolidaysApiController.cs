using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Web.Filters;
using Modules = AttendanceSystem.Common.Constants.AppConstants.Modules;
using Actions = AttendanceSystem.Common.Constants.AppConstants.Actions;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceSystem.Web.Controllers.Api;

[Route("api/holidays")]
[SessionAuthorize]
public class HolidaysApiController : ApiControllerBase
{
    private readonly IHolidayService _svc;
    public HolidaysApiController(IHolidayService svc) => _svc = svc;

    [HttpGet]
    [SessionAuthorize(Modules.Holidays, Actions.View)]
    public async Task<IActionResult> GetAll()
    {
        var r = await _svc.GetAllAsync();
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpGet("year/{year}")]
    [SessionAuthorize(Modules.Holidays, Actions.View)]
    public async Task<IActionResult> GetByYear(int year)
    {
        var r = await _svc.GetByYearAsync(year);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpPost]
    [SessionAuthorize(Modules.Holidays, Actions.Edit)]
    public async Task<IActionResult> Save([FromBody] SaveHolidayDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var r = await _svc.SaveAsync(dto);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpDelete("{id}")]
    [SessionAuthorize(Modules.Holidays, Actions.Delete)]
    public async Task<IActionResult> Delete(int id)
    {
        var r = await _svc.DeleteAsync(id, CurrentUserId);
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }
}
