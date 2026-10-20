using CodigoActivo.API.Errors;
using CodigoActivo.API.Extensions;
using CodigoActivo.API.Security;
using CodigoActivo.Application.Accounts.SecondFactor;
using CodigoActivo.Application.Accounts.Sessions;
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
            ApplyCookiePolicy(options.Cookie, builder.Environment, "CodigoActivo.Csrf");
        });
    }

    private static void ApplyCookiePolicy(
        CookieBuilder cookie,
        IHostEnvironment environment,
        string name
    )
    {
        cookie.Name = environment.IsProduction() ? "__Host-" + name : name;
        cookie.HttpOnly = true;
        cookie.Path = "/";
        cookie.SecurePolicy = environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        cookie.SameSite = SameSiteMode.Lax;
    }

    private static void AddAuthentication(WebApplicationBuilder builder)
    {
        builder
            .Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                ApplyCookiePolicy(options.Cookie, builder.Environment, "CodigoActivo.Session");
                options.SlidingExpiration = false;

                options.Events.OnRedirectToLogin = context =>
                    context.HttpContext.WriteApiErrorAsync(
                        ApiError.Unauthorized(ErrorCode.AuthenticationRequired)
                    );
                options.Events.OnRedirectToAccessDenied = context =>
                    context.HttpContext.WriteApiErrorAsync(
                        ApiError.Forbidden(ErrorCode.AccessDenied)
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
                    ApplyCookiePolicy(
                        options.Cookie,
                        builder.Environment,
                        "CodigoActivo.TwoFactor"
                    );
                    options.SlidingExpiration = false;

                    options.Events.OnRedirectToLogin = context =>
                        context.HttpContext.WriteApiErrorAsync(
                            ApiError.Unauthorized(ErrorCode.TwoFactorChallengeExpired)
                        );
                    options.Events.OnRedirectToAccessDenied = context =>
                        context.HttpContext.WriteApiErrorAsync(
                            ApiError.Forbidden(ErrorCode.AccessDenied)
                        );
                    options.Events.OnValidatePrincipal = context =>
                        context
                            .HttpContext.RequestServices.GetRequiredService<TwoFactorTicketValidator>()
                            .ValidateAsync(context);
                }
            );

        builder
            .Services.AddOptions<CookieAuthenticationOptions>(
                CookieAuthenticationDefaults.AuthenticationScheme
            )
            .Configure<SessionLifetimeOptions>(
                (options, session) => options.ExpireTimeSpan = session.Lifetime
            );

        builder
            .Services.AddOptions<CookieAuthenticationOptions>(TwoFactorAuthentication.Scheme)
            .Configure<TwoFactorOptions>(
                (options, twoFactor) => options.ExpireTimeSpan = twoFactor.ChallengeLifetime
            );
    }

    private static void AddAuthorization(IServiceCollection services)
    {
        services
            .AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
    }
}
