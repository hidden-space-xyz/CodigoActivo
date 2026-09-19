namespace CodigoActivo.Domain.Security;

/// <summary>
/// Hashes and verifies password values using the configured algorithm.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Determines whether h exists.
    /// </summary>
    /// <param name="password">Plain-text password to hash or verify.</param>
    /// <returns>The generated text.</returns>
    public string Hash(string password);

    /// <summary>
    /// Verifies the supplied value against its stored cryptographic representation.
    /// </summary>
    /// <param name="password">Plain-text password to hash or verify.</param>
    /// <param name="hash">Encoded password hash to verify.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public bool Verify(string password, string hash);
}
