using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Common.Constants;
using AttendanceSystem.Common.Helpers;
using AttendanceSystem.Common.Logging;
using AttendanceSystem.Common.Models;
using AttendanceSystem.Common.Session;
using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Domain.Interfaces;

namespace AttendanceSystem.Application.Services;

/// <summary>Handles user authentication, password management and session.</summary>
public class AuthService : IAuthService
{
    private readonly IUnitOfWork _uow;
    private readonly IAuditService _audit;
    private readonly IEmailService _email;

    public AuthService(IUnitOfWork uow, IAuditService audit, IEmailService email)
    {
        _uow = uow;
        _audit = audit;
        _email = email;
    }

    public async Task<Result<UserDto>> LoginAsync(LoginDto dto)
    {
        try
        {
            var user = await _uow.Users.GetByUsernameAsync(dto.Username);
            if (user == null)
                return Result<UserDto>.Failure("Invalid username or password.");

            if (!user.IsActive)
                return Result<UserDto>.Failure("Your account is inactive. Please contact administrator.");

            if (user.IsLocked)
                return Result<UserDto>.Failure("Your account is locked. Please contact administrator.");

            if (!PasswordHelper.VerifyPassword(dto.Password, user.PasswordHash))
            {
                await _uow.Users.IncrementFailedLoginAsync(user.Id);
                if (user.FailedLoginAttempts + 1 >= AppConstants.MaxLoginAttempts)
                    await _uow.Users.LockUserAsync(user.Id);
                await _uow.SaveChangesAsync();
                return Result<UserDto>.Failure("Invalid username or password.");
            }

            await _uow.Users.ResetFailedLoginAsync(user.Id);
            user.LastLoginAt = DateTime.Now;
            if (dto.RememberMe)
                user.RememberToken = PasswordHelper.GenerateToken();
            await _uow.SaveChangesAsync();

            var permissions = user.Role.RolePermissions
                .Select(rp => $"{rp.Permission.Module}.{rp.Permission.Action}")
                .ToList();

            AppSession.SetSession(user.Id, user.Username, user.FullName,
                user.Role.Name, user.RoleId, user.EmployeeId, permissions);

            await _audit.LogAsync(AppConstants.Modules.Dashboard, "Login", user.Id);

            return Result<UserDto>.Success(MapToDto(user));
        }
        catch (Exception ex)
        {
            AppLogger.Error("AuthService.LoginAsync", ex);
            return Result<UserDto>.Failure("An error occurred during login.");
        }
    }

