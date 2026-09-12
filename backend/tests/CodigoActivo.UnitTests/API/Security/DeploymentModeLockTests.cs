using AwesomeAssertions;
using CodigoActivo.API.Security;
using Microsoft.Extensions.Configuration;
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

        var result = new DeploymentModeLock(path).Lock(BuildConfiguration(demoMode.ToString()));

        result.Should().Be(demoMode);
        File.ReadAllText(path).Trim().Should().Be(expected);
    }

    [Fact]
    public void LockSameModeOnRestartKeepsSelection()
    {
        var path = Path.Join(directory, "deployment-mode");
        var configuration = BuildConfiguration("true");
        var modeLock = new DeploymentModeLock(path);
        modeLock.Lock(configuration);

        var result = modeLock.Lock(configuration);

        result.Should().BeTrue();
    }

    [Fact]
    public void LockDifferentModeOnRestartFails()
    {
        var path = Path.Join(directory, "deployment-mode");
        var modeLock = new DeploymentModeLock(path);
        modeLock.Lock(BuildConfiguration("true"));

        var act = () => modeLock.Lock(BuildConfiguration("false"));

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

        var act = () => new DeploymentModeLock(path).Lock(BuildConfiguration(value));

        act.Should().Throw<InvalidOperationException>().WithMessage("*DEMO_MODE*");
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static IConfiguration BuildConfiguration(string? demoMode)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["DEMO_MODE"] = demoMode,
                }
            )
            .Build();
    }
}
