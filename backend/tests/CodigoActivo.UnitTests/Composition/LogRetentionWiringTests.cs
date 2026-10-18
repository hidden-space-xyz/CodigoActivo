using AwesomeAssertions;
using CodigoActivo.Composition;
using CodigoActivo.Infrastructure.Logs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodigoActivo.UnitTests.Composition;

public sealed class LogRetentionWiringTests
{
    private const string DiagnosticsKey = "DIAGNOSTIC_LOG_RETENTION_DAYS";
    private const string AuditKey = "AUDIT_LOG_RETENTION_DAYS";

    private static LogRetentionOptions Options(string? diagnostics, string? audit)
    {
        var settings = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["SMTP_HOST"] = "smtp.example.test",
            ["SMTP_FROM_ADDRESS"] = "no-reply@example.test",
            [DiagnosticsKey] = diagnostics,
            [AuditKey] = audit,
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        using var provider = new ServiceCollection()
            .AddLogging()
            .AddCodigoActivo(configuration)
            .BuildServiceProvider();
        return provider.GetRequiredService<LogRetentionOptions>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void MissingRetentionsTakeTheDefaults(string? value)
    {
        var options = Options(value, value);

        options.Diagnostics.Should().Be(LogRetentionOptions.DefaultDiagnostics);
        options.Audit.Should().Be(LogRetentionOptions.DefaultAudit);
    }

    [Fact]
    public void ValidRetentionsOverrideTheDefaults()
    {
        var options = Options("14", "365");

        options.Diagnostics.Should().Be(TimeSpan.FromDays(14));
        options.Audit.Should().Be(TimeSpan.FromDays(365));
    }

    [Theory]
    [InlineData("0", null, DiagnosticsKey)]
    [InlineData("not-a-number", null, DiagnosticsKey)]
    [InlineData(null, "-5", AuditKey)]
    public void UnusableRetentionStopsTheStart(string? diagnostics, string? audit, string key)
    {
        var act = () => Options(diagnostics, audit);

        act.Should().Throw<InvalidOperationException>().WithMessage($"*{key}*");
    }

    [Theory]
    [InlineData("0.5", null)]
    [InlineData(null, "731")]
    public void RetentionOutsideTheLimitsStopsTheStart(string? diagnostics, string? audit)
    {
        var act = () => Options(diagnostics, audit);

        act.Should()
            .Throw<OptionsValidationException>()
            .WithMessage($"*{DiagnosticsKey}*{AuditKey}*between 1 and 730 days*");
    }
}
