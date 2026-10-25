using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Accounts;
using CodigoActivo.Application.Accounts.EmailAddresses;
using CodigoActivo.Application.Accounts.Erasure;
using CodigoActivo.Application.Accounts.Notifications;
using CodigoActivo.Application.Accounts.Passwords;
using CodigoActivo.Application.Accounts.SecondFactor;
using CodigoActivo.Application.Accounts.Sessions;
using CodigoActivo.Composition.Configuration;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Accounts;
using CodigoActivo.Infrastructure.Communication.Templates;
using CodigoActivo.Infrastructure.Security;
using CodigoActivo.Infrastructure.Users;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CodigoActivo.Composition.Accounts;

/// <summary>
/// Registers accounts: credentials, second factor, sessions, verification, the emails they send and
/// the erasure of accounts.
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
        services.AddScoped<IDisposableEmailDomainStore, DisposableEmailDomainStore>();
        services.AddScoped<IDisposableEmailDomainLookup>(provider =>
            provider.GetRequiredService<IDisposableEmailDomainStore>()
        );
        services.AddSingleton<IPasswordHasher, Argon2idPasswordHasher>();
        services.AddSingleton<ITotpService, TotpService>();
        services.AddSingleton<IAccountEmailComposer, AccountEmailComposer>();
        services.AddSingleton<CredentialTimingProtector>();
        services.AddScoped<AccountEmails>();
        services.AddScoped<AccountSecurityNotifier>();
        services.AddScoped<AccountLock>();
        services.AddScoped<PasswordAttemptGuard>();
        services.AddScoped<SecondFactorGuard>();
        services.AddScoped<OtpValidator>();
        services.AddScoped<LoginCodeIssuer>();
        services.AddScoped<EmailChangeLinkIssuer>();
        services.AddScoped<EmailClaims>();
        services.AddScoped<IAccountErasureStore, AccountErasureStore>();
        services.AddScoped<AccountEraser>();
        services.AddScoped<PersonalDataReader>();
        services.AddScoped<AuthenticatorCodeVerifier>();
        services.AddScoped<DisposableEmailChecker>();
        services.AddHostedService<ExpiredSessionCleanupWorker>();
        services.AddSingleton(provider => new DisposableEmailDomainDownloader(
            new SocketsHttpHandler { AutomaticDecompression = System.Net.DecompressionMethods.All },
            provider.GetRequiredService<DisposableEmailDomainOptions>()
        ));
        services.AddHostedService<DisposableEmailDomainRefreshWorker>();
        return services;
    }

    private static void AddOptions(IServiceCollection services, Settings settings)
    {
        services.AddFixedOptions<AccountVerificationOptions>();
        services.AddFixedOptions<PasswordResetOptions>();
        services.AddFixedOptions<PasswordLockoutOptions>();
        services.AddFixedOptions<TwoFactorOptions>();
        services.AddFixedOptions<SessionLifetimeOptions>();
        services.AddFixedOptions<SessionCleanupOptions>();
        services.AddFixedOptions<DisposableEmailDomainOptions>();
        services.AddValidatedOptions<InitialAdministratorOptions>(options =>
            options.Email = settings.Text("BOOTSTRAP_ADMIN_EMAIL", string.Empty)
        );
    }
}
