using AwesomeAssertions;
using CodigoActivo.Composition;
using CodigoActivo.Infrastructure.Database;
using CodigoActivo.Infrastructure.Users;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodigoActivo.UnitTests.Composition;

public sealed class SessionCleanupWiringTests
{
    private static IServiceCollection Build(string? intervalMinutes)
    {
        var settings = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["SMTP_HOST"] = "smtp.example.test",
            ["SMTP_FROM_ADDRESS"] = "no-reply@example.test",
        };
        if (intervalMinutes is not null)
        {
            settings["SessionCleanup:IntervalMinutes"] = intervalMinutes;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new ServiceCollection().AddLogging().AddCodigoActivo(configuration);
    }

    private static SessionCleanupOptions Options(string? intervalMinutes)
    {
        using var provider = Build(intervalMinutes).BuildServiceProvider();
        return provider.GetRequiredService<SessionCleanupOptions>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void MissingIntervalTakesTheDefault(string? value)
    {
        Options(value).Interval.Should().Be(SessionCleanupOptions.DefaultInterval);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("not-a-number")]
    public void UnusableIntervalStopsTheStart(string value)
    {
        var act = () => Options(value);

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*SessionCleanup:IntervalMinutes*");
    }

    [Fact]
    public void ValidIntervalOverridesTheDefault()
    {
        Options("15").Interval.Should().Be(TimeSpan.FromMinutes(15));
    }

    [Fact]
    public void IntervalAboveTheMaximumStopsTheStart()
    {
        var act = () => Options("100000");

        act.Should()
            .Throw<OptionsValidationException>()
            .WithMessage("*SessionCleanup:IntervalMinutes*");
    }

    [Fact]
    public void AddCodigoActivoRegistersTheCleanerAsAHostedService()
    {
        using var provider = Build(null).BuildServiceProvider();

        provider
            .GetServices<IHostedService>()
            .Should()
            .ContainSingle(service => service is ExpiredSessionCleaner);
    }
}
