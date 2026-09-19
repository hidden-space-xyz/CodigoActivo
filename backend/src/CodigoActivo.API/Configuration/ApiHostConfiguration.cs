using CodigoActivo.API.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;

namespace CodigoActivo.API.Configuration;

internal static class ApiHostConfiguration
{
    internal static void ConfigureApiHost(this WebApplicationBuilder builder)
    {
        ConfigureAllowedHosts(builder);
        ValidateProductionConfiguration(builder.Environment, builder.Configuration);
        ConfigureDataProtection(builder);
        ConfigureKestrel(builder);
        ConfigureLogging(builder.Logging);
    }

    private static void ConfigureAllowedHosts(WebApplicationBuilder builder)
    {
        if (
            builder.Environment.IsProduction()
            && Uri.TryCreate(
                builder.Configuration["APP_BASE_URL"],
                UriKind.Absolute,
                out var baseUri
            )
        )
        {
            builder.Configuration["AllowedHosts"] =
                $"{baseUri.IdnHost};localhost;127.0.0.1;api";
        }
    }

    private static void ValidateProductionConfiguration(
        IHostEnvironment environment,
        IConfiguration configuration
    )
    {
        if (environment.IsProduction())
        {
            ProductionConfigurationValidator.Validate(configuration);
        }
    }

    private static void ConfigureDataProtection(WebApplicationBuilder builder)
    {
        var dataProtection = builder
            .Services.AddDataProtection()
            .SetApplicationName("CodigoActivo");

        if (!builder.Environment.IsProduction())
        {
            return;
        }

        ProtectKeysWithEd25519Certificate(
            builder.Services,
            dataProtection,
            new DirectoryInfo("/home/app/.aspnet/DataProtection-Keys"),
            builder.Configuration["DATA_PROTECTION_CERTIFICATE_PASSWORD"]!
        );
    }

    internal static Ed25519CertificateStore ProtectKeysWithEd25519Certificate(
        IServiceCollection services,
        IDataProtectionBuilder dataProtection,
        DirectoryInfo keysDirectory,
        string certificatePassword
    )
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(dataProtection);

        using var loggerFactory = LoggerFactory.Create(ConfigureLogging);
        var certificateStore = Ed25519CertificateStore.LoadOrCreate(
            keysDirectory,
            certificatePassword,
            loggerFactory.CreateLogger<Ed25519CertificateStore>()
        );

        services.AddSingleton(certificateStore);
        dataProtection.PersistKeysToFileSystem(keysDirectory);
        services.Configure<KeyManagementOptions>(options =>
            options.XmlEncryptor = new Ed25519CmsXmlEncryptor(certificateStore)
        );
        return certificateStore;
    }

    private static void ConfigureKestrel(WebApplicationBuilder builder)
    {
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Limits.MaxRequestBodySize = 12 * 1024 * 1024;
            options.Limits.MaxRequestHeadersTotalSize = 32 * 1024;
            options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(15);
        });
    }

    private static void ConfigureLogging(ILoggingBuilder logging)
    {
        logging.ClearProviders();
        logging.AddSimpleConsole(options =>
        {
            options.IncludeScopes = true;
            options.SingleLine = true;
            options.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff zzz ";
        });
    }
}
