using CodigoActivo.API.Extensions;
using CodigoActivo.API.Security;
using CodigoActivo.Application.Options;
using CodigoActivo.Domain.Common;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;

namespace CodigoActivo.API.Configuration;

internal static class ApiSecurityConfiguration
{
    internal static void AddApiSecurity(this WebApplicationBuilder builder)
    {
        AddAntiforgery(builder);
        AddAuthentication(builder);
        AddAuthorization(builder.Services);
    }

    private static void AddAntiforgery(WebApplicationBuilder builder)
    {
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
    }

    private static void AddAuthentication(WebApplicationBuilder builder)
    {
        var session = ReadSessionLifetime(builder.Configuration);
        builder.Services.AddSingleton(session);

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
                options.ExpireTimeSpan = session.Lifetime;

                options.Events.OnRedirectToLogin = context =>
                    context.HttpContext.WriteApiErrorAsync(
                        Error.Unauthorized(ErrorCode.AuthenticationRequired)
                    );
                options.Events.OnRedirectToAccessDenied = context =>
                    context.HttpContext.WriteApiErrorAsync(
                        Error.Forbidden(ErrorCode.AccessDenied)
                    );
                options.Events.OnValidatePrincipal = context =>
                    context
                        .HttpContext.RequestServices.GetRequiredService<SessionTicketValidator>()
                        .ValidateAsync(context);
            })
            .AddCookie(
                TwoFactorAuthentication.Scheme,
                options =>
                {
                    options.Cookie.Name = builder.Environment.IsProduction()
                        ? "__Host-CodigoActivo.TwoFactor"
                        : "CodigoActivo.TwoFactor";
                    options.Cookie.HttpOnly = true;
                    options.Cookie.Path = "/";
                    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
                        ? CookieSecurePolicy.SameAsRequest
                        : CookieSecurePolicy.Always;
                    options.Cookie.SameSite = SameSiteMode.Lax;
                    options.SlidingExpiration = false;
                    options.ExpireTimeSpan = TwoFactorOptions.DefaultChallengeLifetime;

                    options.Events.OnRedirectToLogin = context =>
                        context.HttpContext.WriteApiErrorAsync(
                            Error.Unauthorized(ErrorCode.TwoFactorChallengeExpired)
                        );
                    options.Events.OnRedirectToAccessDenied = context =>
                        context.HttpContext.WriteApiErrorAsync(
                            Error.Forbidden(ErrorCode.AccessDenied)
                        );
                    options.Events.OnValidatePrincipal = context =>
                        context
                            .HttpContext.RequestServices.GetRequiredService<TwoFactorTicketValidator>()
                            .ValidateAsync(context);
                }
            );

        builder
            .Services.AddOptions<CookieAuthenticationOptions>(TwoFactorAuthentication.Scheme)
            .Configure<TwoFactorOptions>(
                (options, twoFactor) => options.ExpireTimeSpan = twoFactor.ChallengeLifetime
            );
    }

    private static SessionLifetimeOptions ReadSessionLifetime(IConfiguration configuration)
    {
        var hours = configuration.GetValue<double?>("Auth:ExpireHours");
        return new SessionLifetimeOptions
        {
            Lifetime =
                hours is { } value && double.IsFinite(value) && value > 0
                    ? TimeSpan.FromHours(value)
                    : SessionLifetimeOptions.DefaultLifetime,
        };
    }

    private static void AddAuthorization(IServiceCollection services)
    {
        services
            .AddAuthorizationBuilder()
            .SetFallbackPolicy(
                new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()
            );
    }
}
