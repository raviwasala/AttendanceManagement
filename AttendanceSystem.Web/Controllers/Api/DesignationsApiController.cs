using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Web.Filters;
using Modules = AttendanceSystem.Common.Constants.AppConstants.Modules;
using Actions = AttendanceSystem.Common.Constants.AppConstants.Actions;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceSystem.Web.Controllers.Api;

[Route("api/designations")]
[SessionAuthorize]
public class DesignationsApiController : ApiControllerBase
{
    private readonly IDesignationService _svc;
    public DesignationsApiController(IDesignationService svc) => _svc = svc;

    [HttpGet]
    [SessionAuthorize(Modules.Designations, Actions.View)]
    public async Task<IActionResult> GetAll()
    {
        var r = await _svc.GetAllAsync();
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpGet("{id}")]
    [SessionAuthorize(Modules.Designations, Actions.View)]
    public async Task<IActionResult> GetById(int id)
    {
        var r = await _svc.GetByIdAsync(id);
        return r.IsSuccess ? Ok(r.Data) : NotFound(r.ErrorMessage);
    }

    [HttpPost]
    [SessionAuthorize(Modules.Designations, Actions.Edit)]
    public async Task<IActionResult> Save([FromBody] SaveDesignationDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var r = await _svc.SaveAsync(dto);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpDelete("{id}")]
    [SessionAuthorize(Modules.Designations, Actions.Delete)]
    public async Task<IActionResult> Delete(int id)
    {
        var r = await _svc.DeleteAsync(id, CurrentUserId);
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }
}
