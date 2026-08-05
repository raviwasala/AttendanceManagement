using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Web.Filters;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceSystem.Web.Controllers;

/// <summary>
/// Employee self-service pages.
///
/// Guarded by <c>[SessionAuthorize]</c> alone — being signed in is the only requirement,
/// because every one of these screens shows the caller their own data and nobody else's.
/// Requiring Attendance.View here would be wrong: that permission means "see the company's
/// attendance", which is exactly what these pages are the alternative to.
/// </summary>
[Route("Me")]
[ApiExplorerSettings(IgnoreApi = true)]
[SessionAuthorize]
public class MeController : BaseController
{
    [HttpGet("")] [HttpGet("Attendance")]
    public IActionResult Attendance() => View();

    [HttpGet("Leave")]
    public IActionResult Leave() => View();
}
