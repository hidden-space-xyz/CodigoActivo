using CodigoActivo.Infrastructure.Security;
using CodigoActivo.Infrastructure.Time;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace CodigoActivo.Composition;

/// <summary>
/// Configures ASP.NET Data Protection for the host: the application name, AES-256-GCM payloads and,
/// when requested, a key ring encrypted at rest with the Ed25519 certificate.
/// </summary>
public static class DataProtectionSetup
{
    /// <summary>
    /// Directory where the key ring and its protecting certificate are stored in production.
    /// </summary>
    public const string KeysDirectory = "/home/app/.aspnet/DataProtection-Keys";

    /// <summary>
    /// Adds Data Protection configured for the host.
    /// </summary>
    /// <param name="services">Service collection to configure.</param>
    /// <param name="protectKeysAtRest">Whether the key ring is persisted and encrypted with the certificate.</param>
    /// <param name="certificatePassword">Password of the certificate; required when keys are protected at rest.</param>
    /// <returns>The Data Protection builder.</returns>
    public static IDataProtectionBuilder AddCodigoActivoDataProtection(
        this IServiceCollection services,
        bool protectKeysAtRest,
        string? certificatePassword
    )
    {
        var dataProtection = services
            .AddDataProtection()
            .SetApplicationName("CodigoActivo")
            .ProtectPayloadsWithAesGcm();

        if (protectKeysAtRest)
        {
            dataProtection.ProtectKeysWithEd25519Certificate(
                services,
                new DirectoryInfo(KeysDirectory),
                certificatePassword!,
                new SystemClock(TimeZoneInfo.Utc)
            );
        }

        return dataProtection;
    }
}
