using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.Database.Context;

/// <summary>
/// Configures the database contexts the same way for the application and for the EF Core tools,
/// so the model the migrations are built from is the model the application runs.
/// </summary>
public static class PostgresContexts
{
    /// <summary>
    /// Points the write context at PostgreSQL with the migrations of this assembly.
    /// </summary>
    /// <param name="options">Options of the write context.</param>
    /// <param name="connectionString">Connection string of the database.</param>
    /// <returns>The same options.</returns>
    public static DbContextOptionsBuilder UseWriteDatabase(
        this DbContextOptionsBuilder options,
        string connectionString
    )
    {
        ArgumentNullException.ThrowIfNull(options);

        return options
            .UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsAssembly(typeof(CodigoActivoDbContext).Assembly.FullName)
            )
            .UseSnakeCaseNamingConvention();
    }

    /// <summary>
    /// Points the read context at PostgreSQL without change tracking.
    /// </summary>
    /// <param name="options">Options of the read context.</param>
    /// <param name="connectionString">Connection string of the database.</param>
    /// <returns>The same options.</returns>
    public static DbContextOptionsBuilder UseReadDatabase(
        this DbContextOptionsBuilder options,
        string connectionString
    )
    {
        ArgumentNullException.ThrowIfNull(options);

        return options
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
    }
}
