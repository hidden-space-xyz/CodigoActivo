using AwesomeAssertions;
using CodigoActivo.Application.Options;
using CodigoActivo.Composition;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodigoActivo.UnitTests.Composition;

public sealed class AccountVerificationConfigurationTests : IDisposable
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
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var provider = new ServiceCollection()
            .AddCodigoActivo(configuration)
            .BuildServiceProvider();
        providers.Add(provider);
        return provider;
    }

    [Fact]
    public void AddCodigoActivoValidAccountVerificationSettingsBindsOptionsFromConfiguration()
    {
        var provider = Build(
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["ACCOUNT_VERIFICATION_REQUIRED"] = "true",
                ["AccountVerification:OtpLifetimeMinutes"] = "10",
                ["AccountVerification:ResendCooldownSeconds"] = "30",
                ["SMTP_HOST"] = "smtp.example.test",
                ["SMTP_FROM_ADDRESS"] = "no-reply@example.test",
            }
        );

        var options = provider.GetRequiredService<AccountVerificationOptions>();
        options.Required.Should().BeTrue();
        options.OtpLifetime.Should().Be(TimeSpan.FromMinutes(10));
        options.ResendCooldown.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void AddCodigoActivoMissingOrInvalidValuesDefaultsAccountVerificationOptions()
    {
        var provider = Build(
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["ACCOUNT_VERIFICATION_REQUIRED"] = "false",
                ["AccountVerification:OtpLifetimeMinutes"] = "Infinity",
                ["AccountVerification:ResendCooldownSeconds"] = "not-a-number",
            }
        );

        var options = provider.GetRequiredService<AccountVerificationOptions>();
        options.Required.Should().BeFalse();
        options.OtpLifetime.Should().Be(AccountVerificationOptions.DefaultOtpLifetime);
        options.ResendCooldown.Should().Be(AccountVerificationOptions.DefaultResendCooldown);
    }

    [Fact]
    public void AddCodigoActivoRequiredFlagAbsentDefaultsRequiredToTrue()
    {
        var provider = Build(
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["SMTP_HOST"] = "smtp.example.test",
                ["SMTP_FROM_ADDRESS"] = "no-reply@example.test",
            }
        );

        provider.GetRequiredService<AccountVerificationOptions>().Required.Should().BeTrue();
    }

    [Theory]
    [InlineData(null, "no-reply@example.test")]
    [InlineData("smtp.example.test", null)]
    public void AddCodigoActivoVerificationRequiredButSmtpUnconfiguredThrows(
        string? host,
        string? from
    )
    {
        var settings = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["ACCOUNT_VERIFICATION_REQUIRED"] = "true",
            ["SMTP_HOST"] = host,
            ["SMTP_FROM_ADDRESS"] = from,
        };

        var act = () => Build(settings);

        act.Should().Throw<InvalidOperationException>().WithMessage("*SMTP is not configured*");
    }

    [Fact]
    public void AddCodigoActivoVerificationDisabledDoesNotRequireSmtp()
    {
        var act = () =>
            Build(
                new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["ACCOUNT_VERIFICATION_REQUIRED"] = "false",
                }
            );

        act.Should().NotThrow();
    }
}
