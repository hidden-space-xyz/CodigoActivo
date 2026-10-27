namespace CodigoActivo.Application.Abstractions.Security;

/// <summary>
/// Hashes and verifies account passwords with a salted, memory-hard hash (Argon2id) at fixed cost
/// parameters.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Hashes a password with a fresh random salt for storage.
    /// </summary>
    /// <param name="password">Plain-text password to hash.</param>
    /// <returns>The encoded hash, which carries the algorithm, its cost parameters and the salt.</returns>
    public string Hash(string password);

    /// <summary>
    /// Verifies a password against a hash produced by <see cref="Hash"/>, in constant time.
    /// </summary>
    /// <param name="password">Plain-text password to verify.</param>
    /// <param name="hash">Encoded password hash to verify against.</param>
    /// <returns>
    /// <see langword="true"/> when the password matches; <see langword="false"/> when it does not or
    /// the hash is malformed or uses other cost parameters.
    /// </returns>
    public bool Verify(string password, string hash);
}
