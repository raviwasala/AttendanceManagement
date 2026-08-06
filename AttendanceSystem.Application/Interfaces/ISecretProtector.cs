namespace AttendanceSystem.Application.Interfaces;

/// <summary>
/// Encrypts and decrypts stored secrets — currently the SMTP password.
///
/// Reversible on purpose: SMTP needs the original password to authenticate, so the hashing
/// used for user passwords and tokens is not an option here. The implementation lives in
/// Infrastructure because key management belongs to the platform, not to business rules.
/// </summary>
public interface ISecretProtector
{
    /// <summary>Encrypts a value. Null or blank input returns null.</summary>
    string? Protect(string? plaintext);

    /// <summary>
    /// Decrypts a value, returning null if it cannot be read — which happens when the
    /// data-protection keys have been lost or replaced. Callers treat that as "no password
    /// configured" rather than crashing, so the settings screen stays reachable and the
    /// password can simply be entered again.
    /// </summary>
    string? Unprotect(string? ciphertext);
}
