using AwesomeAssertions;
using CodigoActivo.Application.Accounts.Notifications;
using CodigoActivo.Application.Accounts.Passwords;
using CodigoActivo.Application.Accounts.SecondFactor;
using CodigoActivo.Application.Accounts.Sessions;
using CodigoActivo.Application.Emails;
using CodigoActivo.Application.Files;
using CodigoActivo.Composition;
using CodigoActivo.Infrastructure.Accounts;
using CodigoActivo.Infrastructure.Communication;
using CodigoActivo.Infrastructure.Files;
using CodigoActivo.Infrastructure.Logs;
using CodigoActivo.Infrastructure.Security;
using CodigoActivo.Infrastructure.Users;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodigoActivo.UnitTests.Composition;

public sealed class FixedOptionsTests : IDisposable
{
    private readonly ServiceProvider provider;

    public FixedOptionsTests()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["SMTP_HOST"] = "smtp.example.test",
                    ["SMTP_FROM_ADDRESS"] = "no-reply@example.test",
                    ["Auth:ExpireHours"] = "87600",
                    ["SessionCleanup:IntervalMinutes"] = "1",
                    ["AccountVerification:OtpLifetimeMinutes"] = "10080",
                    ["PasswordReset:CodeLifetimeMinutes"] = "10080",
                    ["PasswordLockout:MaxFailedAttempts"] = "100000",
                    ["TwoFactor:ChallengeLifetimeMinutes"] = "10080",
                    ["TwoFactor:MaxFailedAttempts"] = "100000",
                    ["TwoFactor:LockoutMinutes"] = "0.01",
                    ["TwoFactor:Issuer"] = "Otra Asociación",
                    ["DisposableEmailDomains:SourceUrl"] = "https://lists.example.test/empty.conf",
                    ["FileStorage:MaxSizeBytes"] = "1099511627776",
                    ["FileStorage:RootPath"] = "/tmp",
                    ["KeyRing:DirectoryPath"] = "/tmp",
                    ["ManualEmail:MaxRecipients"] = "1000000",
                    ["EmailGuard:RecipientPerDay"] = "1000000",
                    ["EmailGuard:GlobalPerHour"] = "1000000",
                    ["EmailQueue:Workers"] = "1000",
                    ["DIAGNOSTIC_LOG_RETENTION_DAYS"] = "3650",
                    ["AUDIT_LOG_RETENTION_DAYS"] = "1",
                }
            )
            .Build();
        provider = new ServiceCollection()
            .AddLogging()
            .AddCodigoActivo(configuration)
            .BuildServiceProvider();
    }

    public void Dispose()
    {
        provider.Dispose();
    }

    [Fact]
    public void AddCodigoActivoSettingsCannotChangeTheSecurityAndRetentionPolicy()
    {
        Fixed<SessionLifetimeOptions>();
        Fixed<SessionCleanupOptions>();
        Fixed<AccountVerificationOptions>();
        Fixed<PasswordResetOptions>();
        Fixed<PasswordLockoutOptions>();
        Fixed<TwoFactorOptions>();
        Fixed<DisposableEmailDomainOptions>();
        Fixed<FileUploadOptions>();
        Fixed<FileStorageOptions>();
        Fixed<KeyRingOptions>();
        Fixed<ManualEmailOptions>();
        Fixed<EmailGuardOptions>();
        Fixed<EmailQueueOptions>();
        Fixed<LogRetentionOptions>();
    }

    private void Fixed<TOptions>()
        where TOptions : class, new()
    {
        provider.GetRequiredService<TOptions>().Should().BeEquivalentTo(new TOptions());
    }
}
