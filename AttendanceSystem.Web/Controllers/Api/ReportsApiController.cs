using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Web.Filters;
using Modules = AttendanceSystem.Common.Constants.AppConstants.Modules;
using Actions = AttendanceSystem.Common.Constants.AppConstants.Actions;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceSystem.Web.Controllers.Api;

[Route("api/reports")]
[SessionAuthorize]
public class ReportsApiController : ApiControllerBase
{
    private readonly IReportService _svc;
    public ReportsApiController(IReportService svc) => _svc = svc;

    [HttpGet("daily")]
    [SessionAuthorize(Modules.Reports, Actions.View)]
    public async Task<IActionResult> Daily([FromQuery] DateTime date)
    {
        var data = await _svc.GetDailyAttendanceReportAsync(date);
        return Ok(data);
    }

    [HttpGet("monthly")]
    [SessionAuthorize(Modules.Reports, Actions.View)]
    public async Task<IActionResult> Monthly([FromQuery] int month, [FromQuery] int year,
        [FromQuery] int? departmentId)
    {
        var data = await _svc.GetMonthlyAttendanceReportAsync(month, year, departmentId);
        return Ok(data);
    }

    [HttpGet("late")]
    [SessionAuthorize(Modules.Reports, Actions.View)]
    public async Task<IActionResult> Late([FromQuery] DateTime from, [FromQuery] DateTime to,
        [FromQuery] int? departmentId)
    {
        var data = await _svc.GetLateReportAsync(from, to, departmentId);
        return Ok(data);
    }

    [HttpGet("absent")]
    [SessionAuthorize(Modules.Reports, Actions.View)]
    public async Task<IActionResult> Absent([FromQuery] DateTime from, [FromQuery] DateTime to,
        [FromQuery] int? departmentId)
    {
        var data = await _svc.GetAbsentReportAsync(from, to, departmentId);
        return Ok(data);
    }

    [HttpGet("leave")]
    [SessionAuthorize(Modules.Reports, Actions.View)]
    public async Task<IActionResult> Leave([FromQuery] DateTime from, [FromQuery] DateTime to,
        [FromQuery] int? departmentId)
    {
        var data = await _svc.GetLeaveReportAsync(from, to, departmentId);
        return Ok(data);
    }

    [HttpGet("employees")]
    [SessionAuthorize(Modules.Reports, Actions.View)]
    public async Task<IActionResult> Employees([FromQuery] int? departmentId)
    {
        var data = await _svc.GetEmployeeListReportAsync(departmentId);
        return Ok(data);
    }
}
