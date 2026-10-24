using AwesomeAssertions;
using CodigoActivo.Composition;
using CodigoActivo.Infrastructure.Files;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace CodigoActivo.UnitTests.Composition;

public sealed class UnusedFilePurgeWiringTests
{
    [Fact]
    public void AddCodigoActivoDeletesUnusedUploadsHourlyAfterTheStartupDelay()
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
        var options = provider.GetRequiredService<UnusedFilePurgeOptions>();

        provider
            .GetServices<IHostedService>()
            .Should()
            .ContainSingle(service => service is UnusedFilePurgeWorker);
        options.Interval.Should().Be(TimeSpan.FromHours(1));
        options.StartupDelay.Should().Be(TimeSpan.FromSeconds(30));
    }
}
