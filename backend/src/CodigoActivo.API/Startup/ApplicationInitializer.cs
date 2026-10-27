using CodigoActivo.Composition;

namespace CodigoActivo.API.Startup;

internal static class ApplicationInitializer
{
    internal static async Task InitializeAsync(
        this WebApplication app,
        CancellationToken cancellationToken
    )
    {
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        services.ValidateConfiguration();
        services.VerifyKeyRing();
        services.CheckDeploymentMode();

        await services.MigrateDatabaseAsync(cancellationToken);
        await services.MaintainLogTablesAsync(cancellationToken);
        await services.SeedCatalogsAsync(cancellationToken);
        await services.SeedInitialAdministratorAsync(cancellationToken);
        var demoMode = services.LockDeploymentMode();
        await SyncDemoDataAsync(services, demoMode);
    }

    private static async Task SyncDemoDataAsync(IServiceProvider services, bool demoMode)
    {
        if (!demoMode)
        {
            return;
        }

        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        await services.SeedDemoDataAsync(cancellation.Token);
        await services.SeedDemoDataHistoryAsync(cancellation.Token);
        await services.SeedDemoLogsAsync(cancellation.Token);
    }
}
