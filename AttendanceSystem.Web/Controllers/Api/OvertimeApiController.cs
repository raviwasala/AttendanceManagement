using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Domain.Enums;
using AttendanceSystem.Web.Filters;
using Microsoft.AspNetCore.Mvc;
using Modules = AttendanceSystem.Common.Constants.AppConstants.Modules;
using Actions = AttendanceSystem.Common.Constants.AppConstants.Actions;

namespace AttendanceSystem.Web.Controllers.Api;

/// <summary>
/// Overtime management endpoints.
///
/// Rules are guarded by Overtime.Edit rather than Overtime.Create, because changing a rule
/// restates what every future claim is worth — it is a policy change, not data entry. Deciding
/// a claim needs Overtime.Approve, which is granted separately from viewing so a clerk can run
/// the register without being able to authorise payment.
/// </summary>
[Route("api/overtime")]
[SessionAuthorize]
public class OvertimeApiController : ApiControllerBase
{
    private readonly IOvertimeService _svc;

    public OvertimeApiController(IOvertimeService svc) => _svc = svc;

    // ── Rules ────────────────────────────────────────────────────────────────

    [HttpGet("rules")]
    [SessionAuthorize(Modules.Overtime, Actions.View)]
    public async Task<IActionResult> Rules()
    {
        var r = await _svc.GetRulesAsync();
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpGet("rules/{id:int}")]
    [SessionAuthorize(Modules.Overtime, Actions.View)]
    public async Task<IActionResult> Rule(int id)
    {
        var r = await _svc.GetRuleByIdAsync(id);
        return r.IsSuccess ? Ok(r.Data) : NotFound(r.ErrorMessage);
    }

    [HttpPost("rules")]
    [SessionAuthorize(Modules.Overtime, Actions.Edit)]
    public async Task<IActionResult> SaveRule([FromBody] SaveOvertimeRuleDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var r = await _svc.SaveRuleAsync(dto);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpDelete("rules/{id:int}")]
    [SessionAuthorize(Modules.Overtime, Actions.Delete)]
    public async Task<IActionResult> DeleteRule(int id)
    {
        var r = await _svc.DeleteRuleAsync(id);
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }

    // ── Claims ───────────────────────────────────────────────────────────────

    [HttpPost("generate")]
    [SessionAuthorize(Modules.Overtime, Actions.Create)]
    public async Task<IActionResult> Generate([FromBody] GenerateOvertimeDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var r = await _svc.GenerateAsync(dto);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpGet("register")]
    [SessionAuthorize(Modules.Overtime, Actions.View)]
    public async Task<IActionResult> Register(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int? employeeId, [FromQuery] int? departmentId,
        [FromQuery] OvertimeStatus? status)
    {
        var (start, end) = DefaultRange(from, to);
        var r = await _svc.GetRegisterAsync(start, end, employeeId, departmentId, status);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpPost("decide")]
    [SessionAuthorize(Modules.Overtime, Actions.Approve)]
    public async Task<IActionResult> Decide([FromBody] OvertimeDecisionDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var r = await _svc.DecideAsync(dto);
        return r.IsSuccess ? Ok(new { Changed = r.Data }) : BadRequest(r.ErrorMessage);
    }

    [HttpGet("summary")]
    [SessionAuthorize(Modules.Overtime, Actions.View)]
    public async Task<IActionResult> Summary(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int? departmentId, [FromQuery] int? employeeId)
    {
        var (start, end) = DefaultRange(from, to);
        var r = await _svc.GetSummaryAsync(start, end, departmentId, employeeId);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    /// <summary>Defaults to the current month, so a bare call returns something useful.</summary>
    private static (DateTime From, DateTime To) DefaultRange(DateTime? from, DateTime? to)
    {
        var today = DateTime.Today;
        var start = from?.Date ?? new DateTime(today.Year, today.Month, 1);
        var end = to?.Date ?? start.AddMonths(1).AddDays(-1);
        return (start, end);
    }
}