    public async Task<Result<UserDto>> ValidateRememberTokenAsync(string username, string token)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(token))
                return Result<UserDto>.Failure("Invalid token.");

            var user = await _uow.Users.GetByUsernameAsync(username);
            if (user == null || !user.IsActive || user.IsLocked)
                return Result<UserDto>.Failure("User not found or disabled.");

            if (user.RememberToken != token)
                return Result<UserDto>.Failure("Invalid remember token.");

            var permissions = user.Role.RolePermissions
                .Select(rp => $"{rp.Permission.Module}.{rp.Permission.Action}")
                .ToList();

            AppSession.SetSession(user.Id, user.Username, user.FullName,
                user.Role.Name, user.RoleId, user.EmployeeId, permissions);

            return Result<UserDto>.Success(MapToDto(user));
        }
        catch (Exception ex)
        {
            AppLogger.Error("AuthService.ValidateRememberTokenAsync", ex);
            return Result<UserDto>.Failure("Failed to validate remember token.");
        }
    }

    public async Task<Result> RequestPasswordResetAsync(ForgotPasswordDto dto, string baseUrl)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dto.Email))
                return Result.Failure("Email address or username is required.");

            var input = dto.Email.Trim();
            var user = await _uow.Users.GetByEmailAsync(input);
            if (user == null)
            {
                user = await _uow.Users.GetByUsernameAsync(input);
            }

            if (user != null && user.IsActive)
            {
                var token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
                user.ResetPasswordToken = token;
                user.ResetPasswordTokenExpiry = DateTime.Now.AddHours(24);
                await _uow.SaveChangesAsync();

                var targetEmail = !string.IsNullOrWhiteSpace(user.Email) ? user.Email : input;
                var resetLink = $"{baseUrl.TrimEnd('/')}/Auth/ResetPassword?email={Uri.EscapeDataString(targetEmail)}&token={token}";

                await _email.SendPasswordResetEmailAsync(targetEmail, resetLink, token);
                await _audit.LogAsync(AppConstants.Modules.Users, "RequestPasswordReset", user.Id);
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            AppLogger.Error("AuthService.RequestPasswordResetAsync", ex);
            return Result.Failure("An error occurred while processing password reset request.");
        }
    }

    public async Task<Result> ResetPasswordWithTokenAsync(ResetPasswordWithTokenDto dto)
    {
        try
        {
            if (dto.NewPassword != dto.ConfirmPassword)
                return Result.Failure("New password and confirm password do not match.");

            var (isValid, msg) = PasswordHelper.ValidateStrength(dto.NewPassword);
            if (!isValid) return Result.Failure(msg);

            var input = dto.Email.Trim();
            var user = await _uow.Users.GetByEmailAsync(input);
            if (user == null)
            {
                user = await _uow.Users.GetByUsernameAsync(input);
            }

            if (user == null || user.ResetPasswordToken != dto.Token ||
                !user.ResetPasswordTokenExpiry.HasValue || user.ResetPasswordTokenExpiry.Value < DateTime.Now)
            {
                return Result.Failure("Invalid or expired password reset token. Please request a new password reset.");
            }

            user.PasswordHash = PasswordHelper.HashPassword(dto.NewPassword);
            user.PasswordChangedAt = DateTime.Now;
            user.ResetPasswordToken = null;
            user.ResetPasswordTokenExpiry = null;
            user.FailedLoginAttempts = 0;
            user.IsLocked = false;
            await _uow.SaveChangesAsync();

            await _audit.LogAsync(AppConstants.Modules.Users, "ResetPasswordWithToken", user.Id);
            return Result.Success();
        }
        catch (Exception ex)
        {
            AppLogger.Error("AuthService.ResetPasswordWithTokenAsync", ex);
            return Result.Failure("An error occurred while resetting password.");
        }
    }

    public async Task<Result> ChangePasswordAsync(int userId, ChangePasswordDto dto)
    {
        try
        {
            if (dto.NewPassword != dto.ConfirmPassword)
                return Result.Failure("New password and confirm password do not match.");

            var (isValid, msg) = PasswordHelper.ValidateStrength(dto.NewPassword);
            if (!isValid) return Result.Failure(msg);

            var user = await _uow.Users.GetByIdAsync(userId);
            if (user == null) return Result.Failure("User not found.");

            if (!PasswordHelper.VerifyPassword(dto.CurrentPassword, user.PasswordHash))
                return Result.Failure("Current password is incorrect.");

            user.PasswordHash = PasswordHelper.HashPassword(dto.NewPassword);
            user.PasswordChangedAt = DateTime.Now;
            await _uow.SaveChangesAsync();

            await _audit.LogAsync(AppConstants.Modules.Users, "ChangePassword", userId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            AppLogger.Error("AuthService.ChangePasswordAsync", ex);
            return Result.Failure("An error occurred while changing password.");
        }
    }

    public async Task LogoutAsync(int userId)
    {
        await _audit.LogAsync(AppConstants.Modules.Dashboard, "Logout", userId);
        AppSession.Clear();
    }

    private static UserDto MapToDto(User u) => new()
    {
        Id = u.Id, Username = u.Username, Email = u.Email,
        FullName = u.FullName, RoleId = u.RoleId, RoleName = u.Role?.Name ?? string.Empty,
        EmployeeId = u.EmployeeId, IsActive = u.IsActive, IsLocked = u.IsLocked,
        LastLoginAt = u.LastLoginAt, CreatedAt = u.CreatedAt, RememberToken = u.RememberToken
    };
}
