using System.Text.Json.Serialization;
using CodigoActivo.API.Caching;
using CodigoActivo.API.Extensions;
using CodigoActivo.API.Middlewares;
using CodigoActivo.API.OpenApi;
using CodigoActivo.Application.Caching;
using CodigoActivo.Composition;
using CodigoActivo.Domain.Common;
using CodigoActivo.Infrastructure.Communication;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Seeders;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.IncludeScopes = true;
    options.SingleLine = true;
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff zzz ";
});

builder.Services.AddCodigoActivo(builder.Configuration);

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
    options.Cookie.Name = "CodigoActivo.Csrf";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.Cookie.SameSite = ResolveSameSite(builder.Configuration["AUTH_SAMESITE"]);
});

builder
    .Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = builder.Configuration["Auth:CookieName"] ?? "CodigoActivo.Session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.Cookie.SameSite = ResolveSameSite(builder.Configuration["AUTH_SAMESITE"]);
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(
            builder.Configuration.GetValue<double?>("Auth:ExpireHours") ?? 8
        );

        options.Events.OnRedirectToLogin = ctx =>
            ctx.HttpContext.WriteApiErrorAsync(
                Error.Unauthorized(ErrorCode.AuthenticationRequired)
            );
        options.Events.OnRedirectToAccessDenied = ctx =>
            ctx.HttpContext.WriteApiErrorAsync(Error.Forbidden(ErrorCode.AccessDenied));
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

app.UseMiddleware<RequestLoggingMiddleware>();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "CodigoActivo API v1"));
}

app.UseHttpsRedirection();

app.UseMiddleware<CacheControlMiddleware>();

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

static async Task InitializeDatabaseAsync(WebApplication app, CancellationToken ct)
{
    await using var scope = app.Services.CreateAsyncScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    await MigrateDatabaseAsync(scope.ServiceProvider, logger, ct);
    await SeedDatabaseAsync(scope.ServiceProvider, logger, ct);
    await SyncDemoDataAsync(scope.ServiceProvider, app.Configuration, logger, ct);
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
    ILogger<Program> logger,
    CancellationToken ct
)
{
    var demoSeeder = services.GetRequiredService<DemoDataSeeder>();
    try
    {
        if (config.GetValue("DEMO_MODE", false))
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            await demoSeeder.SeedAsync(cts.Token);
        }
        else
        {
            await demoSeeder.RemoveAsync(ct);
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Demo data synchronization failed");
    }
}
