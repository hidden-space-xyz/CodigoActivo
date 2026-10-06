using AwesomeAssertions;
using CodigoActivo.Application.Accounts;
using CodigoActivo.Composition;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodigoActivo.UnitTests.Composition;

public sealed class SessionLifetimeConfigurationTests
{
    private static SessionLifetimeOptions Build(string? expireHours)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Auth:ExpireHours"] = expireHours,
                    ["SMTP_HOST"] = "smtp.example.test",
                    ["SMTP_FROM_ADDRESS"] = "no-reply@example.test",
                }
            )
            .Build();
        using var provider = new ServiceCollection()
            .AddLogging()
            .AddCodigoActivo(configuration)
            .BuildServiceProvider();
        return provider.GetRequiredService<SessionLifetimeOptions>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void MissingExpireHoursTakesTheDefaultLifetime(string? value)
    {
        Build(value).Lifetime.Should().Be(SessionLifetimeOptions.DefaultLifetime);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("not-a-number")]
    [InlineData("1e400")]
    [InlineData("1e300")]
    public void UnusableExpireHoursStopsTheStart(string value)
    {
        var act = () => Build(value);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Auth:ExpireHours*");
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
