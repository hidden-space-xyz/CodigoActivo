using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using System.Xml.Linq;
using CodigoActivo.API.Caching;
using CodigoActivo.API.Extensions;
using CodigoActivo.API.Middlewares;
using CodigoActivo.API.OpenApi;
using CodigoActivo.API.Security;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.Options;
using CodigoActivo.Composition;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Infrastructure.Communication;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Seeders;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
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
    options.Cookie.SameSite = ResolveSameSite(builder.Configuration["AUTH_SAMESITE"]);
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
        options.Cookie.SameSite = ResolveSameSite(builder.Configuration["AUTH_SAMESITE"]);
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

builder.Services.AddRateLimiter(options =>
{
    var maxConcurrentCredentialRequests = builder.Environment.IsDevelopment()
        ? 10_000
        : Math.Clamp(
            builder.Configuration.GetValue<int?>("Auth:MaxConcurrentCredentialRequests") ?? 4,
            1,
            32
        );
    var maxQueuedCredentialRequests = builder.Environment.IsDevelopment()
        ? 10_000
        : Math.Clamp(
            builder.Configuration.GetValue<int?>("Auth:MaxQueuedCredentialRequests") ?? 128,
            100,
            1_000
        );

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = CredentialConcurrencyLimiter.Create(
        maxConcurrentCredentialRequests,
        maxQueuedCredentialRequests
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

static SameSiteMode ResolveSameSite(string? value)
{
    return value?.Trim().ToLowerInvariant() switch
    {
        "none" => SameSiteMode.None,
        "strict" => SameSiteMode.Strict,
        "lax" => SameSiteMode.Lax,
        _ => SameSiteMode.Lax,
    };
}

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
    var certificate = LoadOrCreateDataProtectionCertificate(
        keysDirectory,
        certificatePassword
    );

    dataProtection
        .PersistKeysToFileSystem(keysDirectory)
        .ProtectKeysWithCertificate(certificate);
}

static X509Certificate2 LoadOrCreateDataProtectionCertificate(
    DirectoryInfo keysDirectory,
    string password
)
{
    keysDirectory.Create();
    if (keysDirectory.EnumerateFiles("key-*.xml").Any(IsUnprotectedDataProtectionKey))
    {
        throw new InvalidOperationException(
            "Unprotected Data Protection keys were found. Rotate the api-dataprotection volume before starting this release."
        );
    }

    var certificatePath = Path.Combine(keysDirectory.FullName, "key-encryption.pfx");
    if (!File.Exists(certificatePath))
    {
        using var rsa = RSA.Create(3072);
        var request = new CertificateRequest(
            "CN=CodigoActivo Data Protection",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1
        );
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.KeyEncipherment, critical: true)
        );
        using var generatedCertificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddYears(5)
        );
        var encodedCertificate = generatedCertificate.Export(
            X509ContentType.Pfx,
            password
        );

        try
        {
            using var stream = new FileStream(
                certificatePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None
            );
            stream.Write(encodedCertificate);
            stream.Flush(flushToDisk: true);
            if (OperatingSystem.IsLinux())
            {
                File.SetUnixFileMode(
                    certificatePath,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite
                );
            }
        }
        catch (IOException) when (File.Exists(certificatePath))
        {
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encodedCertificate);
        }
    }

    var certificate = X509CertificateLoader.LoadPkcs12FromFile(
        certificatePath,
        password,
        X509KeyStorageFlags.EphemeralKeySet
    );
    if (!certificate.HasPrivateKey)
    {
        certificate.Dispose();
        throw new InvalidOperationException(
            "The Data Protection certificate does not contain a private key"
        );
    }

    return certificate;
}

static bool IsUnprotectedDataProtectionKey(FileInfo file)
{
    using var stream = file.OpenRead();
    var document = XDocument.Load(stream, LoadOptions.None);
    return !document
        .Descendants()
        .Any(element => element.Name.LocalName == "encryptedSecret");
}

static async Task InitializeDatabaseAsync(WebApplication app, CancellationToken ct)
{
    await using var scope = app.Services.CreateAsyncScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    await MigrateDatabaseAsync(scope.ServiceProvider, logger, ct);
    await SeedDatabaseAsync(scope.ServiceProvider, logger, ct);
    await SyncDemoDataAsync(scope.ServiceProvider, app.Configuration, ct);
    await EnsureAdministrativeAccessAsync(scope.ServiceProvider, app.Environment, logger, ct);
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
    IConfiguration config,
    CancellationToken ct
)
{
    var demoSeeder = services.GetRequiredService<DemoDataSeeder>();
    if (config.GetValue("DEMO_MODE", false))
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        await demoSeeder.SeedAsync(cts.Token);
        return;
    }

    if (await demoSeeder.IsSeededAsync(ct))
    {
        throw new InvalidOperationException(
            "Demo data was found while DEMO_MODE=false. Delete the persistent db-data, api-files and api-dataprotection volumes before starting the stack without demo mode."
        );
    }
}

static async Task EnsureAdministrativeAccessAsync(
    IServiceProvider services,
    IHostEnvironment environment,
    ILogger<Program> logger,
    CancellationToken ct
)
{
    if (!environment.IsProduction())
    {
        return;
    }

    var db = services.GetRequiredService<CodigoActivoDbContext>();
    var hasUsableAdmin = await db.Users.AnyAsync(
        user =>
            user.IsAdmin
            && user.PasswordHash != null
            && user.UserStatusTypeId == SeedIds.UserStatusTypes.Active,
        ct
    );
    if (hasUsableAdmin)
    {
        return;
    }

    var registration = services.GetRequiredService<RegistrationOptions>();
    if (string.IsNullOrWhiteSpace(registration.BootstrapAdminEmail))
    {
        throw new InvalidOperationException(
            "No usable administrator exists. Set BOOTSTRAP_ADMIN_EMAIL before exposing registration."
        );
    }

    var bootstrapAccount = await db
        .Users.AsNoTracking()
        .Where(user => user.Email == registration.BootstrapAdminEmail)
        .Select(user => new
        {
            user.IsAdmin,
            user.PasswordHash,
            user.UserStatusTypeId,
        })
        .SingleOrDefaultAsync(ct);
    if (
        bootstrapAccount is not null
        && (
            !bootstrapAccount.IsAdmin
            || bootstrapAccount.PasswordHash is null
            || bootstrapAccount.UserStatusTypeId != SeedIds.UserStatusTypes.Pending
        )
    )
    {
        throw new InvalidOperationException(
            "BOOTSTRAP_ADMIN_EMAIL is already assigned to an account that cannot complete administrator bootstrap."
        );
    }

    if (bootstrapAccount is null)
    {
        logger.LogWarning(
            "No usable administrator exists; only the configured bootstrap address can register as administrator"
        );
    }
    else
    {
        logger.LogWarning(
            "The bootstrap administrator exists but must complete account verification"
        );
    }
}
