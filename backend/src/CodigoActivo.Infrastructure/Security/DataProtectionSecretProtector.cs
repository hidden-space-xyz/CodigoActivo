using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Infrastructure.Diagnostics;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Infrastructure.Security;

/// <summary>
/// Protects authenticator secrets with the ASP.NET Core Data Protection key ring, so a copy of the
/// database alone is not enough to generate valid codes.
/// </summary>
public sealed class DataProtectionSecretProtector : ISecretProtector
{
    private const string Purpose = "CodigoActivo.Authenticator.v1";

    private readonly IDataProtector protector;
    private readonly ILogger<DataProtectionSecretProtector> logger;

    /// <summary>
    /// Initializes the protector for the authenticator purpose.
    /// </summary>
    /// <param name="provider">Data protection provider configured by the host.</param>
    /// <param name="logger">Logger used to record the secrets that could not be read.</param>
    public DataProtectionSecretProtector(
        IDataProtectionProvider provider,
        ILogger<DataProtectionSecretProtector> logger
    )
    {
        ArgumentNullException.ThrowIfNull(provider);
        protector = provider.CreateProtector(Purpose);
        this.logger = logger;
    }

    /// <inheritdoc />
    public string Protect(string plaintext)
    {
        return protector.Protect(plaintext);
    }

    /// <inheritdoc />
    public bool TryUnprotect(string protectedValue, [NotNullWhen(true)] out string? plaintext)
    {
        try
        {
            plaintext = protector.Unprotect(protectedValue);
            return true;
        }
        catch (CryptographicException ex)
        {
            logger.AuthenticatorKeyUnreadable(ex);
            plaintext = null;
            return false;
        }
    }
}
