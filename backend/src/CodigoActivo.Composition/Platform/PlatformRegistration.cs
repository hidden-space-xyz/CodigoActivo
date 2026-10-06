using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Common;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Composition.Configuration;
using CodigoActivo.Infrastructure.Security;
using CodigoActivo.Infrastructure.Time;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CodigoActivo.Composition.Platform;

/// <summary>
/// Registers what every feature relies on: the clock, the public address of the application, the
/// caches and the protection of secrets.
/// </summary>
internal static class PlatformRegistration
{
    /// <summary>
    /// Folder that keeps the data protection keys in production.
    /// </summary>
    private const string KeysDirectory = "/home/app/.aspnet/DataProtection-Keys";

    /// <summary>
    /// Adds the platform services.
    /// </summary>
    /// <param name="services">Service collection to add to.</param>
    /// <param name="configuration">Configuration of the application.</param>
    /// <param name="production">Whether the application runs in production.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddPlatform(
        this IServiceCollection services,
        IConfiguration configuration,
        bool production
    )
    {
        var settings = new Settings(configuration);
        var timeZone = settings.TimeZone("APP_TIMEZONE", TimeZoneInfo.Local);
        services.AddSingleton<IClock>(new SystemClock(timeZone));

        services.AddValidatedOptions<ApplicationOptions>(options =>
            options.BaseUrl = settings
                .Text("APP_BASE_URL", ApplicationOptions.DefaultBaseUrl)
                .TrimEnd('/')
        );

        services.AddMemoryCache(options => options.SizeLimit = CacheLimits.LocalCacheSizeBytes);
        services.AddHybridCache(options =>
            options.MaximumPayloadBytes = CacheLimits.MaximumPayloadBytes
        );

        AddDataProtection(services, configuration, production);
        return services;
    }

    private static void AddDataProtection(
        IServiceCollection services,
        IConfiguration configuration,
        bool production
    )
    {
        var dataProtection = services
            .AddDataProtection()
            .SetApplicationName("CodigoActivo")
            .ProtectPayloadsWithAesGcm();
        if (production)
        {
            dataProtection.ProtectKeysWithEd25519Certificate(
                services,
                new DirectoryInfo(KeysDirectory),
                configuration["DATA_PROTECTION_CERTIFICATE_PASSWORD"]!,
                new SystemClock(TimeZoneInfo.Utc)
            );
        }

        services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();
    }
}
