using CodigoActivo.Domain.Security;
using Microsoft.AspNetCore.DataProtection;

namespace CodigoActivo.Infrastructure.Security;

/// <summary>
/// Protects authenticator secrets with the ASP.NET Core Data Protection key ring, so a copy of the
/// database alone is not enough to generate valid codes.
/// </summary>
public sealed class DataProtectionSecretProtector : ISecretProtector
{
    private const string Purpose = "CodigoActivo.Authenticator.v1";

    private readonly IDataProtector protector;

    /// <summary>
    /// Initializes the protector for the authenticator purpose.
    /// </summary>
    /// <param name="provider">Data protection provider configured by the host.</param>
    public DataProtectionSecretProtector(IDataProtectionProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        protector = provider.CreateProtector(Purpose);
    }

    /// <summary>
    /// Encrypts the supplied value for storage.
    /// </summary>
    /// <param name="plaintext">Value to protect.</param>
    /// <returns>The protected representation.</returns>
    public string Protect(string plaintext)
    {
        return protector.Protect(plaintext);
    }

    /// <summary>
    /// Decrypts a value produced by <see cref="Protect"/>.
    /// </summary>
    /// <param name="protectedValue">Protected representation to reverse.</param>
    /// <returns>The original value.</returns>
    public string Unprotect(string protectedValue)
    {
        return protector.Unprotect(protectedValue);
    }
}
