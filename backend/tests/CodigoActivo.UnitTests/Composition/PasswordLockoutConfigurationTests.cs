using AwesomeAssertions;
using CodigoActivo.Application.Auth;
using CodigoActivo.Application.Options;
using CodigoActivo.Composition;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodigoActivo.UnitTests.Composition;

public sealed class PasswordLockoutConfigurationTests : IDisposable
{
    private readonly List<ServiceProvider> providers = [];

    public void Dispose()
    {
        foreach (var provider in providers)
        {
            provider.Dispose();
        }
    }

    private ServiceProvider Build(Dictionary<string, string?> settings)
    {
        settings["SMTP_HOST"] = "smtp.example.test";
        settings["SMTP_FROM_ADDRESS"] = "no-reply@example.test";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var provider = new ServiceCollection()
            .AddLogging()
            .AddCodigoActivo(configuration)
            .BuildServiceProvider();
        providers.Add(provider);
        return provider;
    }

    [Fact]
    public void AddCodigoActivoValidPasswordLockoutSettingBindsOptionsFromConfiguration()
    {
        var provider = Build(
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["PasswordLockout:MaxFailedAttempts"] = "8",
            }
        );

        provider.GetRequiredService<PasswordLockoutOptions>().MaxFailedAttempts.Should().Be(8);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("zero")]
    [InlineData("0")]
    [InlineData("-3")]
    public void AddCodigoActivoMissingOrInvalidValueDefaultsPasswordLockoutOptions(string? value)
    {
        var provider = Build(
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["PasswordLockout:MaxFailedAttempts"] = value,
            }
        );

        provider
            .GetRequiredService<PasswordLockoutOptions>()
            .MaxFailedAttempts.Should()
            .Be(PasswordLockoutOptions.DefaultMaxFailedAttempts);
    }

    [Fact]
    public void AddCodigoActivoRegistersThePasswordAttemptGuardPerScope()
    {
        var provider = Build(new Dictionary<string, string?>(StringComparer.Ordinal));

        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<PasswordAttemptGuard>().Should().NotBeNull();
    }

    [Fact]
    public void DefaultMaxFailedAttemptsIsFive()
    {
        PasswordLockoutOptions.DefaultMaxFailedAttempts.Should().Be(5);
    }
}
