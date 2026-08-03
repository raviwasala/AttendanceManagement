using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Web.Filters;
using Microsoft.AspNetCore.Mvc;
using Modules = AttendanceSystem.Common.Constants.AppConstants.Modules;
using Actions = AttendanceSystem.Common.Constants.AppConstants.Actions;

namespace AttendanceSystem.Web.Controllers.Api;

/// <summary>
/// Dashboard analytics. Read-only, and each endpoint requires View on the module whose data
/// it exposes — punctuality reveals attendance, so it is gated on Attendance, not Dashboard.
/// </summary>
[Route("api/analytics")]
[SessionAuthorize]
public class AnalyticsApiController : ApiControllerBase
{
    private readonly IAnalyticsService _analytics;

    public AnalyticsApiController(IAnalyticsService analytics) => _analytics = analytics;

    [HttpGet("attendance-trend")]
    [SessionAuthorize(Modules.Attendance, Actions.View)]
    public async Task<IActionResult> AttendanceTrend([FromQuery] int days = 7)
    {
        var r = await _analytics.GetAttendanceTrendAsync(days);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    // Deliberately gated on Employees.View, not Attendance.View. This returns named
    // individuals ranked by lateness; Attendance.View is granted to the Employee role so
    // staff can see their own records, and it should not also expose a leaderboard of
    // colleagues' punctuality. Reviewing named performance data is an HR function.
    [HttpGet("punctuality")]
    [SessionAuthorize(Modules.Employees, Actions.View)]
    public async Task<IActionResult> Punctuality(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int top = 10)
    {
        // Default to the last 30 days so the panel is useful without any query string.
        var toDate = to ?? DateTime.Today;
        var fromDate = from ?? toDate.AddDays(-29);

        var r = await _analytics.GetPunctualityAsync(fromDate, toDate, top);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpGet("leave-overview")]
    [SessionAuthorize(Modules.Leave, Actions.View)]
    public async Task<IActionResult> LeaveOverview()
    {
        var r = await _analytics.GetLeaveOverviewAsync();
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpGet("operations")]
    [SessionAuthorize(Modules.Employees, Actions.View)]
    public async Task<IActionResult> Operations([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var toDate = to ?? DateTime.Today;
        var fromDate = from ?? toDate.AddDays(-29);

        var r = await _analytics.GetOperationsHealthAsync(fromDate, toDate);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }
}
