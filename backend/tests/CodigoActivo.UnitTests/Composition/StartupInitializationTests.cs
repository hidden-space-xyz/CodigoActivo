using AwesomeAssertions;
using CodigoActivo.Composition;
using CodigoActivo.Composition.Accounts;
using CodigoActivo.Infrastructure.Communication;
using CodigoActivo.Infrastructure.Security;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Composition;

public sealed class StartupInitializationTests
{
    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("smtp")]
    public void ValidateConfigurationSmtpPortBelowOneStopsTheStart(string port)
    {
        using var provider = BuildProvider(("SMTP_PORT", port));

        var act = () => provider.ValidateConfiguration();

        act.Should().Throw<InvalidOperationException>().WithMessage("*SMTP_PORT*");
    }

    [Fact]
    public void ValidateConfigurationSmtpPortAboveTheHighestStopsTheStart()
    {
        using var provider = BuildProvider(("SMTP_PORT", "65536"));

        var act = () => provider.ValidateConfiguration();

        act.Should().Throw<OptionsValidationException>().WithMessage("*SMTP_PORT*");
    }

    [Fact]
    public void ValidateConfigurationHighestSmtpPortIsAccepted()
    {
        using var provider = BuildProvider(("SMTP_PORT", "65535"));

        var act = () => provider.ValidateConfiguration();

        act.Should().NotThrow();
        provider.GetRequiredService<SmtpOptions>().Port.Should().Be(65_535);
    }

    [Fact]
    public void VerifyKeyRingMissingDataProtectionKeyStopsTheStart()
    {
        using var provider = BuildProvider(("DATA_PROTECTION_KEY", null));

        var act = () => provider.VerifyKeyRing();

        act.Should().Throw<InvalidOperationException>().WithMessage("*DATA_PROTECTION_KEY*");
    }

    [Fact]
    public void VerifyKeyRingUsableKeyAndNoKeyRingIsAccepted()
    {
        using var provider = BuildProvider(
            ("DATA_PROTECTION_KEY", DataProtectionRootKeys.RandomBase64())
        );

        var act = () => provider.VerifyKeyRing();

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void LockDeploymentModeDevelopmentOutsideAContainerReturnsTheConfiguredMode(
        string demoMode,
        bool expected
    )
    {
        using var provider = BuildProvider(("DEMO_MODE", demoMode));

        provider.LockDeploymentMode().Should().Be(expected);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("  admin@app.test  ", "admin@app.test")]
    public void AddCodigoActivoInitialAdministratorEmailComesFromTheBootstrapSetting(
        string? value,
        string expected
    )
    {
        using var provider = BuildProvider(("BOOTSTRAP_ADMIN_EMAIL", value));

        provider.GetRequiredService<InitialAdministratorOptions>().Email.Should().Be(expected);
    }

    private static ServiceProvider BuildProvider((string Key, string? Value) setting)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["SMTP_HOST"] = "smtp.app.test",
                    ["SMTP_FROM_ADDRESS"] = "sender@app.test",
                    [setting.Key] = setting.Value,
                }
            )
            .Build();
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Development);

        return new ServiceCollection()
            .AddLogging()
            .AddSingleton<IConfiguration>(configuration)
            .AddSingleton(environment)
            .AddCodigoActivo(configuration)
            .AddSingleton(
                new KeyRingOptions
                {
                    DirectoryPath = Path.Join(
                        Path.GetTempPath(),
                        "codigoactivo-keyring-tests",
                        Guid.NewGuid().ToString("N")
                    ),
                }
            )
            .BuildServiceProvider();
    }
}
