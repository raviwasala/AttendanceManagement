using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Common.Logging;
using Microsoft.AspNetCore.DataProtection;

namespace AttendanceSystem.Infrastructure.Services;

/// <summary>
/// <see cref="ISecretProtector"/> over ASP.NET Core Data Protection.
///
/// Delegates rather than inventing anything: keys are generated, stored and rotated by the
/// framework, and key handling is the whole game for reversible encryption.
/// </summary>
public class SecretProtector : ISecretProtector
{
    // Purpose string: values encrypted for this purpose cannot be decrypted through another,
    // so a bug elsewhere cannot be used to read mail credentials.
    private const string Purpose = "AttendanceSystem.StoredSecrets.v1";

    private readonly IDataProtector _protector;

    public SecretProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(Purpose);
    }

    public string? Protect(string? plaintext) =>
        string.IsNullOrWhiteSpace(plaintext) ? null : _protector.Protect(plaintext);

    public string? Unprotect(string? ciphertext)
    {
        if (string.IsNullOrWhiteSpace(ciphertext)) return null;

        try
        {
            return _protector.Unprotect(ciphertext);
        }
        catch (Exception ex)
        {
            AppLogger.Error("SecretProtector.Unprotect — stored secret could not be read. "
                          + "The data-protection keys have probably changed; re-enter it.", ex);
            return null;
        }
    }
}
