using AwesomeAssertions;
using CodigoActivo.Application.Accounts.Commands;
using CodigoActivo.Application.BlockedData.Commands;
using CodigoActivo.Application.Files.Commands;
using CodigoActivo.Application.TermsDecisions.Commands;
using CodigoActivo.Application.Users.Commands;
using CodigoActivo.Composition;
using CodigoActivo.Infrastructure.Logs;
using CodigoActivo.Infrastructure.Messaging;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace CodigoActivo.UnitTests.Composition;

public sealed class BackgroundWorkerWiringTests
{
    private static ServiceProvider Build()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["SMTP_HOST"] = "smtp.example.test",
                    ["SMTP_FROM_ADDRESS"] = "no-reply@example.test",
                    ["DATA_PROTECTION_KEY"] = DataProtectionRootKeys.RandomBase64(),
                }
            )
            .Build();
        return new ServiceCollection()
            .AddLogging()
            .AddCodigoActivo(configuration)
            .BuildServiceProvider();
    }

    [Theory]
    [InlineData(typeof(LogRetentionWorker))]
    [InlineData(typeof(ScheduledJobsWorker))]
    public void AddCodigoActivoRegistersTheWorkerAsAHostedService(Type worker)
    {
        using var provider = Build();

        provider
            .GetServices<IHostedService>()
            .Should()
            .ContainSingle(service => service.GetType() == worker);
    }

    [Fact]
    public void AddCodigoActivoSchedulesEveryJobThirtySecondsAfterStartupAndThenOnItsInterval()
    {
        using var provider = Build();
        var hourly = TimeSpan.FromHours(1);
        var everyMinute = TimeSpan.FromMinutes(1);

        provider
            .GetServices<ScheduledJob>()
            .Should()
            .OnlyContain(job => job.StartupDelay == TimeSpan.FromSeconds(30))
            .And.Subject.Select(job => (job.Name, job.Interval))
            .Should()
            .BeEquivalentTo([
                (nameof(RemoveExpiredSessionsCommand), hourly),
                (nameof(DiscardExpiredAccountChangesCommand), hourly),
                (nameof(PurgeUnverifiedAccountsCommand), hourly),
                (nameof(PurgeAdultDependentsCommand), hourly),
                (nameof(PurgeUnusedFilesCommand), hourly),
                (nameof(PurgeDataHistoryCommand), hourly),
                (nameof(SendConsentWithdrawalNoticesCommand), everyMinute),
                (nameof(SendBlockedDataConsultationNoticesCommand), everyMinute),
            ]);
    }
}
