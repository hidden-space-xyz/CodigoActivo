using System.Globalization;
using CodigoActivo.API.Extensions;
using CodigoActivo.API.Middlewares;
using CodigoActivo.API.OData;
using CodigoActivo.Composition;
using CodigoActivo.Domain.Common;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Seeders;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OData;
using Microsoft.AspNetCore.OData.Query.Expressions;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Context;
using Serilog.Events;
using Serilog.Formatting.Compact;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting CodigoActivo API");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, loggerConfiguration) =>
        {
            loggerConfiguration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .WriteTo.File(
                    new RenderedCompactJsonFormatter(),
                    Path.Combine(context.HostingEnvironment.ContentRootPath, "logs", "codigoactivo-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 14
                );

            loggerConfiguration.WriteTo.Console(new RenderedCompactJsonFormatter());
        }
    );

    builder.Services.AddCodigoActivo(builder.Configuration);

    DeaccentFilterBinder.EnsureFunctionRegistered();

    builder
        .Services.AddControllers()
        .AddOData(options =>
            options
                .Select()
                .Filter()
                .OrderBy()
                .Expand()
                .Count()
                .SetMaxTop(100)
                .AddRouteComponents(
                    "api/odata",
                    EdmModelBuilder.Build(),
                    services => services.AddSingleton<IFilterBinder, DeaccentFilterBinder>()
                )
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
        options.Cookie.SameSite = ResolveSameSite(builder.Configuration["Auth:SameSite"]);
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
            options.Cookie.SameSite = ResolveSameSite(builder.Configuration["Auth:SameSite"]);
            options.SlidingExpiration = true;
            options.ExpireTimeSpan = TimeSpan.FromHours(
                builder.Configuration.GetValue<double?>("Auth:ExpireHours") ?? 8
            );

            options.Events.OnRedirectToLogin = async ctx =>
            {
                var (statusCode, body) = ApiErrorResponseExtensions.Create(
                    Error.Unauthorized(ErrorCode.AuthenticationRequired),
                    ctx.HttpContext
                );
                ctx.Response.StatusCode = statusCode;
                await ctx.Response.WriteAsJsonAsync(body);
            };
            options.Events.OnRedirectToAccessDenied = async ctx =>
            {
                var (statusCode, body) = ApiErrorResponseExtensions.Create(
                    Error.Forbidden(ErrorCode.AccessDenied),
                    ctx.HttpContext
                );
                ctx.Response.StatusCode = statusCode;
                await ctx.Response.WriteAsJsonAsync(body);
            };
        });

    builder.Services.AddAuthorization();

    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();

    const string CorsPolicy = "Frontend";
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    builder.Services.AddCors(options =>
        options.AddPolicy(
            CorsPolicy,
            policy =>
                policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()
        )
    );

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.DocInclusionPredicate((_, apiDescription) =>
            !(apiDescription.RelativePath ?? string.Empty).StartsWith(
                "api/odata",
                StringComparison.OrdinalIgnoreCase
            )
        );

        var tagOverrides = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["EventCommands"] = "Events",
            ["AnnouncementCommands"] = "Announcements",
            ["ResourceCommands"] = "Resources",
            ["PartnerCommands"] = "Partners",
            ["ActivityCommands"] = "Activities",
            ["UserCommands"] = "Users",
            ["FileCommands"] = "Files",
        };
        c.TagActionsBy(api =>
            api.ActionDescriptor is ControllerActionDescriptor descriptor
                ? [tagOverrides.GetValueOrDefault(descriptor.ControllerName, descriptor.ControllerName)]
                : [api.GroupName ?? "default"]
        );

        c.OperationFilter<JsonResponseMediaTypeFilter>();
        c.DocumentFilter<ApiErrorResponseDocumentFilter>();
    });

    var app = builder.Build();

    await InitializeDatabaseAsync(app);

    app.Use(async (httpContext, next) =>
        {
            using (LogContext.PushProperty("CorrelationId", httpContext.GetOrSetTraceId()))
            {
                await next();
            }
        }
    );

    app.UseSerilogRequestLogging(options =>
    {
        options.GetLevel = static (httpContext, _, ex) => ResolveRequestLogLevel(httpContext, ex);
    });

    app.UseExceptionHandler();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "CodigoActivo API v1"));
    }

    app.UseHttpsRedirection();
    app.UseCors(CorsPolicy);

    app.UseAuthentication();
    app.UseAuthorization();

    app.UseMiddleware<CsrfValidationMiddleware>();

    app.MapControllers();

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "CodigoActivo API terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

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

static LogEventLevel ResolveRequestLogLevel(HttpContext httpContext, Exception? ex)
{
    if (ex is not null || httpContext.Response.StatusCode >= StatusCodes.Status500InternalServerError)
        return LogEventLevel.Error;

    return httpContext.Response.StatusCode >= StatusCodes.Status400BadRequest
        ? LogEventLevel.Warning
        : LogEventLevel.Information;
}

static async Task InitializeDatabaseAsync(WebApplication app)
{
    var config = app.Configuration;
    if (
        !config.GetValue("Database:MigrateOnStartup", true)
        && !config.GetValue("Database:SeedOnStartup", true)
    )
        return;

    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<CodigoActivoDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    if (config.GetValue("Database:MigrateOnStartup", true))
    {
        logger.LogInformation("Applying database migrations");
        await db.Database.MigrateAsync();
        logger.LogInformation("Database migrations applied");
    }

    if (config.GetValue("Database:SeedOnStartup", true))
    {
        logger.LogInformation("Seeding database");
        await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync();
        logger.LogInformation("Database seeding complete");
    }
}