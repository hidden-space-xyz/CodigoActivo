using System.Security.Cryptography;
using CodigoActivo.Application.Abstractions.Security;

namespace CodigoActivo.Application.Accounts.Passwords;

/// <summary>
/// Verifies passwords so that an account without a stored password, or no account at all, pays the
/// same hashing work as one with a password: the missing hash is replaced by a random fallback hash
/// that never matches, so the timing does not disclose which accounts exist.
/// </summary>
public sealed class CredentialTimingProtector
{
    private readonly IPasswordHasher hasher;
    private readonly string fallbackHash;

    /// <summary>
    /// Initializes the protector and hashes a random secret as the fallback hash.
    /// </summary>
    /// <param name="hasher">Hasher used to verify passwords and to build the fallback hash.</param>
    public CredentialTimingProtector(IPasswordHasher hasher)
    {
        this.hasher = hasher;
        fallbackHash = hasher.Hash(Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
    }

    /// <summary>
    /// Verifies a password against a stored hash, paying the same work when there is none.
    /// </summary>
    /// <param name="password">Plain-text password presented by the client.</param>
    /// <param name="passwordHash">Stored password hash, or <see langword="null"/> when there is none.</param>
    /// <returns>
    /// <see langword="true"/> when the password matches a stored hash; always <see langword="false"/>
    /// when <paramref name="passwordHash"/> is <see langword="null"/>.
    /// </returns>
    public bool Verify(string password, string? passwordHash)
    {
        return hasher.Verify(password, passwordHash ?? fallbackHash) && passwordHash is not null;
    }
}
