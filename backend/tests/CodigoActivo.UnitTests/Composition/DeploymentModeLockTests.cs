using AwesomeAssertions;
using CodigoActivo.Composition.Platform;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Composition;

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

        LockInContainer(Build(path), demoMode);

        File.ReadAllText(path).Trim().Should().Be(expected);
    }

    [Fact]
    public void LockSameModeOnRestartKeepsSelection()
    {
        var path = Path.Join(directory, "deployment-mode");
        var modeLock = Build(path);
        LockInContainer(modeLock, true);

        var act = () => LockInContainer(modeLock, true);

        act.Should().NotThrow();
        File.ReadAllText(path).Trim().Should().Be("demo");
    }

    [Fact]
    public void LockDifferentModeOnRestartFails()
    {
        var path = Path.Join(directory, "deployment-mode");
        var modeLock = Build(path);
        LockInContainer(modeLock, true);

        var act = () => LockInContainer(modeLock, false);

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*initialized in demo mode*cannot start in normal mode*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("false")]
    [InlineData("not-a-boolean")]
    public void LockOnADevelopmentHostWritesNothingToTheFilesystem(string? containerFlag)
    {
        var path = Path.Join(directory, "deployment-mode");

        Build(path).Lock(true, BuildConfiguration(containerFlag), Environment("Development"));

        File.Exists(path).Should().BeFalse();
        Directory.Exists(directory).Should().BeFalse();
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void LockOutsideAContainerStillPersistsWhenTheEnvironmentIsNotDevelopment(
        string environmentName
    )
    {
        var path = Path.Join(directory, "deployment-mode");

        Build(path)
            .Lock(true, BuildConfiguration(containerFlag: null), Environment(environmentName));

        File.ReadAllText(path).Trim().Should().Be("demo");
    }

    [Theory]
    [InlineData("true")]
    [InlineData("TRUE")]
    [InlineData("1")]
    public void LockInsideAContainerPersistsEvenInDevelopment(string containerFlag)
    {
        var path = Path.Join(directory, "deployment-mode");

        Build(path).Lock(false, BuildConfiguration(containerFlag), Environment("Development"));

        File.ReadAllText(path).Trim().Should().Be("normal");
    }

    [Fact]
    public void LockOnADevelopmentHostIgnoresAConflictingPersistedMode()
    {
        var path = Path.Join(directory, "deployment-mode");
        LockInContainer(Build(path), true);

        var act = () =>
            Build(path)
                .Lock(false, BuildConfiguration(containerFlag: null), Environment("Development"));

        act.Should().NotThrow();
        File.ReadAllText(path).Trim().Should().Be("demo");
    }

    [Fact]
    public void CheckFirstStartWritesNothing()
    {
        var path = Path.Join(directory, "deployment-mode");

        var act = () => CheckInContainer(Build(path), true);

        act.Should().NotThrow();
        Directory.Exists(directory).Should().BeFalse();
    }

    [Fact]
    public void CheckSamePersistedModeIsAccepted()
    {
        var path = Path.Join(directory, "deployment-mode");
        var modeLock = Build(path);
        LockInContainer(modeLock, false);

        var act = () => CheckInContainer(modeLock, false);

        act.Should().NotThrow();
    }

    [Fact]
    public void CheckDifferentPersistedModeFails()
    {
        var path = Path.Join(directory, "deployment-mode");
        var modeLock = Build(path);
        LockInContainer(modeLock, true);

        var act = () => CheckInContainer(modeLock, false);

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*initialized in demo mode*cannot start in normal mode*");
    }

    [Fact]
    public void CheckUnreadablePersistedModeFails()
    {
        var path = Path.Join(directory, "deployment-mode");
        Directory.CreateDirectory(directory);
        File.WriteAllText(path, "maybe");

        var act = () => CheckInContainer(Build(path), true);

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*deployment mode file is invalid*");
    }

    [Fact]
    public void CheckOnADevelopmentHostIgnoresAConflictingPersistedMode()
    {
        var path = Path.Join(directory, "deployment-mode");
        LockInContainer(Build(path), true);

        var act = () =>
            Build(path)
                .Check(false, BuildConfiguration(containerFlag: null), Environment("Development"));

        act.Should().NotThrow();
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
        return new DeploymentModeLock(path);
    }

    private static void LockInContainer(DeploymentModeLock modeLock, bool demoMode)
    {
        modeLock.Lock(demoMode, BuildConfiguration("true"), Environment("Production"));
    }

    private static void CheckInContainer(DeploymentModeLock modeLock, bool demoMode)
    {
        modeLock.Check(demoMode, BuildConfiguration("true"), Environment("Production"));
    }

    private static IHostEnvironment Environment(string environmentName)
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);
        return environment;
    }

    private static IConfiguration BuildConfiguration(string? containerFlag)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { ["DOTNET_RUNNING_IN_CONTAINER"] = containerFlag }
            )
            .Build();
    }
}
