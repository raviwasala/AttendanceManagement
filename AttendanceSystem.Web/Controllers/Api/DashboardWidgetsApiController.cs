using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Web.Filters;
using Modules = AttendanceSystem.Common.Constants.AppConstants.Modules;
using Actions = AttendanceSystem.Common.Constants.AppConstants.Actions;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceSystem.Web.Controllers.Api;

/// <summary>
/// Which dashboard widgets the signed-in user sees, and the company default.
///
/// Personal preferences need no module permission beyond being signed in — choosing what
/// appears on your own dashboard reveals nothing, and the service only ever offers widgets you
/// already hold the permission for. Setting the company default is Settings.Edit, because it
/// changes what every new user starts with.
/// </summary>
[Route("api/dashboard-widgets")]
[SessionAuthorize]
public class DashboardWidgetsApiController : ApiControllerBase
{
    private readonly IDashboardPreferenceService _svc;
    public DashboardWidgetsApiController(IDashboardPreferenceService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> Mine()
    {
        var r = await _svc.GetMyWidgetsAsync();
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpPost]
    public async Task<IActionResult> SaveMine([FromBody] SaveDashboardPreferencesDto dto)
    {
        var r = await _svc.SaveMyPreferencesAsync(dto);
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }

    [HttpPost("reset")]
    public async Task<IActionResult> ResetMine()
    {
        var r = await _svc.ResetMineAsync();
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }

    [HttpGet("default")]
    [SessionAuthorize(Modules.Settings, Actions.View)]
    public async Task<IActionResult> Default()
    {
        var r = await _svc.GetCompanyDefaultAsync();
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpPost("default")]
    [SessionAuthorize(Modules.Settings, Actions.Edit)]
    public async Task<IActionResult> SaveDefault([FromBody] SaveDashboardPreferencesDto dto)
    {
        var r = await _svc.SaveCompanyDefaultAsync(dto);
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }
}
