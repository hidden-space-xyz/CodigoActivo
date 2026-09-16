using System.Security.Cryptography;
using CodigoActivo.Domain.Security;

namespace CodigoActivo.Application.Auth;

/// <summary>
/// Protects credential timing operations against timing disclosure.
/// </summary>
public sealed class CredentialTimingProtector
{
    private readonly IPasswordHasher hasher;
    private readonly string fallbackHash;

    /// <summary>
    /// Initializes a credential timing protector with its required dependencies.
    /// </summary>
    /// <param name="hasher">The hasher value.</param>
    public CredentialTimingProtector(IPasswordHasher hasher)
    {
        this.hasher = hasher;
        fallbackHash = hasher.Hash(Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
    }

    /// <summary>
    /// Verifies the supplied value against its stored cryptographic representation.
    /// </summary>
    /// <param name="password">Plain-text password to hash or verify.</param>
    /// <param name="passwordHash">The password hash value.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public bool Verify(string password, string? passwordHash)
    {
        return hasher.Verify(password, passwordHash ?? fallbackHash) && passwordHash is not null;
    }
}
