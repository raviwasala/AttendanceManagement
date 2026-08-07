using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Web.Filters;
using Modules = AttendanceSystem.Common.Constants.AppConstants.Modules;
using Actions = AttendanceSystem.Common.Constants.AppConstants.Actions;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceSystem.Web.Controllers.Api;

/// <summary>
/// One employee's payroll record.
///
/// Payroll.View to read and Payroll.Edit to change — not the PayrollSetup permissions. A
/// grade assignment reveals what a named person earns, which is a different thing from the
/// salary structure itself.
/// </summary>
[Route("api/employee-payroll")]
[SessionAuthorize]
public class EmployeePayrollApiController : ApiControllerBase
{
    private readonly IEmployeePayrollService _svc;
    public EmployeePayrollApiController(IEmployeePayrollService svc) => _svc = svc;

    [HttpGet("list")]
    [SessionAuthorize(Modules.Payroll, Actions.View)]
    public async Task<IActionResult> List([FromQuery] string? search, [FromQuery] int? departmentId,
                                          [FromQuery] bool? readyOnly)
    {
        var r = await _svc.GetListAsync(search, departmentId, readyOnly);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    // ── Bulk operations ───────────────────────────────────────────────────────

    [HttpGet("bank-rows")]
    [SessionAuthorize(Modules.Payroll, Actions.View)]
    public async Task<IActionResult> BankRows([FromQuery] string? search,
        [FromQuery] int? departmentId, [FromQuery] bool? incompleteOnly)
    {
        var r = await _svc.GetBankRowsAsync(search, departmentId, incompleteOnly);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpPost("bank-rows")]
    [SessionAuthorize(Modules.Payroll, Actions.Edit)]
    public async Task<IActionResult> SaveBankRow([FromBody] SaveEmployeeBankRowDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var r = await _svc.SaveBankRowAsync(dto);
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }

    [HttpGet("common-value/count")]
    [SessionAuthorize(Modules.Payroll, Actions.View)]
    public async Task<IActionResult> CommonValueCount([FromQuery] int componentId,
        [FromQuery] AttendanceSystem.Domain.Enums.CommonValueScope scope)
    {
        var r = await _svc.CountForScopeAsync(componentId, scope);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpPost("common-value")]
    [SessionAuthorize(Modules.Payroll, Actions.Edit)]
    public async Task<IActionResult> CommonValue([FromBody] CommonValueEntryDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var r = await _svc.ApplyCommonValueAsync(dto);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpPost("bulk-component")]
    [SessionAuthorize(Modules.Payroll, Actions.Edit)]
    public async Task<IActionResult> BulkComponent([FromBody] BulkAssignComponentDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var r = await _svc.BulkAssignComponentAsync(dto);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    // ── Transaction schedule ──────────────────────────────────────────────────

    [HttpGet("schedule/{employeeId:int}")]
    [SessionAuthorize(Modules.Payroll, Actions.View)]
    public async Task<IActionResult> Schedule(int employeeId)
    {
        var r = await _svc.GetScheduleAsync(employeeId);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpPost("schedule")]
    [SessionAuthorize(Modules.Payroll, Actions.Edit)]
    public async Task<IActionResult> SaveSchedule([FromBody] SaveTransactionScheduleRowDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var r = await _svc.SaveScheduleRowAsync(dto);
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }

    [HttpDelete("schedule/{id:int}")]
    [SessionAuthorize(Modules.Payroll, Actions.Edit)]
    public async Task<IActionResult> DeleteSchedule(int id)
    {
        var r = await _svc.DeleteScheduleRowAsync(id);
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }

    // ── EPF adjustments ───────────────────────────────────────────────────────

    [HttpGet("epf-adjustments")]
    [SessionAuthorize(Modules.Payroll, Actions.View)]
    public async Task<IActionResult> EpfAdjustments([FromQuery] int? year, [FromQuery] int? month)
    {
        var r = await _svc.GetEpfAdjustmentsAsync(year, month);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpPost("epf-adjustments")]
    [SessionAuthorize(Modules.Payroll, Actions.Edit)]
    public async Task<IActionResult> SaveEpfAdjustment([FromBody] SaveEpfAdjustmentDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var r = await _svc.SaveEpfAdjustmentAsync(dto);
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }

    [HttpDelete("epf-adjustments/{id:int}")]
    [SessionAuthorize(Modules.Payroll, Actions.Delete)]
    public async Task<IActionResult> DeleteEpfAdjustment(int id)
    {
        var r = await _svc.DeleteEpfAdjustmentAsync(id);
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }

    // ── Non-effective employees ───────────────────────────────────────────────

    [HttpGet("non-effective")]
    [SessionAuthorize(Modules.Payroll, Actions.View)]
    public async Task<IActionResult> NonEffective()
    {
        var r = await _svc.GetNonEffectiveAsync();
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpPost("suspend")]
    [SessionAuthorize(Modules.Payroll, Actions.Edit)]
    public async Task<IActionResult> Suspend([FromBody] SuspendEmployeeDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var r = await _svc.SuspendAsync(dto);
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }

    /// <summary>
    /// Renames an employee code. Employees.Edit rather than Payroll.Edit — the code is
    /// employee master data that payroll happens to print.
    /// </summary>
    [HttpPost("change-code")]
    [SessionAuthorize(Modules.Employees, Actions.Edit)]
    public async Task<IActionResult> ChangeCode([FromBody] ChangeEmployeeCodeDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var r = await _svc.ChangeEmployeeCodeAsync(dto);
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }

    [HttpGet("{employeeId:int}")]
    [SessionAuthorize(Modules.Payroll, Actions.View)]
    public async Task<IActionResult> Get(int employeeId)
    {
        var r = await _svc.GetAsync(employeeId);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpPost]
    [SessionAuthorize(Modules.Payroll, Actions.Edit)]
    public async Task<IActionResult> Save([FromBody] SaveEmployeePayrollInfoDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var r = await _svc.SaveAsync(dto);
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }

    /// <summary>The Salary Details screen — sets basic salary and nothing else.</summary>
    [HttpPost("salary")]
    [SessionAuthorize(Modules.Payroll, Actions.Edit)]
    public async Task<IActionResult> SaveSalary([FromBody] SaveEmployeeSalaryDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var r = await _svc.SaveSalaryAsync(dto);
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }

    [HttpGet("{employeeId:int}/components")]
    [SessionAuthorize(Modules.Payroll, Actions.View)]
    public async Task<IActionResult> Components(int employeeId)
    {
        var r = await _svc.GetComponentsAsync(employeeId);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpPost("components")]
    [SessionAuthorize(Modules.Payroll, Actions.Edit)]
    public async Task<IActionResult> SaveComponent([FromBody] SaveEmployeeComponentDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var r = await _svc.SaveComponentAsync(dto);
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }
}
