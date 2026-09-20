using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

namespace CodigoActivo.UnitTests.Architecture;

public sealed partial class LoggingCallSiteTests
{
    [GeneratedRegex(
        @"\.Log(Trace|Debug|Information|Warning|Error|Critical)\s*\(|\.Log\s*\(\s*(LogLevel|level)"
    )]
    private static partial Regex DirectLoggerCall();

    private static DirectoryInfo ProductionSources()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (
            current is not null && !File.Exists(Path.Combine(current.FullName, "CodigoActivo.slnx"))
        )
        {
            current = current.Parent;
        }

        current.Should().NotBeNull();
        var sources = new DirectoryInfo(Path.Combine(current!.FullName, "src"));
        sources.Exists.Should().BeTrue();
        return sources;
    }

    private static IEnumerable<FileInfo> SourceFiles(DirectoryInfo sources)
    {
        return sources
            .EnumerateFiles("*.cs", SearchOption.AllDirectories)
            .Where(file =>
                !file.FullName.Contains(
                    $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal
                )
                && !file.FullName.Contains(
                    $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal
                )
            );
    }

    [Fact]
    public void ProductionCodeNeverCallsTheLoggerExtensionsDirectly()
    {
        var sources = ProductionSources();

        var offenders = SourceFiles(sources)
            .Where(file => DirectLoggerCall().IsMatch(File.ReadAllText(file.FullName)))
            .Select(file => Path.GetRelativePath(sources.FullName, file.FullName))
            .ToList();

        offenders.Should().BeEmpty();
    }

    [Fact]
    public void ProductionSourcesAreActuallyScanned()
    {
        SourceFiles(ProductionSources()).Should().HaveCountGreaterThan(200);
    }

    [Fact]
    public void TheAnalyzerThatForbidsDirectLoggerCallsIsAnError()
    {
        var sources = ProductionSources();
        var editorConfig = Path.Combine(sources.Parent!.FullName, ".editorconfig");

        File.ReadAllText(editorConfig)
            .Should()
            .Contain("dotnet_diagnostic.CA1848.severity = error");
    }
}
