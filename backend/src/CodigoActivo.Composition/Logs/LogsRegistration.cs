using CodigoActivo.Application.Abstractions.Auditing;
using CodigoActivo.Composition.Configuration;
using CodigoActivo.Composition.Persistence;
using CodigoActivo.Infrastructure.Logs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Serilog.Debugging;

namespace CodigoActivo.Composition.Logs;

/// <summary>
/// Registers the log tables: their own connections, the partition maintenance, the audit log and
/// the provider that stores the events of the host.
/// </summary>
public static class LogsRegistration
{
    /// <summary>
    /// Adds the services of the log tables.
    /// </summary>
    /// <param name="services">Service collection to add to.</param>
    /// <param name="configuration">Configuration of the application.</param>
    /// <returns>The same service collection.</returns>
    internal static IServiceCollection AddLogs(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var connectionString = PersistenceRegistration.BuildConnectionString(
            new Settings(configuration)
        );
        services.AddSingleton(_ => new LogDatabase(connectionString));
        services.AddSingleton<LogPartitionMaintainer>();
        services.AddSingleton<IAuditTrail, SerilogAuditTrail>();
        services.AddHostedService<LogRetentionWorker>();
        return services;
    }

    /// <summary>
    /// Stores the events of the host in <c>diagnostic_log_entries</c>. The configured
    /// <c>Logging:LogLevel</c> rules decide what reaches the table; failures of the logging
    /// pipeline itself go to the standard error.
    /// </summary>
    /// <param name="logging">Logging builder of the host.</param>
    /// <returns>The same logging builder.</returns>
    public static ILoggingBuilder AddDatabaseLogging(this ILoggingBuilder logging)
    {
        ArgumentNullException.ThrowIfNull(logging);

        SelfLog.Enable(Console.Error);
        logging.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<ILoggerProvider, DatabaseLoggerProvider>()
        );
        return logging;
    }
}
