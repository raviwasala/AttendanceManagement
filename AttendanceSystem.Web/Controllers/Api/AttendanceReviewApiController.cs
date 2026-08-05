using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Web.Filters;
using Microsoft.AspNetCore.Mvc;
using Modules = AttendanceSystem.Common.Constants.AppConstants.Modules;
using Actions = AttendanceSystem.Common.Constants.AppConstants.Actions;

namespace AttendanceSystem.Web.Controllers.Api;

/// <summary>
/// Daily attendance review — rostered shift against what the device recorded, with the
/// in/out times correctable.
///
/// Saving requires Attendance.Edit rather than Create: correcting a punch changes what an
/// employee is paid for, so it is an edit even when it creates the first record for a day.
/// </summary>
[Route("api/attendance-review")]
[SessionAuthorize]
public class AttendanceReviewApiController : ApiControllerBase
{
    private readonly IAttendanceReviewService _review;

    public AttendanceReviewApiController(IAttendanceReviewService review) => _review = review;

    // Employees.View, not Attendance.View — see the note on AdminController.Attendance.
    // This returns every employee's times; Attendance.View is what lets staff see their own.
    [HttpGet]
    [SessionAuthorize(Modules.Employees, Actions.View)]
    public async Task<IActionResult> Get(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int? employeeId, [FromQuery] int? departmentId,
        [FromQuery] DateTime? date)
    {
        // `date` is kept for callers that only want a single day; from/to take precedence.
        var start = from ?? date ?? DateTime.Today;
        var end = to ?? date ?? start;

        var r = await _review.GetReviewAsync(start, end, employeeId, departmentId);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpPost("entry")]
    [SessionAuthorize(Modules.Attendance, Actions.Edit)]
    public async Task<IActionResult> SaveEntry([FromBody] SaveAttendanceEntryDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var r = await _review.SaveEntryAsync(dto);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }
}
