using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Composition;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodigoActivo.UnitTests.Composition;

public sealed class TimeZoneConfigurationTests
{
    private static IClock Build(string? timeZone)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["APP_TIMEZONE"] = timeZone,
                    ["SMTP_HOST"] = "smtp.example.test",
                    ["SMTP_FROM_ADDRESS"] = "no-reply@example.test",
                }
            )
            .Build();
        using var provider = new ServiceCollection()
            .AddLogging()
            .AddCodigoActivo(configuration)
            .BuildServiceProvider();
        return provider.GetRequiredService<IClock>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void MissingTimeZoneTakesTheMachineTimeZone(string? value)
    {
        Build(value).TimeZone.Should().Be(TimeZoneInfo.Local);
    }

    [Fact]
    public void IanaTimeZoneSetsTheApplicationClock()
    {
        Build("Europe/Madrid").TimeZone.HasIanaId.Should().BeTrue();
    }

    [Theory]
    [InlineData("Romance Standard Time")]
    [InlineData("Not/AZone")]
    public void NonIanaTimeZoneStopsTheStart(string value)
    {
        var act = () => Build(value);

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*APP_TIMEZONE*IANA time zone*");
    }
}
