using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Web.Filters;
using Modules = AttendanceSystem.Common.Constants.AppConstants.Modules;
using Actions = AttendanceSystem.Common.Constants.AppConstants.Actions;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceSystem.Web.Controllers.Api;

[Route("api/departments")]
[SessionAuthorize]
public class DepartmentsApiController : ApiControllerBase
{
    private readonly IDepartmentService _svc;
    public DepartmentsApiController(IDepartmentService svc) => _svc = svc;

    [HttpGet]
    [SessionAuthorize(Modules.Departments, Actions.View)]
    public async Task<IActionResult> GetAll()
    {
        var r = await _svc.GetAllAsync();
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpGet("{id}")]
    [SessionAuthorize(Modules.Departments, Actions.View)]
    public async Task<IActionResult> GetById(int id)
    {
        var r = await _svc.GetByIdAsync(id);
        return r.IsSuccess ? Ok(r.Data) : NotFound(r.ErrorMessage);
    }

    [HttpGet("search")]
    [SessionAuthorize(Modules.Departments, Actions.View)]
    public async Task<IActionResult> Search([FromQuery] string q)
    {
        var r = await _svc.SearchAsync(q ?? "");
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpPost]
    [SessionAuthorize(Modules.Departments, Actions.Edit)]
    public async Task<IActionResult> Save([FromBody] SaveDepartmentDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var r = await _svc.SaveAsync(dto);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpDelete("{id}")]
    [SessionAuthorize(Modules.Departments, Actions.Delete)]
    public async Task<IActionResult> Delete(int id)
    {
        var r = await _svc.DeleteAsync(id, CurrentUserId);
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }

    // ── Approvers ─────────────────────────────────────────────────────────────
    //
    // Departments.Edit, not Leave.Approve: this decides *who* may approve, which is
    // administering the organisation rather than approving anything. Someone who can approve
    // leave should not be able to widen their own scope.

    [HttpGet("{id:int}/approvers")]
    [SessionAuthorize(Modules.Departments, Actions.View)]
    public async Task<IActionResult> Approvers(int id)
    {
        var r = await _svc.GetApproversAsync(id);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpPost("approvers")]
    [SessionAuthorize(Modules.Departments, Actions.Edit)]
    public async Task<IActionResult> AddApprover([FromBody] SaveDepartmentApproverDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var r = await _svc.AddApproverAsync(dto);
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }

    [HttpDelete("approvers/{approverId:int}")]
    [SessionAuthorize(Modules.Departments, Actions.Edit)]
    public async Task<IActionResult> RemoveApprover(int approverId)
    {
        var r = await _svc.RemoveApproverAsync(approverId);
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }
}
