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

    // Viewing the readiness checks and the payroll figures needs only Attendance.View;
    // closing the month is guarded separately on the API by Attendance.Delete, since it
    // stops everybody else editing a whole month.
    [HttpGet("MonthEnd")]
    [SessionAuthorize(Modules.Attendance, Actions.View)]
    public IActionResult MonthEnd() => View();

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

    /// <summary>
    /// Payroll setup per employee — grade, statutory numbers, bank details, and who is not
    /// yet ready to be paid.
    ///
    /// Payroll rather than PayrollSetup: this lists what named individuals are paid, which is
    /// a different thing from the salary structure.
    /// </summary>
    [HttpGet("EmployeePayroll")]
    [SessionAuthorize(Modules.Payroll, Actions.View)]
    public IActionResult EmployeePayroll() => View();

    /// <summary>
    /// Direct salary entry — pick an employee, set their basic, save.
    ///
    /// Deliberately narrow. It writes only the salary override, so a quick correction here
    /// cannot clear the bank account or statutory numbers that live on the profile.
    /// </summary>
    [HttpGet("SalaryDetails")]
    [SessionAuthorize(Modules.Payroll, Actions.View)]
    public IActionResult SalaryDetails() => View();

    /// <summary>
    /// Bank details for every employee in one grid. Collecting account numbers is a
    /// data-entry task done once for hundreds of people; a profile visit each turns half an
    /// hour into an afternoon.
    /// </summary>
    [HttpGet("EmployeeBankAccounts")]
    [SessionAuthorize(Modules.Payroll, Actions.View)]
    public IActionResult EmployeeBankAccounts() => View();

    /// <summary>Staff loans — granting, recovery and early settlement.</summary>
    [HttpGet("Loans")]
    [SessionAuthorize(Modules.Payroll, Actions.View)]
    public IActionResult Loans() => View();

    /// <summary>Raising basic salary, for one employee or a whole department or grade.</summary>
    [HttpGet("SalaryIncrements")]
    [SessionAuthorize(Modules.Payroll, Actions.View)]
    public IActionResult SalaryIncrements() => View();

    /// <summary>Proposed increments awaiting approval. Confirming here is what changes pay.</summary>
    [HttpGet("IncrementConfirmation")]
    [SessionAuthorize(Modules.Payroll, Actions.View)]
    public IActionResult IncrementConfirmation() => View();

    /// <summary>Every employee's pay for a month on one sheet — checked before money moves.</summary>
    [HttpGet("PayRegister")]
    [SessionAuthorize(Modules.Payroll, Actions.View)]
    public IActionResult PayRegister() => View();

    /// <summary>One employee's payslip, as it was stored.</summary>
    [HttpGet("Payslip")]
    [SessionAuthorize(Modules.Payroll, Actions.View)]
    public IActionResult Payslip() => View();

    /// <summary>Department totals and the journal they post to.</summary>
    [HttpGet("PaySummary")]
    [SessionAuthorize(Modules.Payroll, Actions.View)]
    public IActionResult PaySummary() => View();

    /// <summary>The month payroll is working on, and the history of closed months.</summary>
    [HttpGet("PayrollPeriods")]
    [SessionAuthorize(Modules.Payroll, Actions.View)]
    public IActionResult PayrollPeriods() => View();

    /// <summary>One code, one month, an amount per employee — this month's one-offs.</summary>
    [HttpGet("ItemWiseTransaction")]
    [SessionAuthorize(Modules.Payroll, Actions.View)]
    public IActionResult ItemWiseTransaction() => View();

    /// <summary>One employee, one month, every one-off code — the item-wise grid transposed.</summary>
    [HttpGet("EmployeeWiseTransaction")]
    [SessionAuthorize(Modules.Payroll, Actions.View)]
    public IActionResult EmployeeWiseTransaction() => View();

    /// <summary>One employee's scheduled allowances and deductions, bounded by month.</summary>
    [HttpGet("TransactionSchedule")]
    [SessionAuthorize(Modules.Payroll, Actions.View)]
    public IActionResult TransactionSchedule() => View();

    /// <summary>Settling a loan early — its own screen, mirroring the old system.</summary>
    [HttpGet("LoanSettlement")]
    [SessionAuthorize(Modules.Payroll, Actions.View)]
    public IActionResult LoanSettlement() => View();

    /// <summary>Gives one allowance or deduction the same value across many employees.</summary>
    [HttpGet("GroupAssignComponent")]
    [SessionAuthorize(Modules.Payroll, Actions.View)]
    public IActionResult GroupAssignComponent() => View();

    /// <summary>
    /// Corrections and exceptions — EPF adjustments, payroll suspensions, code changes.
    ///
    /// Three small screens on one page. Each is used rarely and none earns its own sidebar
    /// entry, but together they are what a clerk reaches for when something is wrong.
    /// </summary>
    [HttpGet("PayrollAdmin")]
    [SessionAuthorize(Modules.Payroll, Actions.View)]
    public IActionResult PayrollAdmin() => View();

    /// <summary>
    /// Payroll master data — grades, allowances, banks, statutory rates.
    ///
    /// PayrollSetup rather than Payroll: configuring the salary structure and seeing what an
    /// individual is paid are different jobs, usually held by different people.
    /// </summary>
    [HttpGet("PayrollSetup")]
    [SessionAuthorize(Modules.PayrollSetup, Actions.View)]
    public IActionResult PayrollSetup() => View();

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
