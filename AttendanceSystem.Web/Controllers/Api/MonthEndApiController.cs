using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Web.Filters;
using Modules = AttendanceSystem.Common.Constants.AppConstants.Modules;
using Actions = AttendanceSystem.Common.Constants.AppConstants.Actions;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace AttendanceSystem.Web.Controllers.Api;

/// <summary>
/// Month-end close and the payroll hand-off.
///
/// Reading the status and the figures needs Attendance.View; closing the month needs
/// Attendance.Delete — the strongest attendance permission — because it stops everyone
/// else editing a whole month's records.
/// </summary>
[Route("api/month-end")]
[SessionAuthorize]
public class MonthEndApiController : ApiControllerBase
{
    private readonly IMonthEndService _svc;
    public MonthEndApiController(IMonthEndService svc) => _svc = svc;

    [HttpGet("status")]
    [SessionAuthorize(Modules.Attendance, Actions.View)]
    public async Task<IActionResult> Status([FromQuery] int month, [FromQuery] int year)
    {
        var r = await _svc.GetStatusAsync(month, year);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpGet("payroll")]
    [SessionAuthorize(Modules.Attendance, Actions.View)]
    public async Task<IActionResult> Payroll([FromQuery] int month, [FromQuery] int year)
    {
        var r = await _svc.GetPayrollAsync(month, year);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpPost("close")]
    [SessionAuthorize(Modules.Attendance, Actions.Delete)]
    public async Task<IActionResult> Close([FromBody] CloseMonthDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var r = await _svc.CloseMonthAsync(dto);
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }

    /// <summary>
    /// The payroll file. Built server-side rather than in the browser so it contains every
    /// employee rather than the page currently on screen.
    /// </summary>
    [HttpGet("payroll.csv")]
    [SessionAuthorize(Modules.Attendance, Actions.View)]
    public async Task<IActionResult> PayrollCsv([FromQuery] int month, [FromQuery] int year)
    {
        var r = await _svc.GetPayrollAsync(month, year);
        if (!r.IsSuccess) return BadRequest(r.ErrorMessage);

        var d = r.Data!;
        var sb = new StringBuilder();

        // Carried in the file itself: a spreadsheet outlives the screen it came from, and
        // "was this month closed when it was exported" is the first thing anyone asks of a
        // payroll figure that turns out to be wrong.
        sb.AppendLine($"# Payroll export,{d.PeriodDisplay},Generated,{d.GeneratedAt:yyyy-MM-dd HH:mm}," +
                      $"Period closed,{(d.IsClosed ? "Yes" : "NO - figures may still change")}");

        sb.AppendLine("Employee Code,Employee Name,Department,Designation,Total Days,Working Days," +
                      "Present Days,Absent Days,Leave Days,Holidays,Late Days,Working Hours," +
                      "Approved OT Hours,Premium OT Hours,Attendance %");

        foreach (var row in d.Rows)
        {
            sb.AppendLine(string.Join(',',
                Csv(row.EmployeeCode), Csv(row.EmployeeName), Csv(row.Department), Csv(row.Designation),
                row.TotalDays, row.WorkingDays, row.PresentDays, row.AbsentDays, row.LeaveDays,
                row.HolidayDays, row.LateDays, row.WorkingHours,
                row.ApprovedOtHours, row.PremiumOtHours, row.AttendancePercentage));
        }

        // UTF-8 BOM so Excel reads non-ASCII names correctly rather than as mojibake.
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", $"payroll-{d.Year}-{d.Month:00}.csv");
    }

    /// <summary>
    /// Quotes a field, and neutralises anything Excel would execute.
    ///
    /// A name beginning = + - or @ is treated as a formula by Excel, which turns an exported
    /// employee name into code running on the payroll clerk's machine.
    /// </summary>
    private static string Csv(string? value)
    {
        var v = value ?? string.Empty;
        if (v.Length > 0 && (v[0] is '=' or '+' or '-' or '@')) v = "'" + v;
        return "\"" + v.Replace("\"", "\"\"") + "\"";
    }
}
