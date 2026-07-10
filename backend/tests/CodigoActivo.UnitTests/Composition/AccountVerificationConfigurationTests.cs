using AwesomeAssertions;
using CodigoActivo.Composition;
using CodigoActivo.Domain.Security;
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
            provider.Dispose();
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
    public void AddCodigoActivo_ValidAccountVerificationSettings_BindsOptionsFromConfiguration()
    {
        var provider = Build(
            new Dictionary<string, string?>
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
    public void AddCodigoActivo_MissingOrInvalidValues_DefaultsAccountVerificationOptions()
    {
        var provider = Build(
            new Dictionary<string, string?>
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
    public void AddCodigoActivo_RequiredFlagAbsent_DefaultsRequiredToTrue()
    {
        var provider = Build(
            new Dictionary<string, string?>
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
    public void AddCodigoActivo_VerificationRequiredButSmtpUnconfigured_Throws(
        string? host,
        string? from
    )
    {
        var settings = new Dictionary<string, string?>
        {
            ["ACCOUNT_VERIFICATION_REQUIRED"] = "true",
            ["SMTP_HOST"] = host,
            ["SMTP_FROM_ADDRESS"] = from,
        };

        var act = () => Build(settings);

        act.Should().Throw<InvalidOperationException>().WithMessage("*SMTP is not configured*");
    }

    [Fact]
    public void AddCodigoActivo_VerificationDisabled_DoesNotRequireSmtp()
    {
        var act = () =>
            Build(new Dictionary<string, string?> { ["ACCOUNT_VERIFICATION_REQUIRED"] = "false" });

        act.Should().NotThrow();
    }
}
