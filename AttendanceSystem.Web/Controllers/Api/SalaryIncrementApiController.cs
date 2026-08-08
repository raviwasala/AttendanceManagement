using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Web.Filters;
using Modules = AttendanceSystem.Common.Constants.AppConstants.Modules;
using Actions = AttendanceSystem.Common.Constants.AppConstants.Actions;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceSystem.Web.Controllers.Api;

/// <summary>
/// Salary increments.
///
/// Applying needs Payroll.Approve, not Edit. A raise changes what somebody is paid every
/// month from now on — a different order of decision from keying this month's incentive,
/// and usually a different person's authority.
/// </summary>
[Route("api/salary-increment")]
[SessionAuthorize]
public class SalaryIncrementApiController : ApiControllerBase
{
    private readonly ISalaryIncrementService _svc;
    public SalaryIncrementApiController(ISalaryIncrementService svc) => _svc = svc;

    [HttpPost("preview")]
    [SessionAuthorize(Modules.Payroll, Actions.View)]
    public async Task<IActionResult> Preview([FromBody] ApplyIncrementDto dto)
    {
        var r = await _svc.PreviewAsync(dto);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    // Proposing needs only Edit — it changes nobody's pay. Confirming and rejecting need
    // Approve, because that is the act that commits the company to the money.
    [HttpPost("propose")]
    [SessionAuthorize(Modules.Payroll, Actions.Edit)]
    public async Task<IActionResult> Propose([FromBody] ApplyIncrementDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var r = await _svc.ProposeAsync(dto);
        return r.IsSuccess ? Ok(new { Summary = r.Data }) : BadRequest(r.ErrorMessage);
    }

    [HttpGet("pending")]
    [SessionAuthorize(Modules.Payroll, Actions.View)]
    public async Task<IActionResult> Pending()
    {
        var r = await _svc.GetPendingAsync();
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpPost("confirm")]
    [SessionAuthorize(Modules.Payroll, Actions.Approve)]
    public async Task<IActionResult> Confirm([FromBody] ConfirmIncrementsDto dto)
    {
        var r = await _svc.ConfirmAsync(dto.Ids);
        return r.IsSuccess ? Ok(new { Summary = r.Data }) : BadRequest(r.ErrorMessage);
    }

    [HttpPost("reject")]
    [SessionAuthorize(Modules.Payroll, Actions.Approve)]
    public async Task<IActionResult> Reject([FromBody] RejectIncrementsDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var r = await _svc.RejectAsync(dto.Ids, dto.Reason);
        return r.IsSuccess ? Ok(new { Summary = r.Data }) : BadRequest(r.ErrorMessage);
    }

    [HttpGet("history")]
    [SessionAuthorize(Modules.Payroll, Actions.View)]
    public async Task<IActionResult> History(int? employeeId)
    {
        var r = await _svc.GetHistoryAsync(employeeId);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }
}
