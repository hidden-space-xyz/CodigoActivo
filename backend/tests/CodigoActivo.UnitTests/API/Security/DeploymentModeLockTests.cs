using AwesomeAssertions;
using CodigoActivo.API.Security;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CodigoActivo.UnitTests.API.Security;

public sealed class DeploymentModeLockTests : IDisposable
{
    private readonly string directory = Path.Join(
        Path.GetTempPath(),
        "codigoactivo-mode-tests",
        Guid.NewGuid().ToString("N")
    );

    [Theory]
    [InlineData(true, "demo")]
    [InlineData(false, "normal")]
    public void LockFirstStartPersistsConfiguredMode(bool demoMode, string expected)
    {
        var path = Path.Join(directory, "deployment-mode");

        var result = Build(path).Lock(InContainer(demoMode.ToString()));

        result.Should().Be(demoMode);
        File.ReadAllText(path).Trim().Should().Be(expected);
    }

    [Fact]
    public void LockSameModeOnRestartKeepsSelection()
    {
        var path = Path.Join(directory, "deployment-mode");
        var configuration = InContainer("true");
        var modeLock = Build(path);
        modeLock.Lock(configuration);

        var result = modeLock.Lock(configuration);

        result.Should().BeTrue();
    }

    [Fact]
    public void LockDifferentModeOnRestartFails()
    {
        var path = Path.Join(directory, "deployment-mode");
        var modeLock = Build(path);
        modeLock.Lock(InContainer("true"));

        var act = () => modeLock.Lock(InContainer("false"));

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*initialized in demo mode*cannot start in normal mode*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("yes")]
    public void LockInvalidConfiguredModeFails(string? value)
    {
        var path = Path.Join(directory, "deployment-mode");

        var act = () => Build(path).Lock(InContainer(value));

        act.Should().Throw<InvalidOperationException>().WithMessage("*DEMO_MODE*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("false")]
    [InlineData("not-a-boolean")]
    public void LockOutsideContainerWritesNothingToTheFilesystem(string? containerFlag)
    {
        var path = Path.Join(directory, "deployment-mode");
        var logger = new RecordingLogger<DeploymentModeLock>();

        var result = new DeploymentModeLock(path, logger).Lock(
            BuildConfiguration("true", containerFlag)
        );

        result.Should().BeTrue();
        File.Exists(path).Should().BeFalse();
        Directory.Exists(directory).Should().BeFalse();
        logger
            .LevelEntries.Should()
            .ContainSingle(entry =>
                entry.Level == LogLevel.Information && entry.Message.Contains("skipped")
            );
    }

    [Fact]
    public void LockOutsideContainerStillRejectsAnInvalidConfiguredMode()
    {
        var path = Path.Join(directory, "deployment-mode");

        var act = () => Build(path).Lock(BuildConfiguration("maybe", containerFlag: null));

        act.Should().Throw<InvalidOperationException>().WithMessage("*DEMO_MODE*");
    }

    [Fact]
    public void LockOutsideContainerIgnoresAConflictingPersistedMode()
    {
        var path = Path.Join(directory, "deployment-mode");
        Build(path).Lock(InContainer("true"));

        var result = Build(path).Lock(BuildConfiguration("false", containerFlag: null));

        result.Should().BeFalse();
        File.ReadAllText(path).Trim().Should().Be("demo");
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static DeploymentModeLock Build(string path)
    {
        return new DeploymentModeLock(path, NullLogger<DeploymentModeLock>.Instance);
    }

    private static IConfiguration InContainer(string? demoMode)
    {
        return BuildConfiguration(demoMode, "true");
    }

    private static IConfiguration BuildConfiguration(string? demoMode, string? containerFlag)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["DEMO_MODE"] = demoMode,
                    ["DOTNET_RUNNING_IN_CONTAINER"] = containerFlag,
                }
            )
            .Build();
    }
}
