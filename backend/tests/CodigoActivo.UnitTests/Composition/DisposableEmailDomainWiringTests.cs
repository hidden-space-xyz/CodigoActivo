using AwesomeAssertions;
using CodigoActivo.Composition;
using CodigoActivo.Infrastructure.Accounts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace CodigoActivo.UnitTests.Composition;

public sealed class DisposableEmailDomainWiringTests
{
    [Fact]
    public void AddCodigoActivoSchedulesTheDailyRefreshAsAHostedService()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["SMTP_HOST"] = "smtp.example.test",
                    ["SMTP_FROM_ADDRESS"] = "no-reply@example.test",
                }
            )
            .Build();
        using var provider = new ServiceCollection()
            .AddLogging()
            .AddCodigoActivo(configuration)
            .BuildServiceProvider();

        var options = provider.GetRequiredService<DisposableEmailDomainOptions>();

        provider
            .GetServices<IHostedService>()
            .Should()
            .ContainSingle(service => service is DisposableEmailDomainRefreshWorker);
        options.RefreshInterval.Should().Be(TimeSpan.FromDays(1));
        options.RetryInterval.Should().Be(TimeSpan.FromHours(1));
    }
}
