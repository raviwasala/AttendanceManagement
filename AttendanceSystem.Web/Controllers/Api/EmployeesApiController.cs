using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Common.Models;
using AttendanceSystem.Web.Filters;
using Modules = AttendanceSystem.Common.Constants.AppConstants.Modules;
using Actions = AttendanceSystem.Common.Constants.AppConstants.Actions;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceSystem.Web.Controllers.Api;

[Route("api/employees")]
[SessionAuthorize]
public class EmployeesApiController : ApiControllerBase
{
    private readonly IEmployeeService _svc;
    public EmployeesApiController(IEmployeeService svc) => _svc = svc;

    /// <summary>
    /// Every active employee, unpaged. Still here because a dozen screens fill an employee
    /// dropdown from it and a dropdown genuinely wants the whole list.
    /// </summary>
    [HttpGet]
    [SessionAuthorize(Modules.Employees, Actions.View)]
    public async Task<IActionResult> GetAll()
    {
        var r = await _svc.GetAllAsync();
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    /// <summary>One page of employees, filtered and searched in the database.</summary>
    [HttpGet("paged")]
    [SessionAuthorize(Modules.Employees, Actions.View)]
    public async Task<IActionResult> GetPaged(
        [FromQuery] string? search,
        [FromQuery] int? departmentId, [FromQuery] int? designationId, [FromQuery] int? branchId,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PageRequest.DefaultPageSize)
    {
        var r = await _svc.GetPagedAsync(search, departmentId, designationId, branchId, isActive,
            new PageRequest { Page = page, PageSize = pageSize });

        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpGet("{id}")]
    [SessionAuthorize(Modules.Employees, Actions.View)]
    public async Task<IActionResult> GetById(int id)
    {
        var r = await _svc.GetByIdAsync(id);
        return r.IsSuccess ? Ok(r.Data) : NotFound(r.ErrorMessage);
    }

    [HttpGet("search")]
    [SessionAuthorize(Modules.Employees, Actions.View)]
    public async Task<IActionResult> Search([FromQuery] string q)
    {
        var r = await _svc.SearchAsync(q ?? "");
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpPost]
    [SessionAuthorize(Modules.Employees, Actions.Edit)]
    public async Task<IActionResult> Save([FromBody] SaveEmployeeDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var r = await _svc.SaveAsync(dto);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpDelete("{id}")]
    [SessionAuthorize(Modules.Employees, Actions.Delete)]
    public async Task<IActionResult> Delete(int id)
    {
        var r = await _svc.DeleteAsync(id, CurrentUserId);
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }

    [HttpPost("{id}/toggle")]
    [SessionAuthorize(Modules.Employees, Actions.Edit)]
    public async Task<IActionResult> Toggle(int id)
    {
        var r = await _svc.ToggleActiveAsync(id, CurrentUserId);
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }
}
