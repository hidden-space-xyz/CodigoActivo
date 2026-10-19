using AwesomeAssertions;
using CodigoActivo.Composition;
using CodigoActivo.Infrastructure.Users;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace CodigoActivo.UnitTests.Composition;

public sealed class AdultDependentPurgeWiringTests
{
    [Fact]
    public void AddCodigoActivoErasesAdultDependentsHourlyAfterTheStartupDelay()
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
        var options = provider.GetRequiredService<AdultDependentPurgeOptions>();

        provider
            .GetServices<IHostedService>()
            .Should()
            .ContainSingle(service => service is AdultDependentPurger);
        options.Interval.Should().Be(TimeSpan.FromHours(1));
        options.StartupDelay.Should().Be(TimeSpan.FromSeconds(30));
    }
}
