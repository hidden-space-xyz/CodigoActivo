using CodigoActivo.API.Middlewares;
using CodigoActivo.API.OData;
using CodigoActivo.Composition;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Seeders;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OData;
using Microsoft.AspNetCore.OData.Query.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

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
            .SetMaxTop(1000)
            .AddRouteComponents(
                "api/odata",
                EdmModelBuilder.Build(),
                services => services.AddSingleton<IFilterBinder, DeaccentFilterBinder>()
            )
    );

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

        options.Events.OnRedirectToLogin = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization();

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
    c.SwaggerDoc(
        "v1",
        new OpenApiInfo
        {
            Title = "CodigoActivo API",
            Version = "v1",
            Description = "Backend API for CodigoActivo (cookie session auth + CSRF).",
        }
    );

    c.DocInclusionPredicate(
        (_, apiDescription) =>
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
});

var app = builder.Build();

await InitializeDatabaseAsync(app);

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

static SameSiteMode ResolveSameSite(string? value) =>
    value?.Trim().ToLowerInvariant() switch
    {
        "none" => SameSiteMode.None,
        "strict" => SameSiteMode.Strict,
        "lax" => SameSiteMode.Lax,
        _ => SameSiteMode.Lax,
    };

static async Task InitializeDatabaseAsync(WebApplication app)
{
    var config = app.Configuration;
    if (
        !config.GetValue("Database:MigrateOnStartup", true)
        && !config.GetValue("Database:SeedOnStartup", true)
    )
    {
        return;
    }

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CodigoActivoDbContext>();

    if (config.GetValue("Database:MigrateOnStartup", true))
    {
        await db.Database.MigrateAsync();
    }

    if (config.GetValue("Database:SeedOnStartup", true))
    {
        await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync();
    }
}
