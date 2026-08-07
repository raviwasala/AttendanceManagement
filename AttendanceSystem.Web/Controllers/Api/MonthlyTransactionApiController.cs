using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Web.Filters;
using Modules = AttendanceSystem.Common.Constants.AppConstants.Modules;
using Actions = AttendanceSystem.Common.Constants.AppConstants.Actions;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceSystem.Web.Controllers.Api;

/// <summary>
/// One-off transactions for a payroll month.
///
/// Payroll rather than PayrollSetup: entering this month's incentives is the clerk's daily
/// work, while deciding that an incentive code exists at all is policy.
/// </summary>
[Route("api/monthly-transactions")]
[SessionAuthorize]
public class MonthlyTransactionApiController : ApiControllerBase
{
    private readonly IMonthlyTransactionService _svc;
    public MonthlyTransactionApiController(IMonthlyTransactionService svc) => _svc = svc;

    [HttpGet("item-wise")]
    [SessionAuthorize(Modules.Payroll, Actions.View)]
    public async Task<IActionResult> ItemWise(int componentId, int yearMonth,
                                              int? departmentId, string? search)
    {
        var r = await _svc.GetItemWiseAsync(componentId, yearMonth, departmentId, search);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpPost("item-wise")]
    [SessionAuthorize(Modules.Payroll, Actions.Edit)]
    public async Task<IActionResult> SaveItemWise([FromBody] SaveItemWiseDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var r = await _svc.SaveItemWiseAsync(dto);
        return r.IsSuccess ? Ok(new { Summary = r.Data }) : BadRequest(r.ErrorMessage);
    }

    [HttpGet("employee-wise")]
    [SessionAuthorize(Modules.Payroll, Actions.View)]
    public async Task<IActionResult> EmployeeWise(int employeeId, int yearMonth)
    {
        var r = await _svc.GetEmployeeWiseAsync(employeeId, yearMonth);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpPost("employee-wise")]
    [SessionAuthorize(Modules.Payroll, Actions.Edit)]
    public async Task<IActionResult> SaveEmployeeWise([FromBody] SaveEmployeeWiseDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var r = await _svc.SaveEmployeeWiseAsync(dto);
        return r.IsSuccess ? Ok(new { Summary = r.Data }) : BadRequest(r.ErrorMessage);
    }

    [HttpGet("month-summary")]
    [SessionAuthorize(Modules.Payroll, Actions.View)]
    public async Task<IActionResult> MonthSummary(int yearMonth)
    {
        var r = await _svc.GetMonthSummaryAsync(yearMonth);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }
}
