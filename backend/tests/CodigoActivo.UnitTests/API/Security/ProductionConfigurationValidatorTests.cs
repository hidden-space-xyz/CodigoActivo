using AwesomeAssertions;
using CodigoActivo.API.Security;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CodigoActivo.UnitTests.API.Security;

public sealed class ProductionConfigurationValidatorTests
{
    [Fact]
    public void ValidateSafeConfigurationDoesNotThrow()
    {
        var act = () => ProductionConfigurationValidator.Validate(BuildConfiguration());

        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateDemoModeInProductionDoesNotThrow()
    {
        var config = BuildConfiguration(
            new KeyValuePair<string, string?>("DEMO_MODE", "true")
        );

        var act = () => ProductionConfigurationValidator.Validate(config);

        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateDisabledAccountVerificationInProductionDoesNotThrow()
    {
        var config = BuildConfiguration(
            new KeyValuePair<string, string?>("ACCOUNT_VERIFICATION_REQUIRED", "false")
        );

        var act = () => ProductionConfigurationValidator.Validate(config);

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("POSTGRES_PASSWORD", "short", "POSTGRES_PASSWORD")]
    [InlineData(
        "DATA_PROTECTION_CERTIFICATE_PASSWORD",
        "short",
        "DATA_PROTECTION_CERTIFICATE_PASSWORD"
    )]
    [InlineData("APP_BASE_URL", "http://app.test", "APP_BASE_URL")]
    [InlineData("APP_BASE_URL", "https://example.org", "APP_BASE_URL")]
    [InlineData("APP_BASE_URL", "https://app.test", "APP_BASE_URL")]
    [InlineData("APP_BASE_URL", "https://192.168.1.20", "APP_BASE_URL")]
    [InlineData("SMTP_SECURITY", "None", "SMTP_SECURITY")]
    [InlineData("SMTP_HOST", "bad host", "SMTP_HOST")]
    [InlineData("SMTP_PORT", "70000", "SMTP_PORT")]
    [InlineData("SMTP_FROM_ADDRESS", "Sender <sender@app.test>", "SMTP_FROM_ADDRESS")]
    [InlineData("SMTP_USERNAME", "mailer", "SMTP_USERNAME")]
    public void ValidateUnsafeConfigurationThrows(
        string key,
        string value,
        string expectedMessage
    )
    {
        var config = BuildConfiguration(new KeyValuePair<string, string?>(key, value));

        var act = () => ProductionConfigurationValidator.Validate(config);

        act.Should().Throw<InvalidOperationException>().WithMessage($"*{expectedMessage}*");
    }

    private static IConfiguration BuildConfiguration(
        KeyValuePair<string, string?>? replacement = null
    )
    {
        var values = new Dictionary<string, string?>
        {
            ["DEMO_MODE"] = "false",
            ["ACCOUNT_VERIFICATION_REQUIRED"] = "true",
            ["POSTGRES_PASSWORD"] = "a-strong-32-character-db-password",
            ["DATA_PROTECTION_CERTIFICATE_PASSWORD"] =
                "a-separate-strong-data-protection-password",
            ["APP_BASE_URL"] = "https://codigoactivo.es",
            ["SMTP_SECURITY"] = "StartTls",
            ["SMTP_HOST"] = "smtp.app.test",
            ["SMTP_PORT"] = "587",
            ["SMTP_FROM_ADDRESS"] = "sender@app.test",
            ["SMTP_USERNAME"] = string.Empty,
            ["SMTP_PASSWORD"] = string.Empty,
        };
        if (replacement is { } item)
        {
            values[item.Key] = item.Value;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
