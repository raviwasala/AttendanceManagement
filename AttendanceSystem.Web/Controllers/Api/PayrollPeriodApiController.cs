using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Web.Filters;
using Modules = AttendanceSystem.Common.Constants.AppConstants.Modules;
using Actions = AttendanceSystem.Common.Constants.AppConstants.Actions;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceSystem.Web.Controllers.Api;

/// <summary>
/// The current payroll month.
///
/// Reading it needs only Payroll.View — every entry screen calls it on load. Moving it
/// needs Payroll.Approve: closing a month decides which payslip a figure lands on, and
/// that is not the same authority as keying the figure.
/// </summary>
[Route("api/payroll-period")]
[SessionAuthorize]
public class PayrollPeriodApiController : ApiControllerBase
{
    private readonly IPayrollPeriodService _svc;
    public PayrollPeriodApiController(IPayrollPeriodService svc) => _svc = svc;

    [HttpGet("current")]
    [SessionAuthorize(Modules.Payroll, Actions.View)]
    public async Task<IActionResult> Current()
    {
        var r = await _svc.GetCurrentAsync();
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpGet]
    [SessionAuthorize(Modules.Payroll, Actions.View)]
    public async Task<IActionResult> All()
    {
        var r = await _svc.GetAllAsync();
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpPost("open")]
    [SessionAuthorize(Modules.Payroll, Actions.Approve)]
    public async Task<IActionResult> Open([FromBody] OpenPayrollPeriodDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var r = await _svc.OpenAsync(dto);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpPost("close")]
    [SessionAuthorize(Modules.Payroll, Actions.Approve)]
    public async Task<IActionResult> Close()
    {
        var r = await _svc.CloseAndAdvanceAsync();
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpPost("reopen")]
    [SessionAuthorize(Modules.Payroll, Actions.Approve)]
    public async Task<IActionResult> Reopen([FromBody] ReopenPayrollPeriodDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var r = await _svc.ReopenAsync(dto.Id, dto.Reason);
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }
}
