using System.Security.Cryptography;
using System.Text;

namespace AttendanceSystem.Common.Helpers;

/// <summary>
/// Issues and verifies the long-lived bearer tokens used for "remember me" and password reset.
///
/// Both are credentials: whoever holds one can act as the user. So the raw value is generated
/// from a CSPRNG, handed to the client exactly once, and only ever persisted as a SHA-256 hash.
/// A plain SHA-256 (rather than BCrypt) is appropriate here precisely because the input is
/// 256 bits of entropy — there is nothing to brute-force — and verification happens on every
/// request, where BCrypt's work factor would be a denial-of-service surface.
/// </summary>
public static class TokenHelper
{
    /// <summary>Generates a new raw token with 256 bits of entropy, URL-safe.</summary>
    public static string GenerateRawToken() => PasswordHelper.GenerateToken(32);

    /// <summary>Hashes a raw token for storage. Deterministic, so it can be looked up.</summary>
    public static string Hash(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// Constant-time comparison of a presented raw token against a stored hash.
    /// Constant-time matters: a short-circuiting comparison leaks the stored value
    /// one byte at a time to an attacker who can measure response times.
    /// </summary>
    public static bool Verify(string? rawToken, string? storedHash)
    {
        if (string.IsNullOrEmpty(rawToken) || string.IsNullOrEmpty(storedHash)) return false;

        var presented = Encoding.UTF8.GetBytes(Hash(rawToken));
        var stored = Encoding.UTF8.GetBytes(storedHash);
        return CryptographicOperations.FixedTimeEquals(presented, stored);
    }
}
