using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Web.Filters;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceSystem.Web.Controllers.Api;

[Route("api/roles")]
[ApiController]
[SessionAuthorize]
public class RolesApiController : ControllerBase
{
    private readonly IRoleService _roles;

    public RolesApiController(IRoleService roles)
    {
        _roles = roles;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var r = await _roles.GetAllAsync();
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var r = await _roles.GetByIdAsync(id);
        return r.IsSuccess ? Ok(r.Data) : NotFound(r.ErrorMessage);
    }

    [HttpPost]
    public async Task<IActionResult> Save([FromBody] RoleDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var r = await _roles.SaveAsync(dto);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var r = await _roles.DeleteAsync(id);
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }

    [HttpGet("{id}/permissions")]
    public async Task<IActionResult> GetPermissions(int id)
    {
        var r = await _roles.GetPermissionsForRoleAsync(id);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpPost("{id}/permissions")]
    public async Task<IActionResult> SavePermissions(int id, [FromBody] SaveRolePermissionsRequest req)
    {
        var r = await _roles.SavePermissionsAsync(id, req.PermissionIds ?? new List<int>());
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }
}

public record SaveRolePermissionsRequest(List<int> PermissionIds);
