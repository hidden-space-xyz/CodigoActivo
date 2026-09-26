using AwesomeAssertions;
using CodigoActivo.Composition;
using CodigoActivo.Infrastructure.Communication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace CodigoActivo.UnitTests.Composition;

public sealed class DisposableEmailDomainWiringTests
{
    private static IServiceCollection Build(string? sourceUrl)
    {
        var settings = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["SMTP_HOST"] = "smtp.example.test",
            ["SMTP_FROM_ADDRESS"] = "no-reply@example.test",
        };
        if (sourceUrl is not null)
        {
            settings["DisposableEmailDomains:SourceUrl"] = sourceUrl;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new ServiceCollection().AddLogging().AddCodigoActivo(configuration);
    }

    private static DisposableEmailDomainOptions Options(string? sourceUrl)
    {
        return (DisposableEmailDomainOptions)
            Build(sourceUrl)
                .Single(descriptor =>
                    descriptor.ServiceType == typeof(DisposableEmailDomainOptions)
                )
                .ImplementationInstance!;
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("/relative/list.conf")]
    [InlineData("http://mirror.example.test/list.conf")]
    [InlineData("ftp://mirror.example.test/list.conf")]
    public void SourceUrlMissingInvalidOrInsecureFallsBackToTheDefault(string? value)
    {
        Options(value).SourceUrl.Should().Be(DisposableEmailDomainOptions.DefaultSourceUrl);
    }

    [Fact]
    public void SourceUrlAbsoluteHttpsOverridesTheDefault()
    {
        Options("https://mirror.example.test/list.conf")
            .SourceUrl.Should()
            .Be(new Uri("https://mirror.example.test/list.conf"));
    }

    [Fact]
    public void AddCodigoActivoSchedulesTheDailyRefreshAsAHostedService()
    {
        using var provider = Build(null).BuildServiceProvider();

        var options = provider.GetRequiredService<DisposableEmailDomainOptions>();

        provider
            .GetServices<IHostedService>()
            .Should()
            .ContainSingle(service => service is DisposableEmailDomainRefresher);
        options.RefreshInterval.Should().Be(TimeSpan.FromDays(1));
        options.RetryInterval.Should().Be(TimeSpan.FromHours(1));
    }
}
