using System.Diagnostics.CodeAnalysis;

namespace CodigoActivo.Application.Abstractions.Security;

/// <summary>
/// Encrypts secrets that must be readable again by the application, such as authenticator keys.
/// </summary>
public interface ISecretProtector
{
    /// <summary>
    /// Encrypts the supplied value for storage.
    /// </summary>
    /// <param name="plaintext">Value to protect.</param>
    /// <returns>The protected representation.</returns>
    public string Protect(string plaintext);

    /// <summary>
    /// Decrypts a value produced by <see cref="Protect"/>. A value that cannot be decrypted, because
    /// the keys that protected it are gone or it was altered, is reported as unreadable instead of
    /// failing.
    /// </summary>
    /// <param name="protectedValue">Protected representation to reverse.</param>
    /// <param name="plaintext">The original value, when it could be read.</param>
    /// <returns><see langword="true"/> when the value could be decrypted.</returns>
    public bool TryUnprotect(string protectedValue, [NotNullWhen(true)] out string? plaintext);
}
