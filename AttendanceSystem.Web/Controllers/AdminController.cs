using AttendanceSystem.Common.Constants;
using AttendanceSystem.Web.Filters;
using Microsoft.AspNetCore.Mvc;
using Modules = AttendanceSystem.Common.Constants.AppConstants.Modules;
using Actions = AttendanceSystem.Common.Constants.AppConstants.Actions;

namespace AttendanceSystem.Web.Controllers;

/// <summary>
/// Serves the admin pages. Each page requires the View permission for the module it shows;
/// the data behind them is separately guarded on the API controllers, since hiding a page
/// does nothing to stop a direct call to the endpoint it reads from.
/// </summary>
[Route("Admin")]
[ApiExplorerSettings(IgnoreApi = true)]
public class AdminController : BaseController
{
    private readonly ILogger<AdminController> _logger;

    public AdminController(ILogger<AdminController> logger) => _logger = logger;

    [HttpGet("")] [HttpGet("Index")]
    [SessionAuthorize(Modules.Dashboard, Actions.View)]
    public IActionResult Index() => View();

    [HttpGet("Departments")]
    [SessionAuthorize(Modules.Departments, Actions.View)]
    public IActionResult Departments() => View();

    [HttpGet("Designations")]
    [SessionAuthorize(Modules.Designations, Actions.View)]
    public IActionResult Designations() => View();

    [HttpGet("Branches")]
    [SessionAuthorize(Modules.Branches, Actions.View)]
    public IActionResult Branches() => View();

    [HttpGet("Shifts")]
    [SessionAuthorize(Modules.Shifts, Actions.View)]
    public IActionResult Shifts() => View();

    [HttpGet("ShiftRoster")]
    [SessionAuthorize(Modules.Shifts, Actions.View)]
    public IActionResult ShiftRoster() => View();

    [HttpGet("Employees")]
    [SessionAuthorize(Modules.Employees, Actions.View)]
    public IActionResult Employees() => View();

    [HttpGet("Attendance")]
    [SessionAuthorize(Modules.Attendance, Actions.View)]
    public IActionResult Attendance() => View();

    [HttpGet("AttendanceReview")]
    [SessionAuthorize(Modules.Attendance, Actions.View)]
    public IActionResult AttendanceReview() => View();

    [HttpGet("Leave")]
    [SessionAuthorize(Modules.Leave, Actions.View)]
    public IActionResult Leave() => View();

    [HttpGet("Holidays")]
    [SessionAuthorize(Modules.Holidays, Actions.View)]
    public IActionResult Holidays() => View();

    [HttpGet("Users")]
    [SessionAuthorize(Modules.Users, Actions.View)]
    public IActionResult Users() => View();

    [HttpGet("Roles")]
    [SessionAuthorize(Modules.Roles, Actions.View)]
    public IActionResult Roles() => View();

    [HttpGet("Reports")]
    [SessionAuthorize(Modules.Reports, Actions.View)]
    public IActionResult Reports() => View();

    [HttpGet("Import")]
    [SessionAuthorize(Modules.Import, Actions.View)]
    public IActionResult Import() => View();

    [HttpGet("Devices")]
    [SessionAuthorize(Modules.Devices, Actions.View)]
    public IActionResult Devices() => View();

    [HttpGet("Settings")]
    [SessionAuthorize(Modules.Settings, Actions.View)]
    public IActionResult Settings() => View();
}
