using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceSystem.Web.Controllers;

public class AuthController : BaseController
{
    private readonly IAuthService  _auth;
    private readonly IUserService  _users;

    public AuthController(IAuthService auth, IUserService users)
    {
        _auth  = auth;
        _users = users;
    }

    [HttpGet]
    public IActionResult Login()
    {
        if (IsAuthenticated) return RedirectToAction("Index", "Admin");
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        if (!ModelState.IsValid) return View(dto);

        var result = await _auth.LoginAsync(dto);
        if (!result.IsSuccess)
        {
            ModelState.AddModelError("", result.ErrorMessage ?? "Invalid username or password.");
            return View(dto);
        }

        var user = result.Data!;
        HttpContext.Session.SetInt32(SessionUserId,   user.Id);
        HttpContext.Session.SetString(SessionUsername, user.Username);
        HttpContext.Session.SetString(SessionFullName, user.FullName);
        HttpContext.Session.SetInt32(SessionRoleId,   user.RoleId);
        HttpContext.Session.SetString(SessionEmail,   user.Email ?? "");
        HttpContext.Session.SetString(SessionRoleName, user.RoleName ?? "");

        return RedirectToAction("Index", "Admin");
    }

    [HttpGet]
    public async Task<IActionResult> LogoutGet()
    {
        return await Logout();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        if (CurrentUserId.HasValue)
            await _auth.LogoutAsync(CurrentUserId.Value);
        HttpContext.Session.Clear();
        return RedirectToAction("Login");
    }

    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var auth = RequireAuth();
        if (auth != null) return auth;

        var result = await _users.GetByIdAsync(CurrentUserId!.Value);
        if (!result.IsSuccess)
            return RedirectToAction("Index", "Admin");

        return View(result.Data);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile(string fullName, string email)
    {
        var auth = RequireAuth();
        if (auth != null) return auth;

        if (string.IsNullOrWhiteSpace(fullName))
        {
            TempData["Error"] = "Full name cannot be empty.";
            return RedirectToAction("Profile");
        }

        var userResult = await _users.GetByIdAsync(CurrentUserId!.Value);
        if (!userResult.IsSuccess || userResult.Data == null)
            return RedirectToAction("Index", "Admin");

        var user = userResult.Data;
        var updateDto = new UpdateUserDto
        {
            Id = user.Id,
            FullName = fullName.Trim(),
            Email = email?.Trim() ?? string.Empty,
            RoleId = user.RoleId,
            EmployeeId = user.EmployeeId,
            IsActive = user.IsActive
        };

        var result = await _users.UpdateAsync(updateDto);
        if (!result.IsSuccess)
        {
            TempData["Error"] = result.ErrorMessage ?? "Failed to update profile details.";
            return RedirectToAction("Profile");
        }

        // Update active session data
        HttpContext.Session.SetString(SessionFullName, updateDto.FullName);
        HttpContext.Session.SetString(SessionEmail, updateDto.Email);

        TempData["Success"] = "Profile details updated successfully.";
        return RedirectToAction("Profile");
    }

    [HttpGet]
    public IActionResult ChangePassword()
    {
        var auth = RequireAuth();
        if (auth != null) return auth;
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordDto dto)
    {
        var auth = RequireAuth();
        if (auth != null) return auth;
        if (!ModelState.IsValid) return View("Profile", (await _users.GetByIdAsync(CurrentUserId!.Value)).Data);

        var result = await _auth.ChangePasswordAsync(CurrentUserId!.Value, dto);
        if (!result.IsSuccess)
        {
            TempData["Error"] = result.ErrorMessage ?? "Failed to change password.";
            return View("Profile", (await _users.GetByIdAsync(CurrentUserId!.Value)).Data);
        }

        TempData["Success"] = "Password changed successfully.";
        return RedirectToAction("Profile");
    }
}
