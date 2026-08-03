using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Web.Filters;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceSystem.Web.Controllers.Api;

[Route("api/users")]
[ApiController]
[SessionAuthorize]
public class UsersApiController : ControllerBase
{
    private readonly IUserService _users;
    private readonly IRoleService _roles;

    public UsersApiController(IUserService users, IRoleService roles)
    {
        _users = users;
        _roles = roles;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var r = await _users.GetAllAsync();
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var r = await _users.GetByIdAsync(id);
        return r.IsSuccess ? Ok(r.Data) : NotFound(r.ErrorMessage);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var r = await _users.CreateAsync(dto);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateUserDto dto)
    {
        dto.Id = id;
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var r = await _users.UpdateAsync(dto);
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var currentUserId = HttpContext.Session.GetInt32("UserId") ?? 1;
        var r = await _users.DeleteAsync(id, currentUserId);
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }

    [HttpPost("{id}/reset-password")]
    public async Task<IActionResult> ResetPassword(int id, [FromBody] ResetPasswordRequest req)
    {
        var currentUserId = HttpContext.Session.GetInt32("UserId") ?? 1;
        var r = await _users.ResetPasswordAsync(id, req.NewPassword, currentUserId);
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }

    [HttpPost("{id}/lock")]
    public async Task<IActionResult> Lock(int id)
    {
        var r = await _users.LockAsync(id);
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }

    [HttpPost("{id}/unlock")]
    public async Task<IActionResult> Unlock(int id)
    {
        var r = await _users.UnlockAsync(id);
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }

    [HttpGet("roles")]
    public async Task<IActionResult> GetRoles()
    {
        var r = await _roles.GetAllAsync();
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }
}

public record ResetPasswordRequest(string NewPassword);
