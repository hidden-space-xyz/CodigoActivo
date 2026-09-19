using CodigoActivo.API.Security;
using CodigoActivo.Application.Options;
using CodigoActivo.Infrastructure.Communication;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Seeders;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.API.Startup;

internal static class ApplicationInitializer
{
    internal static async Task InitializeAsync(
        this WebApplication app,
        CancellationToken cancellationToken
    )
    {
        await InitializeDatabaseAsync(app, cancellationToken);
        LogEmailGuardState(app);
    }

    private static async Task InitializeDatabaseAsync(
        WebApplication app,
        CancellationToken cancellationToken
    )
    {
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILogger<Program>>();
        var demoMode = services
            .GetRequiredService<DeploymentModeLock>()
            .Lock(app.Configuration, app.Environment);

        await MigrateDatabaseAsync(services, logger, cancellationToken);
        await SeedDatabaseAsync(services, logger, cancellationToken);
        await services
            .GetRequiredService<InitialAdministratorSeeder>()
            .SeedAsync(
                app.Configuration["BOOTSTRAP_ADMIN_EMAIL"],
                app.Configuration["BOOTSTRAP_ADMIN_PASSWORD"],
                cancellationToken
            );
        await SyncDemoDataAsync(services, demoMode);
    }

    private static async Task MigrateDatabaseAsync(
        IServiceProvider services,
        ILogger<Program> logger,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation("Applying database migrations");
        await services
            .GetRequiredService<CodigoActivoDbContext>()
            .Database.MigrateAsync(cancellationToken);
        logger.LogInformation("Database migrations applied");
    }

    private static async Task SeedDatabaseAsync(
        IServiceProvider services,
        ILogger<Program> logger,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation("Seeding database");
        await services
            .GetRequiredService<DatabaseSeeder>()
            .SeedAsync(cancellationToken);
        logger.LogInformation("Database seeding complete");
    }

    private static async Task SyncDemoDataAsync(IServiceProvider services, bool demoMode)
    {
        if (!demoMode)
        {
            return;
        }

        var demoSeeder = services.GetRequiredService<DemoDataSeeder>();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        await demoSeeder.SeedAsync(cancellation.Token);
    }

    private static void LogEmailGuardState(WebApplication app)
    {
        var guard = app.Services.GetRequiredService<EmailGuardOptions>();
        var logger = app.Services.GetRequiredService<ILogger<Program>>();

        if (!logger.IsEnabled(LogLevel.Information))
        {
            return;
        }

        logger.LogInformation(
            "Outbound email guard armed: per recipient {RecipientBurst} burst, {RecipientPerHour}/hour, {RecipientPerDay}/day; overall "
                + "{GlobalBurst} burst, {GlobalPerHour}/hour with {Reserve} reserved for account email. Admin-written email is exempt",
            guard.RecipientBurst,
            guard.RecipientPerHour,
            guard.RecipientPerDay,
            guard.GlobalBurst,
            guard.GlobalPerHour,
            guard.EffectiveCredentialReserve
        );
    }
}
