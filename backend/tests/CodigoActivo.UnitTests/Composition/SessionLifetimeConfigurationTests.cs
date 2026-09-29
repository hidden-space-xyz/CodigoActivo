using AwesomeAssertions;
using CodigoActivo.Application.Accounts;
using CodigoActivo.Composition;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CodigoActivo.UnitTests.Composition;

public sealed class SessionLifetimeConfigurationTests
{
    private static SessionLifetimeOptions Build(string? expireHours)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { ["Auth:ExpireHours"] = expireHours }
            )
            .Build();
        return SessionLifetimeConfiguration.Read(configuration);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("not-a-number")]
    [InlineData("")]
    [InlineData("1e400")]
    [InlineData("1e300")]
    public void InvalidOrMissingExpireHoursFallsBackToTheDefaultLifetime(string? value)
    {
        Build(value).Lifetime.Should().Be(SessionLifetimeOptions.DefaultLifetime);
    }

    [Fact]
    public void ValidExpireHoursOverridesTheDefaultLifetime()
    {
        Build("2").Lifetime.Should().Be(TimeSpan.FromHours(2));
    }

    [Fact]
    public void DefaultLifetimeKeepsTheSessionForThirtyDays()
    {
        SessionLifetimeOptions.DefaultLifetime.Should().Be(TimeSpan.FromDays(30));
        new SessionLifetimeOptions().Lifetime.Should().Be(TimeSpan.FromDays(30));
    }
}
