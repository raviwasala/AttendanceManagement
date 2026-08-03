using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceSystem.Web.Controllers;

public class AuthController : BaseController
{
    private readonly IAuthService _auth;
    private readonly IUserService _users;

    public AuthController(IAuthService auth, IUserService users)
    {
        _auth = auth;
        _users = users;
    }

    [HttpGet]
    public async Task<IActionResult> Login()
    {
        if (IsAuthenticated) return RedirectToAction("Index", "Admin");

        var model = new LoginDto("", "");

        if (Request.Cookies.TryGetValue("Auth_RememberUser", out var savedUser) &&
            Request.Cookies.TryGetValue("Auth_RememberToken", out var savedToken) &&
            !string.IsNullOrEmpty(savedUser) && !string.IsNullOrEmpty(savedToken))
        {
            var result = await _auth.ValidateRememberTokenAsync(savedUser, savedToken);
            if (result.IsSuccess)
            {
                var user = result.Data!;
                HttpContext.Session.SetInt32(SessionUserId, user.Id);
                HttpContext.Session.SetString(SessionUsername, user.Username);
                HttpContext.Session.SetString(SessionFullName, user.FullName);
                HttpContext.Session.SetInt32(SessionRoleId, user.RoleId);
                HttpContext.Session.SetString(SessionEmail, user.Email ?? "");
                HttpContext.Session.SetString(SessionRoleName, user.RoleName ?? "");

                return RedirectToAction("Index", "Admin");
            }

            model = new LoginDto(savedUser, "", true);
        }

        return View(model);
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
        HttpContext.Session.SetInt32(SessionUserId, user.Id);
        HttpContext.Session.SetString(SessionUsername, user.Username);
        HttpContext.Session.SetString(SessionFullName, user.FullName);
        HttpContext.Session.SetInt32(SessionRoleId, user.RoleId);
        HttpContext.Session.SetString(SessionEmail, user.Email ?? "");
        HttpContext.Session.SetString(SessionRoleName, user.RoleName ?? "");

        if (dto.RememberMe)
        {
            var cookieOptions = new CookieOptions
            {
                Expires = DateTime.UtcNow.AddDays(30),
                HttpOnly = true,
                IsEssential = true,
                SameSite = SameSiteMode.Lax
            };
            if (!string.IsNullOrEmpty(user.Username))
                Response.Cookies.Append("Auth_RememberUser", user.Username, cookieOptions);

            var rememberToken = user.RememberToken ?? Guid.NewGuid().ToString("N");
            Response.Cookies.Append("Auth_RememberToken", rememberToken, cookieOptions);
        }
        else
        {
            Response.Cookies.Delete("Auth_RememberUser");
            Response.Cookies.Delete("Auth_RememberToken");
        }

        return RedirectToAction("Index", "Admin");
    }

    [HttpGet]
    public IActionResult ForgotPassword()
    {
        if (IsAuthenticated) return RedirectToAction("Index", "Admin");
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordDto dto)
    {
        if (IsAuthenticated) return RedirectToAction("Index", "Admin");

        if (string.IsNullOrWhiteSpace(dto.Email))
        {
            ModelState.AddModelError("Email", "Please enter your registered email address or username.");
            return View(dto);
        }

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var result = await _auth.RequestPasswordResetAsync(dto, baseUrl);
        if (!result.IsSuccess)
        {
            ModelState.AddModelError("", result.ErrorMessage ?? "Failed to request password reset.");
            return View(dto);
        }

        ViewBag.SuccessMessage = "If an account matching that email address exists, a password reset link has been sent. Please check your inbox and follow the instructions.";
        return View();
    }

    [HttpGet]
    public IActionResult ResetPassword(string? email, string? token)
    {
        if (IsAuthenticated) return RedirectToAction("Index", "Admin");

        var model = new ResetPasswordWithTokenDto(email ?? "", token ?? "", "", "");
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordWithTokenDto dto)
    {
        if (IsAuthenticated) return RedirectToAction("Index", "Admin");

        if (!ModelState.IsValid) return View(dto);

        var result = await _auth.ResetPasswordWithTokenAsync(dto);
        if (!result.IsSuccess)
        {
            ModelState.AddModelError("", result.ErrorMessage ?? "Failed to reset password.");
            return View(dto);
        }

        ViewBag.SuccessMessage = "Your password has been reset successfully! You can now sign in with your new password.";
        return View();
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
        Response.Cookies.Delete("Auth_RememberUser");
        Response.Cookies.Delete("Auth_RememberToken");
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
