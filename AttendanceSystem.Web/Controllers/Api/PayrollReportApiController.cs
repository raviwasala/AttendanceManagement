using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Web.Filters;
using Modules = AttendanceSystem.Common.Constants.AppConstants.Modules;
using Actions = AttendanceSystem.Common.Constants.AppConstants.Actions;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceSystem.Web.Controllers.Api;

/// <summary>
/// Reading a payroll month. All three views need only Payroll.View — they change nothing.
/// </summary>
[Route("api/payroll-report")]
[SessionAuthorize]
public class PayrollReportApiController : ApiControllerBase
{
    private readonly IPayrollReportService _svc;
    public PayrollReportApiController(IPayrollReportService svc) => _svc = svc;

    [HttpGet("register/{periodId:int}")]
    [SessionAuthorize(Modules.Payroll, Actions.View)]
    public async Task<IActionResult> Register(int periodId, int? departmentId)
    {
        var r = await _svc.GetRegisterAsync(periodId, departmentId);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpGet("summary/{periodId:int}")]
    [SessionAuthorize(Modules.Payroll, Actions.View)]
    public async Task<IActionResult> Summary(int periodId)
    {
        var r = await _svc.GetSummaryAsync(periodId);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpGet("payslips/{periodId:int}")]
    [SessionAuthorize(Modules.Payroll, Actions.View)]
    public async Task<IActionResult> Payslips(int periodId, int? departmentId)
    {
        var r = await _svc.GetPayslipsAsync(periodId, departmentId);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpGet("payslip/{payslipId:int}")]
    [SessionAuthorize(Modules.Payroll, Actions.View)]
    public async Task<IActionResult> Payslip(int payslipId)
    {
        var r = await _svc.GetPayslipAsync(payslipId);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }
}
