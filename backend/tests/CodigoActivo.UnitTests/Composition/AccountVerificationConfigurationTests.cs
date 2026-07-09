using AwesomeAssertions;
using CodigoActivo.Composition;
using CodigoActivo.Domain.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodigoActivo.UnitTests.Composition;

public sealed class AccountVerificationConfigurationTests
{
    private static IServiceProvider Build(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new ServiceCollection().AddCodigoActivo(configuration).BuildServiceProvider();
    }

    [Fact]
    public void Binds_account_verification_options_from_configuration()
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
    public void Defaults_account_verification_options_when_values_missing_or_invalid()
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
    public void Required_defaults_to_true_when_the_flag_is_absent()
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
    public void Throws_when_verification_required_but_smtp_is_unconfigured(
        string? host,
        string? from
    )
    {
        var settings = new Dictionary<string, string?>
        {
            ["ACCOUNT_VERIFICATION_REQUIRED"] = "true",
        };
        if (host is not null)
            settings["SMTP_HOST"] = host;
        if (from is not null)
            settings["SMTP_FROM_ADDRESS"] = from;

        var act = () => Build(settings);

        act.Should().Throw<InvalidOperationException>().WithMessage("*SMTP is not configured*");
    }

    [Fact]
    public void Does_not_require_smtp_when_verification_is_disabled()
    {
        var act = () =>
            Build(new Dictionary<string, string?> { ["ACCOUNT_VERIFICATION_REQUIRED"] = "false" });

        act.Should().NotThrow();
    }
}
