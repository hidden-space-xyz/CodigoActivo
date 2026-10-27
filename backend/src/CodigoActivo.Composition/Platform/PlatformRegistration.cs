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
    /// Adds the platform services.
    /// </summary>
    /// <param name="services">Service collection to add to.</param>
    /// <param name="configuration">Configuration of the application.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddPlatform(
        this IServiceCollection services,
        IConfiguration configuration
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

        services.AddValidatedOptions<DeploymentModeOptions>(options =>
            options.Demo = settings.RequiredBoolean("DEMO_MODE")
        );
        services.AddSingleton<DeploymentModeLock>();
        AddDataProtection(services, settings);
        return services;
    }

    private static void AddDataProtection(IServiceCollection services, Settings settings)
    {
        services.AddSingleton(_ =>
            settings.Required(
                "DATA_PROTECTION_KEY",
                value => DataProtectionRootKey.TryParse(value, out var rootKey) ? rootKey : null,
                $"{DataProtectionRootKey.Size} random bytes encoded in Base64"
            )
        );
        services.AddFixedOptions<KeyRingOptions>();
        services
            .AddDataProtection()
            .SetApplicationName("CodigoActivo")
            .ProtectPayloadsWithAesGcm()
            .ProtectKeysWithRootKey();

        services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();
        services.AddSingleton<IOneTimeCodeHasher>(provider => new HmacOneTimeCodeHasher(
            provider
                .GetRequiredService<DataProtectionRootKey>()
                .DeriveKey(HmacOneTimeCodeHasher.KeyPurpose, HmacOneTimeCodeHasher.KeySize)
        ));
    }
}
