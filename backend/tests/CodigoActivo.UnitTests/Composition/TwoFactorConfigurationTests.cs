using AwesomeAssertions;
using CodigoActivo.Application.Options;
using CodigoActivo.Composition;
using CodigoActivo.Domain.Security;
using CodigoActivo.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodigoActivo.UnitTests.Composition;

public sealed class TwoFactorConfigurationTests : IDisposable
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
    public void AddCodigoActivoValidTwoFactorSettingsBindsOptionsFromConfiguration()
    {
        var provider = Build(
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["TwoFactor:ChallengeLifetimeMinutes"] = "5",
                ["TwoFactor:ResendCooldownSeconds"] = "30",
                ["TwoFactor:AuthenticatorSetupLifetimeMinutes"] = "20",
                ["TwoFactor:MaxFailedAttempts"] = "3",
                ["TwoFactor:LockoutMinutes"] = "45",
                ["TwoFactor:Issuer"] = "  Mi Asociación ",
            }
        );

        var options = provider.GetRequiredService<TwoFactorOptions>();
        options.ChallengeLifetime.Should().Be(TimeSpan.FromMinutes(5));
        options.ResendCooldown.Should().Be(TimeSpan.FromSeconds(30));
        options.SetupLifetime.Should().Be(TimeSpan.FromMinutes(20));
        options.MaxFailedAttempts.Should().Be(3);
        options.LockoutDuration.Should().Be(TimeSpan.FromMinutes(45));
        options.Issuer.Should().Be("Mi Asociación");
    }

    [Fact]
    public void AddCodigoActivoMissingOrInvalidValuesDefaultsTwoFactorOptions()
    {
        var provider = Build(
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["TwoFactor:ChallengeLifetimeMinutes"] = "-1",
                ["TwoFactor:MaxFailedAttempts"] = "zero",
                ["TwoFactor:Issuer"] = "   ",
            }
        );

        var options = provider.GetRequiredService<TwoFactorOptions>();
        options.ChallengeLifetime.Should().Be(TwoFactorOptions.DefaultChallengeLifetime);
        options.ResendCooldown.Should().Be(TwoFactorOptions.DefaultResendCooldown);
        options.SetupLifetime.Should().Be(TwoFactorOptions.DefaultSetupLifetime);
        options.MaxFailedAttempts.Should().Be(TwoFactorOptions.DefaultMaxFailedAttempts);
        options.LockoutDuration.Should().Be(TwoFactorOptions.DefaultLockoutDuration);
        options.Issuer.Should().Be(TwoFactorOptions.DefaultIssuer);
    }

    [Fact]
    public void AddCodigoActivoRegistersTheTotpAndSecretProtectionPorts()
    {
        var provider = Build(new Dictionary<string, string?>(StringComparer.Ordinal));

        provider.GetRequiredService<ITotpService>().Should().BeOfType<TotpService>();
        var protector = provider.GetRequiredService<ISecretProtector>();
        protector.Should().BeOfType<DataProtectionSecretProtector>();
        protector.Unprotect(protector.Protect("secret")).Should().Be("secret");
    }
}
