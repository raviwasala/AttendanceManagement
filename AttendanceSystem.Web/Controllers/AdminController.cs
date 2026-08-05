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

    // Company-wide attendance is gated on Employees.View, not Attendance.View.
    //
    // Attendance.View is granted to the Employee role so staff can see *their own* records
    // via /Me/Attendance. Using it here let any employee open the whole company's attendance.
    // Employees.View is the permission that already means "may see other people's records",
    // and every role except Employee holds it — so this closes the gap without taking
    // anything away from Manager, Supervisor or HR.
    [HttpGet("Attendance")]
    [SessionAuthorize(Modules.Employees, Actions.View)]
    public IActionResult Attendance() => View();

    [HttpGet("AttendanceReview")]
    [SessionAuthorize(Modules.Employees, Actions.View)]
    public IActionResult AttendanceReview() => View();

    // Overtime rules are policy: viewing the screen needs Overtime.View like the rest, but
    // every write on it is guarded by Overtime.Edit on the API.
    [HttpGet("OvertimeRules")]
    [SessionAuthorize(Modules.Overtime, Actions.View)]
    public IActionResult OvertimeRules() => View();

    [HttpGet("OvertimeApproval")]
    [SessionAuthorize(Modules.Overtime, Actions.View)]
    public IActionResult OvertimeApproval() => View();

    [HttpGet("OvertimeRegister")]
    [SessionAuthorize(Modules.Overtime, Actions.View)]
    public IActionResult OvertimeRegister() => View();

    [HttpGet("OvertimeSummary")]
    [SessionAuthorize(Modules.Overtime, Actions.View)]
    public IActionResult OvertimeSummary() => View();

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

    /// <summary>
    /// One employee's full record: details, service, history, documents and this month's
    /// attendance. The edit modal was previously the only way to look at a person, which
    /// meant opening a form to read a phone number.
    /// </summary>
    [HttpGet("EmployeeProfile/{id:int}")]
    [SessionAuthorize(Modules.Employees, Actions.View)]
    public IActionResult EmployeeProfile(int id)
    {
        ViewBag.EmployeeId = id;
        return View();
    }

    [HttpGet("Settings")]
    [SessionAuthorize(Modules.Settings, Actions.View)]
    public IActionResult Settings() => View();

    /// <summary>
    /// Exports, backup, restore and bulk employee import.
    ///
    /// Gated on Settings.View to reach the page; each action on it carries its own permission
    /// server-side. Settings deliberately, rather than a new Data module: adding a module to
    /// PermissionCatalogue only seeds into a new database, so on an existing one the rows
    /// would not exist and the page would 403 for everybody — which is what happened when the
    /// Import module was added.
    /// </summary>
    [HttpGet("DataManagement")]
    [SessionAuthorize(Modules.Settings, Actions.View)]
    public IActionResult DataManagement() => View();

    [HttpGet("AuditLogs")]
    [SessionAuthorize(Modules.AuditLogs, Actions.View)]
    public IActionResult AuditLogs() => View();
}
