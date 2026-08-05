using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Web.Session;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceSystem.Web.Controllers;

public class AuthController : BaseController
{
    private const string RememberUserCookie = "Auth_RememberUser";
    private const string RememberTokenCookie = "Auth_RememberToken";

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

        if (Request.Cookies.TryGetValue(RememberUserCookie, out var savedUser) &&
            Request.Cookies.TryGetValue(RememberTokenCookie, out var savedToken) &&
            !string.IsNullOrEmpty(savedUser) && !string.IsNullOrEmpty(savedToken))
        {
            var result = await _auth.ValidateRememberTokenAsync(savedUser, savedToken);
            if (result.IsSuccess)
            {
                EstablishSession(result.Data!);
                return RedirectToAction("Index", "Admin");
            }

            // The token was rejected — clear it so a stale or stolen cookie is not
            // presented again on every subsequent visit.
            ClearRememberCookies();
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

        EstablishSession(result.Data!);
        return RedirectToAction("Index", "Admin");
    }

    /// <summary>Writes the authenticated identity into the session and refreshes the remember-me cookies.</summary>
    private void EstablishSession(AuthResultDto auth)
    {
        var user = auth.User;

        HttpSessionUserContext.SignIn(HttpContext, user.Id, user.Username, user.FullName,
            user.Email ?? string.Empty, user.RoleId, user.RoleName ?? string.Empty,
            user.EmployeeId, auth.Permissions);

        if (auth.RememberToken is not null)
        {
            var options = new CookieOptions
            {
                Expires = auth.RememberTokenExpiresAt ?? DateTime.UtcNow.AddDays(30),
                HttpOnly = true,
                IsEssential = true,
                // The token is a long-lived credential; never let it travel over plain HTTP.
                Secure = true,
                SameSite = SameSiteMode.Lax
            };

            Response.Cookies.Append(RememberUserCookie, user.Username, options);
            Response.Cookies.Append(RememberTokenCookie, auth.RememberToken, options);
        }
        else
        {
            ClearRememberCookies();
        }
    }

    private void ClearRememberCookies()
    {
        Response.Cookies.Delete(RememberUserCookie);
        Response.Cookies.Delete(RememberTokenCookie);
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();

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
    public async Task<IActionResult> LogoutGet() => await Logout();

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        if (CurrentUserId.HasValue)
        {
            await _auth.LogoutAsync(CurrentUserId.Value);
            // Signing out revokes the stored token too, so the cookie cannot resurrect
            // the session on the next visit.
            await _auth.RevokeRememberTokenAsync(CurrentUserId.Value);
        }

        HttpContext.Session.Clear();
        ClearRememberCookies();
        return RedirectToAction("Login");
    }

    // ── Screen lock ───────────────────────────────────────────────────────────
    //
    // The lock lives in the session, so it survives a refresh, a typed URL and a direct API
    // call. SessionAuthorizeAttribute refuses everything while it is set, and exempts only
    // these actions — otherwise there would be no way back in.

    /// <summary>The lock screen. Also locks, so arriving here by any route is safe.</summary>
    [HttpGet]
    public IActionResult Lock()
    {
        if (!CurrentUserId.HasValue) return RedirectToAction("Login");

        HttpContext.Session.SetInt32(WebSessionKeys.Locked, 1);

        ViewBag.FullName = HttpContext.Session.GetString(WebSessionKeys.FullName);
        ViewBag.Username = HttpContext.Session.GetString(WebSessionKeys.Username);
        return View();
    }

    /// <summary>Locks on demand — the idle timer and the menu item both post here.</summary>
    [HttpPost("Auth/LockNow")]
    public IActionResult LockNow()
    {
        if (!CurrentUserId.HasValue) return Unauthorized();

        HttpContext.Session.SetInt32(WebSessionKeys.Locked, 1);
        return Ok();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Unlock(string password, string? returnUrl)
    {
        if (!CurrentUserId.HasValue) return RedirectToAction("Login");

        var r = await _auth.VerifyPasswordAsync(CurrentUserId.Value, password ?? string.Empty);
        if (!r.IsSuccess)
        {
            ViewBag.Error = r.ErrorMessage;
            ViewBag.FullName = HttpContext.Session.GetString(WebSessionKeys.FullName);
            ViewBag.Username = HttpContext.Session.GetString(WebSessionKeys.Username);
            ViewBag.ReturnUrl = returnUrl;
            return View("Lock");
        }

        HttpContext.Session.Remove(WebSessionKeys.Locked);

        // Only a local path is followed. returnUrl arrives from the browser, and an absolute
        // one would turn the lock screen into an open redirect.
        return !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? Redirect(returnUrl)
            : RedirectToAction("Index", "Admin");
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
            // Role and active flag come from the stored record, never from the form —
            // otherwise a user could grant themselves another role via their own profile.
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

        HttpContext.Session.SetString(WebSessionKeys.FullName, updateDto.FullName);
        HttpContext.Session.SetString(WebSessionKeys.Email, updateDto.Email);

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

        // The password change revoked the remember-me token server-side; drop the cookie too.
        ClearRememberCookies();

        TempData["Success"] = "Password changed successfully.";
        return RedirectToAction("Profile");
    }
}
