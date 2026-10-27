using AwesomeAssertions;
using CodigoActivo.Composition.Platform;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace CodigoActivo.IntegrationTests.Infrastructure;

public sealed class StartupOrderTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private readonly string stateRoot = Path.Join(
        Path.GetTempPath(),
        "codigoactivo-tests",
        Guid.NewGuid().ToString("N")
    );

    private string ModeFile => Path.Join(stateRoot, "deployment-mode");

    [Fact]
    public async Task StartInvalidBootstrapEmailOnEmptyDatabaseLeavesTheDeploymentModeUnlocked()
    {
        await Factory.EmptyDatabaseAsync();
        await using var host = HostWith("BOOTSTRAP_ADMIN_EMAIL", "not-an-email");

        var act = () => host.CreateClient();

        act.Should().Throw<InvalidOperationException>().WithMessage("*BOOTSTRAP_ADMIN_EMAIL*");
        File.Exists(ModeFile).Should().BeFalse();
    }

    [Fact]
    public async Task StartInvalidDemoModeOnEmptyDatabaseChangesNothing()
    {
        await Factory.EmptyDatabaseAsync();
        await using var host = HostWith("DEMO_MODE", "yes");

        var act = () => host.CreateClient();

        act.Should().Throw<InvalidOperationException>().WithMessage("*DEMO_MODE*");
        File.Exists(ModeFile).Should().BeFalse();
        (await Factory.QueryAsync(db => db.Users.AnyAsync(Ct))).Should().BeFalse();
    }

    [Fact]
    public async Task StartOtherLockedModeOnEmptyDatabaseChangesNothing()
    {
        await Factory.EmptyDatabaseAsync();
        Directory.CreateDirectory(stateRoot);
        await File.WriteAllTextAsync(ModeFile, "demo\n", Ct);
        await using var host = HostWith("DEMO_MODE", "false");

        var act = () => host.CreateClient();

        act.Should().Throw<InvalidOperationException>().WithMessage("*initialized in demo mode*");
        (await Factory.QueryAsync(db => db.Users.AnyAsync(Ct))).Should().BeFalse();
    }

    public override ValueTask DisposeAsync()
    {
        if (Directory.Exists(stateRoot))
        {
            Directory.Delete(stateRoot, recursive: true);
        }

        return base.DisposeAsync();
    }

    private WebApplicationFactory<Program> HostWith(string key, string value)
    {
        return Factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting(key, value);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DeploymentModeLock>();
                services.AddSingleton(new DeploymentModeLock(ModeFile));
            });
        });
    }
}
