using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Seeders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CodigoActivo.Composition;

/// <summary>
/// Runs the database start-up steps from a service scope: schema migrations, catalog seeding, the
/// initial administrator and the optional demo data.
/// </summary>
public static class DatabaseInitialization
{
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
    /// <param name="email">Configured bootstrap administrator email.</param>
    /// <param name="password">Configured bootstrap administrator password.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static Task SeedInitialAdministratorAsync(
        this IServiceProvider services,
        string? email,
        string? password,
        CancellationToken ct
    )
    {
        return services
            .GetRequiredService<InitialAdministratorSeeder>()
            .SeedAsync(email, password, ct);
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
}
