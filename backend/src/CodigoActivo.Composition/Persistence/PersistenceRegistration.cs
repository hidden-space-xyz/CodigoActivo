using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Composition.Configuration;
using CodigoActivo.Infrastructure.Database;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Seeders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CodigoActivo.Composition.Persistence;

/// <summary>
/// Registers the database: the write and read contexts, the unit of work and the seeders.
/// </summary>
internal static class PersistenceRegistration
{
    /// <summary>
    /// Adds the database services.
    /// </summary>
    /// <param name="services">Service collection to add to.</param>
    /// <param name="configuration">Configuration of the application.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var connectionString = BuildConnectionString(new Settings(configuration));
        services.AddDbContext<CodigoActivoDbContext>(options =>
            options
                .UseNpgsql(
                    connectionString,
                    npgsql =>
                        npgsql.MigrationsAssembly(typeof(CodigoActivoDbContext).Assembly.FullName)
                )
                .UseSnakeCaseNamingConvention()
        );
        services.AddDbContext<CodigoActivoReadDbContext>(options =>
            options
                .UseNpgsql(connectionString)
                .UseSnakeCaseNamingConvention()
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
        );

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IReadStore>(sp => sp.GetRequiredService<CodigoActivoReadDbContext>());
        services.AddSingleton<IQueryExecutor, QueryExecutor>();
        services.AddScoped<DatabaseSeeder>();
        services.AddScoped<InitialAdministratorSeeder>();
        services.AddScoped<DemoDataSeeder>();
        return services;
    }

    private static string BuildConnectionString(Settings settings)
    {
        return new NpgsqlConnectionStringBuilder
        {
            Host = settings.Text("POSTGRES_HOST", "localhost"),
            Port = settings.PositiveInt("POSTGRES_PORT", 5432),
            Database = settings.Text("POSTGRES_DB", "codigoactivo"),
            Username = settings.Text("POSTGRES_USER", "codigoactivo"),
            Password = settings.Verbatim("POSTGRES_PASSWORD"),
        }.ConnectionString;
    }
}
