namespace CodigoActivo.Domain.Security;

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
    /// Decrypts a value produced by <see cref="Protect"/>.
    /// </summary>
    /// <param name="protectedValue">Protected representation to reverse.</param>
    /// <returns>The original value.</returns>
    public string Unprotect(string protectedValue);
}
