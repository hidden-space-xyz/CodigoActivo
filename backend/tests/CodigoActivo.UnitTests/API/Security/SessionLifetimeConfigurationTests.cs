using AwesomeAssertions;
using CodigoActivo.API.Configuration;
using CodigoActivo.API.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CodigoActivo.UnitTests.API.Security;

public sealed class SessionLifetimeConfigurationTests
{
    private static SessionLifetimeOptions Build(string? expireHours)
    {
        var builder = WebApplication.CreateBuilder();
        if (expireHours is not null)
        {
            builder.Configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Auth:ExpireHours"] = expireHours }
            );
        }

        builder.AddApiSecurity();

        return (SessionLifetimeOptions)
            builder
                .Services.Single(descriptor => descriptor.ServiceType == typeof(SessionLifetimeOptions))
                .ImplementationInstance!;
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
}
