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

/// <summary>
/// Handles user authentication and password management.
///
/// This service does not establish a session. It authenticates and reports who the user is
/// and what they may do; the host (web or desktop) decides how to persist that. Keeping
/// session storage out of here is what allows the same service to serve concurrent web
/// requests without one user's identity leaking into another's.
/// </summary>
public class AuthService : IAuthService
{
    /// <summary>How long an issued remember-me token stays valid.</summary>
    private static readonly TimeSpan RememberTokenLifetime = TimeSpan.FromDays(30);

    /// <summary>How long a password-reset token stays valid.</summary>
    private static readonly TimeSpan ResetTokenLifetime = TimeSpan.FromHours(24);

    private readonly IUnitOfWork _uow;
    private readonly IAuditService _audit;
    private readonly IEmailService _email;

    public AuthService(IUnitOfWork uow, IAuditService audit, IEmailService email)
    {
        _uow = uow;
        _audit = audit;
        _email = email;
    }

    public async Task<Result<AuthResultDto>> LoginAsync(LoginDto dto)
    {
        try
        {
            var user = await _uow.Users.GetByUsernameAsync(dto.Username);
            if (user == null)
                return Result<AuthResultDto>.Failure("Invalid username or password.");

            if (!user.IsActive)
                return Result<AuthResultDto>.Failure("Your account is inactive. Please contact administrator.");

            if (user.IsLocked)
                return Result<AuthResultDto>.Failure("Your account is locked. Please contact administrator.");

            if (!PasswordHelper.VerifyPassword(dto.Password, user.PasswordHash))
            {
                await _uow.Users.IncrementFailedLoginAsync(user.Id);
                if (user.FailedLoginAttempts + 1 >= AppConstants.MaxLoginAttempts)
                    await _uow.Users.LockUserAsync(user.Id);
                await _uow.SaveChangesAsync();
                return Result<AuthResultDto>.Failure("Invalid username or password.");
            }

            await _uow.Users.ResetFailedLoginAsync(user.Id);
            user.LastLoginAt = DateTime.Now;

            var result = new AuthResultDto
            {
                User = MapToDto(user),
                Permissions = GetPermissions(user)
            };

            if (dto.RememberMe)
                IssueRememberToken(user, result);

            await _uow.SaveChangesAsync();
            await _audit.LogAsync(AppConstants.Modules.Dashboard, "Login", user.Id);

            return Result<AuthResultDto>.Success(result);
        }
        catch (Exception ex)
        {
            AppLogger.Error("AuthService.LoginAsync", ex);
            return Result<AuthResultDto>.Failure("An error occurred during login.");
        }
    }

