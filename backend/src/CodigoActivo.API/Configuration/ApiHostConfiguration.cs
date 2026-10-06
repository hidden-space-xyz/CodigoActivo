using CodigoActivo.API.Security;

namespace CodigoActivo.API.Configuration;

internal static class ApiHostConfiguration
{
    internal static void ConfigureApiHost(this WebApplicationBuilder builder, ApiLogging logging)
    {
        logging.Configure(builder.Logging);
        ConfigureAllowedHosts(builder);
        ValidateProductionConfiguration(builder.Environment, builder.Configuration);
        ConfigureKestrel(builder);
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
            builder.Configuration["AllowedHosts"] = $"{baseUri.IdnHost};localhost;127.0.0.1;api";
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

    private static void ConfigureKestrel(WebApplicationBuilder builder)
    {
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.AddServerHeader = false;
            options.Limits.MaxRequestBodySize = 12 * 1024 * 1024;
            options.Limits.MaxRequestHeadersTotalSize = 32 * 1024;
            options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(15);
        });
    }
}
