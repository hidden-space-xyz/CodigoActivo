using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using CodigoActivo.API.Caching;
using CodigoActivo.API.Extensions;
using CodigoActivo.API.Middlewares;
using CodigoActivo.API.OpenApi;
using CodigoActivo.API.Security;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.Options;
using CodigoActivo.Composition;
using CodigoActivo.Domain.Common;
using CodigoActivo.Infrastructure.Communication;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Seeders;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

ConfigureAllowedHosts(builder);
ValidateProductionConfiguration(builder.Environment, builder.Configuration);
ConfigureDataProtection(builder);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 12 * 1024 * 1024;
    options.Limits.MaxRequestHeadersTotalSize = 32 * 1024;
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(15);
});

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.IncludeScopes = true;
    options.SingleLine = true;
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff zzz ";
});

builder.Services.AddCodigoActivo(builder.Configuration);
builder.Services.AddScoped<SessionTicketValidator>();
builder.Services.AddSingleton<DeploymentModeLock>();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;

    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder
    .Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter())
    )
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var (statusCode, body) = ApiErrorResponseExtensions.Create(
                Error.BadRequest(ErrorCode.RequestValidationFailed),
                context.HttpContext
            );
            return new ObjectResult(body) { StatusCode = statusCode };
        };
    });

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.SuppressXFrameOptionsHeader = true;
    options.Cookie.Name = builder.Environment.IsProduction()
        ? "__Host-CodigoActivo.Csrf"
        : "CodigoActivo.Csrf";
    options.Cookie.HttpOnly = true;
    options.Cookie.Path = "/";
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

builder
    .Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = builder.Environment.IsProduction()
            ? "__Host-CodigoActivo.Session"
            : builder.Configuration["Auth:CookieName"] ?? "CodigoActivo.Session";
        options.Cookie.HttpOnly = true;
        options.Cookie.Path = "/";
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.SlidingExpiration = false;
        options.ExpireTimeSpan = TimeSpan.FromHours(
            builder.Configuration.GetValue<double?>("Auth:ExpireHours") ?? 8
        );

        options.Events.OnRedirectToLogin = ctx =>
            ctx.HttpContext.WriteApiErrorAsync(
                Error.Unauthorized(ErrorCode.AuthenticationRequired)
            );
        options.Events.OnRedirectToAccessDenied = ctx =>
            ctx.HttpContext.WriteApiErrorAsync(Error.Forbidden(ErrorCode.AccessDenied));
        options.Events.OnValidatePrincipal = ctx =>
            ctx.HttpContext.RequestServices
                .GetRequiredService<SessionTicketValidator>()
                .ValidateAsync(ctx);
    });

builder
    .Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

var outputCacheLifetime = TimeSpan.FromMinutes(1);
builder.Services.AddOutputCache(options =>
{
    foreach (var tag in CacheTags.OutputCached)
    {
        options.AddPolicy(tag, policy => policy.Expire(outputCacheLifetime).Tag(tag));
    }

    options.AddPolicy(
        OutputCachePolicies.Seo,
        policy =>
            policy
                .Expire(outputCacheLifetime)
                .Tag(CacheTags.Events, CacheTags.Announcements, CacheTags.Resources)
    );
});
builder.Services.AddSingleton<ICacheInvalidator, HttpCacheInvalidator>();

const int MaxConcurrentCredentialRequests = 4;
const int MaxQueuedCredentialRequests = 128;
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = CredentialConcurrencyLimiter.Create(
        MaxConcurrentCredentialRequests,
        MaxQueuedCredentialRequests
    );
    options.AddPolicy(
        SecurityPolicies.Credentials,
        context =>
            RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ =>
                    new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = builder.Environment.IsDevelopment() ? 10_000 : 120,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true,
                    }
            )
    );
});

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.OperationFilter<JsonResponseMediaTypeFilter>();
    c.OperationFilter<CamelCaseQueryParametersFilter>();
    c.DocumentFilter<ApiErrorResponseDocumentFilter>();
});

await using var app = builder.Build();

await InitializeDatabaseAsync(app, app.Lifetime.ApplicationStopping);
LogEmailGuardState(app);

app.UseForwardedHeaders();

app.UseMiddleware<RequestLoggingMiddleware>();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "CodigoActivo API v1"));
}

app.UseHttpsRedirection();