    public async Task<Result<AuthResultDto>> ValidateRememberTokenAsync(string username, string token)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(token))
                return Result<AuthResultDto>.Failure("Invalid token.");

            var user = await _uow.Users.GetByUsernameAsync(username);
            if (user == null || !user.IsActive || user.IsLocked)
                return Result<AuthResultDto>.Failure("User not found or disabled.");

            if (!TokenHelper.Verify(token, user.RememberTokenHash))
                return Result<AuthResultDto>.Failure("Invalid remember token.");

            if (!user.RememberTokenExpiresAt.HasValue || user.RememberTokenExpiresAt.Value < DateTime.Now)
            {
                // Expired tokens are cleared rather than left to linger in the database.
                user.RememberTokenHash = null;
                user.RememberTokenExpiresAt = null;
                await _uow.SaveChangesAsync();
                return Result<AuthResultDto>.Failure("Remember token has expired. Please sign in again.");
            }

            user.LastLoginAt = DateTime.Now;

            var result = new AuthResultDto
            {
                User = MapToDto(user),
                Permissions = GetPermissions(user)
            };

            // Rotate on every use: a token that is replayed after the legitimate client
            // has used it will no longer match, which turns silent theft into a visible logout.
            IssueRememberToken(user, result);

            await _uow.SaveChangesAsync();
            await _audit.LogAsync(AppConstants.Modules.Dashboard, "LoginViaRememberToken", user.Id);

            return Result<AuthResultDto>.Success(result);
        }
        catch (Exception ex)
        {
            AppLogger.Error("AuthService.ValidateRememberTokenAsync", ex);
            return Result<AuthResultDto>.Failure("Failed to validate remember token.");
        }
    }

    public async Task<Result> RevokeRememberTokenAsync(int userId)
    {
        try
        {
            var user = await _uow.Users.GetByIdAsync(userId);
            if (user == null) return Result.Failure("User not found.");

            user.RememberTokenHash = null;
            user.RememberTokenExpiresAt = null;
            await _uow.SaveChangesAsync();
            return Result.Success();
        }
        catch (Exception ex)
        {
            AppLogger.Error("AuthService.RevokeRememberTokenAsync", ex);
            return Result.Failure("Failed to revoke remember token.");
        }
    }

    public async Task<Result> RequestPasswordResetAsync(ForgotPasswordDto dto, string baseUrl)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dto.Email))
                return Result.Failure("Email address or username is required.");

            var input = dto.Email.Trim();
            var user = await _uow.Users.GetByEmailAsync(input)
                       ?? await _uow.Users.GetByUsernameAsync(input);

            if (user != null && user.IsActive)
            {
                var rawToken = TokenHelper.GenerateRawToken();
                user.ResetPasswordTokenHash = TokenHelper.Hash(rawToken);
                user.ResetPasswordTokenExpiry = DateTime.Now.Add(ResetTokenLifetime);
                await _uow.SaveChangesAsync();

                var targetEmail = !string.IsNullOrWhiteSpace(user.Email) ? user.Email : input;
                var resetLink = $"{baseUrl.TrimEnd('/')}/Auth/ResetPassword?email={Uri.EscapeDataString(targetEmail)}&token={Uri.EscapeDataString(rawToken)}";

                await _email.SendPasswordResetEmailAsync(targetEmail, resetLink, rawToken);
                await _audit.LogAsync(AppConstants.Modules.Users, "RequestPasswordReset", user.Id);
            }

            // Always reports success — revealing whether an account exists would turn this
            // endpoint into a username enumeration oracle.
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
            var user = await _uow.Users.GetByEmailAsync(input)
                       ?? await _uow.Users.GetByUsernameAsync(input);

            if (user == null ||
                !TokenHelper.Verify(dto.Token, user.ResetPasswordTokenHash) ||
                !user.ResetPasswordTokenExpiry.HasValue ||
                user.ResetPasswordTokenExpiry.Value < DateTime.Now)
            {
                return Result.Failure("Invalid or expired password reset token. Please request a new password reset.");
            }

            user.PasswordHash = PasswordHelper.HashPassword(dto.NewPassword);
            user.PasswordChangedAt = DateTime.Now;
            user.ResetPasswordTokenHash = null;
            user.ResetPasswordTokenExpiry = null;
            user.FailedLoginAttempts = 0;
            user.IsLocked = false;

            // A password reset invalidates every "remember me" session — otherwise a
            // thief who already holds a token keeps their access after the victim recovers.
            user.RememberTokenHash = null;
            user.RememberTokenExpiresAt = null;

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
            user.RememberTokenHash = null;
            user.RememberTokenExpiresAt = null;
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
        await _uow.SaveChangesAsync();
    }

    /// <summary>Mints a fresh remember-me token, storing only its hash and returning the raw value once.</summary>
    private static void IssueRememberToken(User user, AuthResultDto result)
    {
        var rawToken = TokenHelper.GenerateRawToken();
        var expiresAt = DateTime.Now.Add(RememberTokenLifetime);

        user.RememberTokenHash = TokenHelper.Hash(rawToken);
        user.RememberTokenExpiresAt = expiresAt;

        result.RememberToken = rawToken;
        result.RememberTokenExpiresAt = expiresAt;
    }

    private static List<string> GetPermissions(User user) =>
        user.Role?.RolePermissions
            .Where(rp => rp.Permission != null)
            .Select(rp => PermissionKey.For(rp.Permission!.Module, rp.Permission.Action))
            .Distinct(PermissionKey.Comparer)
            .ToList()
        ?? new List<string>();

    public async Task<Result> VerifyPasswordAsync(int userId, string password)
    {
        try
        {
            if (string.IsNullOrEmpty(password)) return Result.Failure("Enter your password.");

            var user = await _uow.Users.GetByIdAsync(userId);
            if (user == null) return Result.Failure("Your session is no longer valid. Please sign in again.");

            // An account deactivated or locked while the screen was locked must not be let
            // back in — the lock screen is the right place to notice that.
            if (!user.IsActive || user.IsLocked)
                return Result.Failure("This account is no longer active. Please sign in again.");

            return PasswordHelper.VerifyPassword(password, user.PasswordHash)
                ? Result.Success()
                : Result.Failure("That password is not correct.");
        }
        catch (Exception ex)
        {
            AppLogger.Error("AuthService.VerifyPasswordAsync", ex);
            return Result.Failure("Could not verify your password.");
        }
    }

    private static UserDto MapToDto(User u) => new()
    {
        Id = u.Id, Username = u.Username, Email = u.Email,
        FullName = u.FullName, RoleId = u.RoleId, RoleName = u.Role?.Name ?? string.Empty,
        EmployeeId = u.EmployeeId, IsActive = u.IsActive, IsLocked = u.IsLocked,
        LastLoginAt = u.LastLoginAt, CreatedAt = u.CreatedAt
    };
}
