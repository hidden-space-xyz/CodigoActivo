namespace CodigoActivo.API.Configuration;

internal static class ApiHostConfiguration
{
    private const string SecretsDirectory = "/run/secrets";

    internal static void ConfigureApiHost(this WebApplicationBuilder builder)
    {
        builder.Configuration.AddKeyPerFile(SecretsDirectory, optional: true);
        ApiLogging.Configure(builder.Logging, builder.Environment);
        ConfigureAllowedHosts(builder);
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

    private static void ConfigureKestrel(WebApplicationBuilder builder)
    {
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.AddServerHeader = false;
            options.Limits.MaxRequestBodySize = 2 * 1024 * 1024;
            options.Limits.MaxRequestHeadersTotalSize = 32 * 1024;
            options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(15);
        });
    }
}
