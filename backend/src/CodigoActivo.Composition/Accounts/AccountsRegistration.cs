using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Accounts;
using CodigoActivo.Composition.Configuration;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Communication;
using CodigoActivo.Infrastructure.Communication.Templates;
using CodigoActivo.Infrastructure.Database;
using CodigoActivo.Infrastructure.Database.Repositories;
using CodigoActivo.Infrastructure.Security;
using CodigoActivo.Infrastructure.Users;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CodigoActivo.Composition.Accounts;

/// <summary>
/// Registers accounts: credentials, second factor, sessions, verification and the emails they send.
/// </summary>
internal static class AccountsRegistration
{
    /// <summary>
    /// Adds the account services.
    /// </summary>
    /// <param name="services">Service collection to add to.</param>
    /// <param name="configuration">Configuration of the application.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddAccounts(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var settings = new Settings(configuration);
        AddOptions(services, settings);

        services.AddScoped<IUserSessionRepository, UserSessionRepository>();
        services.AddScoped<IDisposableEmailDomainStore, DisposableEmailDomainRepository>();
        services.AddScoped<IDisposableEmailDomainRepository>(provider =>
            provider.GetRequiredService<IDisposableEmailDomainStore>()
        );
        services.AddSingleton<IPasswordHasher, Argon2idPasswordHasher>();
        services.AddSingleton<ITotpService, TotpService>();
        services.AddSingleton<IAccountEmailComposer, AccountEmailComposer>();
        services.AddSingleton<CredentialTimingProtector>();
        services.AddScoped<AccountEmails>();
        services.AddScoped<AccountSecurityNotifier>();
        services.AddScoped<PasswordAttemptGuard>();
        services.AddScoped<OtpValidator>();
        services.AddScoped<LoginCodeIssuer>();
        services.AddScoped<EmailChangeLinkIssuer>();
        services.AddScoped<EmailClaims>();
        services.AddScoped<AuthenticatorCodeVerifier>();
        services.AddScoped<DisposableEmailChecker>();
        services.AddHostedService<ExpiredSessionCleaner>();
        services.AddSingleton(provider => new DisposableEmailDomainDownloader(
            new SocketsHttpHandler { AutomaticDecompression = System.Net.DecompressionMethods.All },
            provider.GetRequiredService<DisposableEmailDomainOptions>()
        ));
        services.AddHostedService<DisposableEmailDomainRefresher>();
        return services;
    }

    private static void AddOptions(IServiceCollection services, Settings settings)
    {
        services.AddValidatedOptions<AccountVerificationOptions>(options =>
        {
            options.OtpLifetime = settings.Duration(
                "AccountVerification:OtpLifetimeMinutes",
                TimeSpan.FromMinutes,
                AccountVerificationOptions.DefaultOtpLifetime
            );
            options.ResendCooldown = settings.Duration(
                "AccountVerification:ResendCooldownSeconds",
                TimeSpan.FromSeconds,
                AccountVerificationOptions.DefaultResendCooldown
            );
        });
        services.AddValidatedOptions<PasswordResetOptions>(options =>
        {
            options.CodeLifetime = settings.Duration(
                "PasswordReset:CodeLifetimeMinutes",
                TimeSpan.FromMinutes,
                PasswordResetOptions.DefaultCodeLifetime
            );
            options.ResendCooldown = settings.Duration(
                "PasswordReset:ResendCooldownSeconds",
                TimeSpan.FromSeconds,
                PasswordResetOptions.DefaultResendCooldown
            );
        });
        services.AddValidatedOptions<PasswordLockoutOptions>(options =>
            options.MaxFailedAttempts = settings.PositiveInt(
                "PasswordLockout:MaxFailedAttempts",
                PasswordLockoutOptions.DefaultMaxFailedAttempts
            )
        );
        services.AddValidatedOptions<TwoFactorOptions>(options =>
        {
            options.ChallengeLifetime = settings.Duration(
                "TwoFactor:ChallengeLifetimeMinutes",
                TimeSpan.FromMinutes,
                TwoFactorOptions.DefaultChallengeLifetime
            );
            options.ResendCooldown = settings.Duration(
                "TwoFactor:ResendCooldownSeconds",
                TimeSpan.FromSeconds,
                TwoFactorOptions.DefaultResendCooldown
            );
            options.SetupLifetime = settings.Duration(
                "TwoFactor:AuthenticatorSetupLifetimeMinutes",
                TimeSpan.FromMinutes,
                TwoFactorOptions.DefaultSetupLifetime
            );
            options.MaxFailedAttempts = settings.PositiveInt(
                "TwoFactor:MaxFailedAttempts",
                TwoFactorOptions.DefaultMaxFailedAttempts
            );
            options.LockoutDuration = settings.Duration(
                "TwoFactor:LockoutMinutes",
                TimeSpan.FromMinutes,
                TwoFactorOptions.DefaultLockoutDuration
            );
            options.Issuer = settings.Text("TwoFactor:Issuer", TwoFactorOptions.DefaultIssuer);
        });
        services.AddValidatedOptions<SessionLifetimeOptions>(options =>
            options.Lifetime = settings.Duration(
                "Auth:ExpireHours",
                TimeSpan.FromHours,
                SessionLifetimeOptions.DefaultLifetime
            )
        );
        services.AddValidatedOptions<SessionCleanupOptions>(
            options =>
                options.Interval = settings.Duration(
                    "SessionCleanup:IntervalMinutes",
                    TimeSpan.FromMinutes,
                    SessionCleanupOptions.DefaultInterval
                ),
            options => options.Interval <= SessionCleanupOptions.MaxInterval,
            $"SessionCleanup:IntervalMinutes must be at most {SessionCleanupOptions.MaxInterval.TotalMinutes} minutes."
        );
        services.AddValidatedOptions<DisposableEmailDomainOptions>(options =>
            options.SourceUrl = settings.HttpsUri(
                "DisposableEmailDomains:SourceUrl",
                DisposableEmailDomainOptions.DefaultSourceUrl
            )
        );
    }
}
