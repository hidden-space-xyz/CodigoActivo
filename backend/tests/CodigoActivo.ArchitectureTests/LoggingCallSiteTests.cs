using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

namespace CodigoActivo.ArchitectureTests;

public sealed partial class LoggingCallSiteTests
{
    [GeneratedRegex(
        @"\.Log(Trace|Debug|Information|Warning|Error|Critical)\s*\(|\.Log\s*\(\s*(LogLevel|level)"
    )]
    private static partial Regex DirectLoggerCall();

    [Fact]
    public void ProductionCodeNeverCallsTheLoggerExtensionsDirectly()
    {
        var sources = BackendSources.Production;

        var offenders = BackendSources
            .CSharpFiles(sources)
            .Where(file => DirectLoggerCall().IsMatch(File.ReadAllText(file.FullName)))
            .Select(file => Path.GetRelativePath(sources.FullName, file.FullName))
            .ToList();

        offenders.Should().BeEmpty();
    }

    [Fact]
    public void ProductionSourcesAreActuallyScanned()
    {
        BackendSources.CSharpFiles(BackendSources.Production).Should().HaveCountGreaterThan(200);
    }

    [Fact]
    public void TheAnalyzerThatForbidsDirectLoggerCallsIsAnError()
    {
        var editorConfig = Path.Join(BackendSources.Root.FullName, ".editorconfig");

        File.ReadAllText(editorConfig)
            .Should()
            .Contain("dotnet_diagnostic.CA1848.severity = error");
    }
}
