using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Domain.Enums;
using AttendanceSystem.Web.Filters;
using Modules = AttendanceSystem.Common.Constants.AppConstants.Modules;
using Actions = AttendanceSystem.Common.Constants.AppConstants.Actions;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceSystem.Web.Controllers.Api;

/// <summary>
/// Staff loans. Payroll.View to read, Payroll.Edit to grant or settle — lending company
/// money is a payroll decision, not a setup one.
/// </summary>
[Route("api/loans")]
[SessionAuthorize]
public class LoansApiController : ApiControllerBase
{
    private readonly ILoanService _svc;
    public LoansApiController(ILoanService svc) => _svc = svc;

    [HttpGet]
    [SessionAuthorize(Modules.Payroll, Actions.View)]
    public async Task<IActionResult> Get([FromQuery] int? employeeId, [FromQuery] LoanStatus? status)
    {
        var r = await _svc.GetLoansAsync(employeeId, status);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    /// <summary>
    /// The schedule a set of terms would produce. A GET because it stores nothing — it exists
    /// so the interest and instalment are visible before the loan is granted, not after.
    /// </summary>
    [HttpGet("preview")]
    [SessionAuthorize(Modules.Payroll, Actions.View)]
    public IActionResult Preview([FromQuery] decimal amount, [FromQuery] decimal rate,
                                 [FromQuery] int months, [FromQuery] LoanInterestType interestType)
    {
        var r = _svc.PreviewSchedule(amount, rate, months, interestType);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpPost]
    [SessionAuthorize(Modules.Payroll, Actions.Edit)]
    public async Task<IActionResult> Save([FromBody] SaveEmployeeLoanDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var r = await _svc.SaveLoanAsync(dto);
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }

    [HttpGet("{loanId:int}/transactions")]
    [SessionAuthorize(Modules.Payroll, Actions.View)]
    public async Task<IActionResult> Transactions(int loanId)
    {
        var r = await _svc.GetTransactionsAsync(loanId);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpPost("settle")]
    [SessionAuthorize(Modules.Payroll, Actions.Edit)]
    public async Task<IActionResult> Settle([FromBody] LoanSettlementDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var r = await _svc.SettleAsync(dto);
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }
}
