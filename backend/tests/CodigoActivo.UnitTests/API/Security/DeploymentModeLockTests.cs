using AwesomeAssertions;
using CodigoActivo.API.Security;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
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

        var result = LockInContainer(Build(path), demoMode.ToString());

        result.Should().Be(demoMode);
        File.ReadAllText(path).Trim().Should().Be(expected);
    }

    [Fact]
    public void LockSameModeOnRestartKeepsSelection()
    {
        var path = Path.Join(directory, "deployment-mode");
        var modeLock = Build(path);
        LockInContainer(modeLock, "true");

        var result = LockInContainer(modeLock, "true");

        result.Should().BeTrue();
    }

    [Fact]
    public void LockDifferentModeOnRestartFails()
    {
        var path = Path.Join(directory, "deployment-mode");
        var modeLock = Build(path);
        LockInContainer(modeLock, "true");

        var act = () => LockInContainer(modeLock, "false");

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

        var act = () => LockInContainer(Build(path), value);

        act.Should().Throw<InvalidOperationException>().WithMessage("*DEMO_MODE*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("false")]
    [InlineData("not-a-boolean")]
    public void LockOnADevelopmentHostWritesNothingToTheFilesystem(string? containerFlag)
    {
        var path = Path.Join(directory, "deployment-mode");
        var logger = new RecordingLogger<DeploymentModeLock>();

        var result = new DeploymentModeLock(path, logger).Lock(
            BuildConfiguration("true", containerFlag),
            Environment("Development")
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

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void LockOutsideAContainerStillPersistsWhenTheEnvironmentIsNotDevelopment(
        string environmentName
    )
    {
        var path = Path.Join(directory, "deployment-mode");

        var result = Build(path)
            .Lock(BuildConfiguration("true", containerFlag: null), Environment(environmentName));

        result.Should().BeTrue();
        File.ReadAllText(path).Trim().Should().Be("demo");
    }

    [Theory]
    [InlineData("true")]
    [InlineData("TRUE")]
    [InlineData("1")]
    public void LockInsideAContainerPersistsEvenInDevelopment(string containerFlag)
    {
        var path = Path.Join(directory, "deployment-mode");

        var result = Build(path)
            .Lock(BuildConfiguration("false", containerFlag), Environment("Development"));

        result.Should().BeFalse();
        File.ReadAllText(path).Trim().Should().Be("normal");
    }

    [Fact]
    public void LockOnADevelopmentHostStillRejectsAnInvalidConfiguredMode()
    {
        var path = Path.Join(directory, "deployment-mode");

        var act = () =>
            Build(path)
                .Lock(BuildConfiguration("maybe", containerFlag: null), Environment("Development"));

        act.Should().Throw<InvalidOperationException>().WithMessage("*DEMO_MODE*");
    }

    [Fact]
    public void LockOnADevelopmentHostIgnoresAConflictingPersistedMode()
    {
        var path = Path.Join(directory, "deployment-mode");
        LockInContainer(Build(path), "true");

        var result = Build(path)
            .Lock(BuildConfiguration("false", containerFlag: null), Environment("Development"));

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

    private static bool LockInContainer(DeploymentModeLock modeLock, string? demoMode)
    {
        return modeLock.Lock(InContainer(demoMode), Environment("Production"));
    }

    private static IHostEnvironment Environment(string environmentName)
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);
        return environment;
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
