using AwesomeAssertions;
using CodigoActivo.Composition.Platform;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace CodigoActivo.IntegrationTests.Infrastructure;

public sealed class StartupOrderTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    [Fact]
    public async Task StartInvalidBootstrapEmailOnEmptyDatabaseLeavesTheDeploymentModeUnlocked()
    {
        await Factory.EmptyDatabaseAsync();
        var stateRoot = Path.Join(
            Path.GetTempPath(),
            "codigoactivo-tests",
            Guid.NewGuid().ToString("N")
        );
        var modeFile = Path.Join(stateRoot, "deployment-mode");
        await using var host = Factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("BOOTSTRAP_ADMIN_EMAIL", "not-an-email");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DeploymentModeLock>();
                services.AddSingleton(new DeploymentModeLock(modeFile));
            });
        });

        try
        {
            var act = () => host.CreateClient();

            act.Should().Throw<InvalidOperationException>().WithMessage("*BOOTSTRAP_ADMIN_EMAIL*");
            File.Exists(modeFile).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(stateRoot))
            {
                Directory.Delete(stateRoot, recursive: true);
            }
        }
    }
}
