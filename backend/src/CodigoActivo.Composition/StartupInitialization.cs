using CodigoActivo.Composition.Accounts;
using CodigoActivo.Composition.Platform;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Seeders;
using CodigoActivo.Infrastructure.Logs;
using CodigoActivo.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodigoActivo.Composition;

/// <summary>
/// Runs the start-up steps from a service scope: the configuration and key ring checks, schema
/// migrations, log table maintenance, catalog seeding, the initial administrator, the deployment
/// mode lock and the optional demo data and log history.
/// </summary>
public static class StartupInitialization
{
    /// <summary>
    /// Checks every setting validated at start, so an unusable value stops the start before any
    /// other step changes state.
    /// </summary>
    /// <param name="services">Service provider.</param>
    public static void ValidateConfiguration(this IServiceProvider services)
    {
        services.GetRequiredService<IStartupValidator>().Validate();
    }

    /// <summary>
    /// Checks that <c>DATA_PROTECTION_KEY</c> is usable and decrypts every key already in the key
    /// ring, so a wrong key stops the start before Data Protection replaces the ring with new keys.
    /// </summary>
    /// <param name="services">Service provider.</param>
    public static void VerifyKeyRing(this IServiceProvider services)
    {
        services
            .GetRequiredService<AesGcmKeyRingProtection>()
            .VerifyKeyRing(services.GetRequiredService<KeyRingOptions>().DirectoryPath);
    }

    /// <summary>
    /// Checks the configured deployment mode against the one this deployment started with and
    /// keeps it for the next starts.
    /// </summary>
    /// <param name="services">Service provider.</param>
    /// <returns><see langword="true"/> when the deployment runs in demo mode.</returns>
    public static bool LockDeploymentMode(this IServiceProvider services)
    {
        return services
            .GetRequiredService<DeploymentModeLock>()
            .Lock(
                services.GetRequiredService<IConfiguration>(),
                services.GetRequiredService<IHostEnvironment>()
            );
    }

    /// <summary>
    /// Applies every pending schema migration.
    /// </summary>
    /// <param name="services">Scoped service provider.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static Task MigrateDatabaseAsync(this IServiceProvider services, CancellationToken ct)
    {
        return services.GetRequiredService<CodigoActivoDbContext>().Database.MigrateAsync(ct);
    }

    /// <summary>
    /// Prepares the partitions of the log tables and removes the expired entries. A failure is
    /// logged and does not stop the start: nothing else depends on the log tables.
    /// </summary>
    /// <param name="services">Scoped service provider.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static Task MaintainLogTablesAsync(this IServiceProvider services, CancellationToken ct)
    {
        return services
            .GetRequiredService<LogPartitionMaintainer>()
            .TryMaintainAsync(services.GetRequiredService<ILogger<LogPartitionMaintainer>>(), ct);
    }

    /// <summary>
    /// Seeds the catalogs the application relies on.
    /// </summary>
    /// <param name="services">Scoped service provider.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static Task SeedCatalogsAsync(this IServiceProvider services, CancellationToken ct)
    {
        return services.GetRequiredService<DatabaseSeeder>().SeedAsync(ct);
    }

    /// <summary>
    /// Creates the initial administrator when the user table is empty, or checks it exists.
    /// </summary>
    /// <param name="services">Scoped service provider.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static Task SeedInitialAdministratorAsync(
        this IServiceProvider services,
        CancellationToken ct
    )
    {
        return services
            .GetRequiredService<InitialAdministratorSeeder>()
            .SeedAsync(services.GetRequiredService<InitialAdministratorOptions>().Email, ct);
    }

    /// <summary>
    /// Adds or refreshes the demo data.
    /// </summary>
    /// <param name="services">Scoped service provider.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static Task SeedDemoDataAsync(this IServiceProvider services, CancellationToken ct)
    {
        return services.GetRequiredService<DemoDataSeeder>().SeedAsync(ct);
    }

    /// <summary>
    /// Replays the changes and erasures of personal data of demo mode when the data history has
    /// none, so the blocked data have something to consult.
    /// </summary>
    /// <param name="services">Scoped service provider.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static Task SeedDemoDataHistoryAsync(
        this IServiceProvider services,
        CancellationToken ct
    )
    {
        return services.GetRequiredService<DemoDataHistorySeeder>().SeedAsync(ct);
    }

    /// <summary>
    /// Adds the invented diagnostic and audit history of demo mode when the log tables have none.
    /// A failure is logged and does not stop the start.
    /// </summary>
    /// <param name="services">Scoped service provider.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static Task SeedDemoLogsAsync(this IServiceProvider services, CancellationToken ct)
    {
        return services.GetRequiredService<DemoLogSeeder>().SeedAsync(ct);
    }
}
