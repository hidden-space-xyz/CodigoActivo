using AwesomeAssertions;
using CodigoActivo.Application.Accounts;
using CodigoActivo.Composition;
using CodigoActivo.Infrastructure.Communication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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
                ["AccountVerification:OtpLifetimeMinutes"] = "10",
                ["AccountVerification:ResendCooldownSeconds"] = "30",
                ["SMTP_HOST"] = "smtp.example.test",
                ["SMTP_FROM_ADDRESS"] = "no-reply@example.test",
            }
        );

        var options = provider.GetRequiredService<AccountVerificationOptions>();
        options.OtpLifetime.Should().Be(TimeSpan.FromMinutes(10));
        options.ResendCooldown.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void AddCodigoActivoMissingValuesDefaultsAccountVerificationOptions()
    {
        var provider = Build(
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["SMTP_HOST"] = "smtp.example.test",
                ["SMTP_FROM_ADDRESS"] = "no-reply@example.test",
            }
        );

        var options = provider.GetRequiredService<AccountVerificationOptions>();
        options.OtpLifetime.Should().Be(AccountVerificationOptions.DefaultOtpLifetime);
        options.ResendCooldown.Should().Be(AccountVerificationOptions.DefaultResendCooldown);
    }

    [Theory]
    [InlineData("AccountVerification:OtpLifetimeMinutes", "Infinity")]
    [InlineData("AccountVerification:ResendCooldownSeconds", "not-a-number")]
    public void AddCodigoActivoUnusableValueStopsTheStart(string key, string value)
    {
        var provider = Build(
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                [key] = value,
                ["SMTP_HOST"] = "smtp.example.test",
                ["SMTP_FROM_ADDRESS"] = "no-reply@example.test",
            }
        );

        var act = () => provider.GetRequiredService<AccountVerificationOptions>();

        act.Should().Throw<InvalidOperationException>().WithMessage($"*{key}*");
    }

    [Theory]
    [InlineData(null, "no-reply@example.test")]
    [InlineData("smtp.example.test", null)]
    [InlineData(null, null)]
    public void AddCodigoActivoSmtpUnconfiguredStopsTheStart(string? host, string? from)
    {
        var provider = Build(
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["SMTP_HOST"] = host,
                ["SMTP_FROM_ADDRESS"] = from,
            }
        );

        var act = () => provider.GetRequiredService<SmtpOptions>();

        act.Should()
            .Throw<OptionsValidationException>()
            .WithMessage("*SMTP is not configured*Login codes are delivered by email*");
    }
}