app.UseMiddleware<CacheControlMiddleware>();

app.UseRouting();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<CsrfValidationMiddleware>();

app.UseOutputCache();

app.MapControllers();

await app.RunAsync();

static void ConfigureAllowedHosts(WebApplicationBuilder builder)
{
    if (
        builder.Environment.IsProduction()
        && Uri.TryCreate(builder.Configuration["APP_BASE_URL"], UriKind.Absolute, out var baseUri)
    )
    {
        builder.Configuration["AllowedHosts"] =
            $"{baseUri.IdnHost};localhost;127.0.0.1;api";
    }
}

static void ValidateProductionConfiguration(
    IHostEnvironment environment,
    IConfiguration configuration
)
{
    if (environment.IsProduction())
    {
        ProductionConfigurationValidator.Validate(configuration);
    }
}

static void ConfigureDataProtection(WebApplicationBuilder builder)
{
    var dataProtection = builder
        .Services.AddDataProtection()
        .SetApplicationName("CodigoActivo");

    if (!builder.Environment.IsProduction())
    {
        return;
    }

    var keysDirectory = new DirectoryInfo("/home/app/.aspnet/DataProtection-Keys");
    var certificatePassword = builder.Configuration[
        "DATA_PROTECTION_CERTIFICATE_PASSWORD"
    ]!;
    var certificateStore = Ed25519CertificateStore.LoadOrCreate(
        keysDirectory,
        certificatePassword
    );

    builder.Services.AddSingleton(certificateStore);
    dataProtection.PersistKeysToFileSystem(keysDirectory);
    builder.Services.Configure<KeyManagementOptions>(options =>
        options.XmlEncryptor = new Ed25519XmlEncryptor(certificateStore)
    );
}

static async Task InitializeDatabaseAsync(WebApplication app, CancellationToken ct)
{
    await using var scope = app.Services.CreateAsyncScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var demoMode = scope.ServiceProvider
        .GetRequiredService<DeploymentModeLock>()
        .Lock(app.Configuration);

    await MigrateDatabaseAsync(scope.ServiceProvider, logger, ct);
    await SeedDatabaseAsync(scope.ServiceProvider, logger, ct);
    await scope
        .ServiceProvider.GetRequiredService<InitialAdministratorSeeder>()
        .SeedAsync(
            app.Configuration["BOOTSTRAP_ADMIN_EMAIL"],
            app.Configuration["BOOTSTRAP_ADMIN_PASSWORD"],
            ct
        );
    await SyncDemoDataAsync(scope.ServiceProvider, demoMode, ct);
}

static async Task MigrateDatabaseAsync(
    IServiceProvider services,
    ILogger<Program> logger,
    CancellationToken ct
)
{
    logger.LogInformation("Applying database migrations");
    await services.GetRequiredService<CodigoActivoDbContext>().Database.MigrateAsync(ct);
    logger.LogInformation("Database migrations applied");
}

static async Task SeedDatabaseAsync(
    IServiceProvider services,
    ILogger<Program> logger,
    CancellationToken ct
)
{
    logger.LogInformation("Seeding database");
    await services.GetRequiredService<DatabaseSeeder>().SeedAsync(ct);
    logger.LogInformation("Database seeding complete");
}

static void LogEmailGuardState(WebApplication app)
{
    var guard = app.Services.GetRequiredService<EmailGuardOptions>();
    var logger = app.Services.GetRequiredService<ILogger<Program>>();

    if (!logger.IsEnabled(LogLevel.Information))
    {
        return;
    }

    logger.LogInformation(
        "Outbound email guard armed: per recipient {RecipientBurst} burst, {RecipientPerHour}/hour, {RecipientPerDay}/day; overall "
            + "{GlobalBurst} burst, {GlobalPerHour}/hour with {Reserve} reserved for account email. Admin-written email is exempt",
        guard.RecipientBurst,
        guard.RecipientPerHour,
        guard.RecipientPerDay,
        guard.GlobalBurst,
        guard.GlobalPerHour,
        guard.EffectiveCredentialReserve
    );
}

static async Task SyncDemoDataAsync(
    IServiceProvider services,
    bool demoMode,
    CancellationToken ct
)
{
    if (demoMode)
    {
        var demoSeeder = services.GetRequiredService<DemoDataSeeder>();
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        await demoSeeder.SeedAsync(cts.Token);
    }
}
