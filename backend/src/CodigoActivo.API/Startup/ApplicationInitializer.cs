using CodigoActivo.API.Diagnostics;
using CodigoActivo.API.Security;
using CodigoActivo.Composition;

namespace CodigoActivo.API.Startup;

internal static class ApplicationInitializer
{
    internal static Task InitializeAsync(
        this WebApplication app,
        CancellationToken cancellationToken
    )
    {
        return InitializeDatabaseAsync(app, cancellationToken);
    }

    private static async Task InitializeDatabaseAsync(
        WebApplication app,
        CancellationToken cancellationToken
    )
    {
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var logger = services
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(LogCategories.Lifecycle);
        var demoMode = services
            .GetRequiredService<DeploymentModeLock>()
            .Lock(app.Configuration, app.Environment);

        await MigrateDatabaseAsync(services, logger, cancellationToken);
        await SeedDatabaseAsync(services, logger, cancellationToken);
        await services.SeedInitialAdministratorAsync(
            app.Configuration["BOOTSTRAP_ADMIN_EMAIL"],
            app.Configuration["BOOTSTRAP_ADMIN_PASSWORD"],
            cancellationToken
        );
        await SyncDemoDataAsync(services, demoMode);
    }

    private static async Task MigrateDatabaseAsync(
        IServiceProvider services,
        ILogger logger,
        CancellationToken cancellationToken
    )
    {
        await services.MigrateDatabaseAsync(cancellationToken);
        logger.DatabaseMigrationsApplied();
    }

    private static async Task SeedDatabaseAsync(
        IServiceProvider services,
        ILogger logger,
        CancellationToken cancellationToken
    )
    {
        await services.SeedCatalogsAsync(cancellationToken);
        logger.DatabaseSeedApplied();
    }

    private static async Task SyncDemoDataAsync(IServiceProvider services, bool demoMode)
    {
        if (!demoMode)
        {
            return;
        }

        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        await services.SeedDemoDataAsync(cancellation.Token);
    }
}
