using System.Security.Cryptography;
using System.Text;

namespace AttendanceSystem.Common.Helpers;

/// <summary>Password hashing and verification helper using BCrypt.</summary>
public static class PasswordHelper
{
    /// <summary>Hashes a plain-text password using BCrypt.</summary>
    public static string HashPassword(string password) =>
        BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);

    /// <summary>Verifies a plain-text password against a BCrypt hash.</summary>
    public static bool VerifyPassword(string password, string hash) =>
        BCrypt.Net.BCrypt.Verify(password, hash);

    /// <summary>Generates a cryptographically-secure random token.</summary>
    public static string GenerateToken(int length = 32)
    {
        var bytes = RandomNumberGenerator.GetBytes(length);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").Replace("=", "");
    }

    /// <summary>Validates password strength rules.</summary>
    public static (bool IsValid, string Message) ValidateStrength(string password)
    {
        if (string.IsNullOrWhiteSpace(password)) return (false, "Password cannot be empty.");
        if (password.Length < 8) return (false, "Password must be at least 8 characters.");
        if (!password.Any(char.IsUpper)) return (false, "Password must contain at least one uppercase letter.");
        if (!password.Any(char.IsLower)) return (false, "Password must contain at least one lowercase letter.");
        if (!password.Any(char.IsDigit)) return (false, "Password must contain at least one digit.");
        return (true, "Password is valid.");
    }
}
