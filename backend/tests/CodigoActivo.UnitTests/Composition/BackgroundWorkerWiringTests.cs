using AwesomeAssertions;
using CodigoActivo.Composition;
using CodigoActivo.Infrastructure.DataHistory;
using CodigoActivo.Infrastructure.Legal;
using CodigoActivo.Infrastructure.Logs;
using CodigoActivo.Infrastructure.Users;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace CodigoActivo.UnitTests.Composition;

public sealed class BackgroundWorkerWiringTests
{
    [Theory]
    [InlineData(typeof(DataHistoryPurgeWorker))]
    [InlineData(typeof(LegalNoticeWorker))]
    [InlineData(typeof(LogRetentionWorker))]
    [InlineData(typeof(UnverifiedAccountPurgeWorker))]
    public void AddCodigoActivoRegistersTheWorkerAsAHostedService(Type worker)
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
        using var provider = new ServiceCollection()
            .AddLogging()
            .AddCodigoActivo(configuration)
            .BuildServiceProvider();

        provider
            .GetServices<IHostedService>()
            .Should()
            .ContainSingle(service => service.GetType() == worker);
    }
}
