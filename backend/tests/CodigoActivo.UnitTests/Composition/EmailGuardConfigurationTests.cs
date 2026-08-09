using AwesomeAssertions;
using CodigoActivo.Composition;
using CodigoActivo.Infrastructure.Communication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodigoActivo.UnitTests.Composition;

public sealed class EmailGuardConfigurationTests : IDisposable
{
    private readonly List<ServiceProvider> providers = [];

    public void Dispose()
    {
        foreach (var provider in providers)
        {
            provider.Dispose();
        }
    }

    private EmailGuardOptions Build(Dictionary<string, string?> settings)
    {
        settings["ACCOUNT_VERIFICATION_REQUIRED"] = "false";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var provider = new ServiceCollection()
            .AddCodigoActivo(configuration)
            .BuildServiceProvider();
        providers.Add(provider);
        return provider.GetRequiredService<EmailGuardOptions>();
    }

    [Fact]
    public void AddCodigoActivoValidEmailGuardSettingsBindsOptionsFromConfiguration()
    {
        var options = Build(
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["EmailGuard:RecipientBurst"] = "7",
                ["EmailGuard:RecipientPerHour"] = "4",
                ["EmailGuard:RecipientPerDay"] = "11",
                ["EmailGuard:GlobalBurst"] = "300",
                ["EmailGuard:GlobalPerHour"] = "250",
                ["EmailGuard:GlobalCredentialReserve"] = "60",
                ["EmailGuard:MaxTrackedRecipients"] = "900",
                ["EmailGuard:SweepIntervalMinutes"] = "2",
                ["EmailGuard:AlertIntervalMinutes"] = "30",
            }
        );

        options.RecipientBurst.Should().Be(7);
        options.RecipientPerHour.Should().Be(4);
        options.RecipientPerDay.Should().Be(11);
        options.GlobalBurst.Should().Be(300);
        options.GlobalPerHour.Should().Be(250);
        options.GlobalCredentialReserve.Should().Be(60);
        options.MaxTrackedRecipients.Should().Be(900);
        options.SweepInterval.Should().Be(TimeSpan.FromMinutes(2));
        options.AlertInterval.Should().Be(TimeSpan.FromMinutes(30));
    }

    [Fact]
    public void AddCodigoActivoMissingOrInvalidValuesFallsBackToTheShippedLimits()
    {
        var options = Build(
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["EmailGuard:RecipientBurst"] = "0",
                ["EmailGuard:RecipientPerDay"] = "not-a-number",
                ["EmailGuard:GlobalPerHour"] = "-5",
                ["EmailGuard:SweepIntervalMinutes"] = "Infinity",
            }
        );

        options.RecipientBurst.Should().Be(EmailGuardOptions.DefaultRecipientBurst);
        options.RecipientPerDay.Should().Be(EmailGuardOptions.DefaultRecipientPerDay);
        options.GlobalPerHour.Should().Be(EmailGuardOptions.DefaultGlobalPerHour);
        options.SweepInterval.Should().Be(EmailGuardOptions.DefaultSweepInterval);
    }

    [Fact]
    public void AddCodigoActivoReserveNotBelowTheGlobalBurstClampsSoAccountEmailCanStillSend()
    {
        var options = Build(
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["EmailGuard:GlobalBurst"] = "50",
                ["EmailGuard:GlobalCredentialReserve"] = "80",
            }
        );

        options.EffectiveCredentialReserve.Should().Be(49);
    }
}
