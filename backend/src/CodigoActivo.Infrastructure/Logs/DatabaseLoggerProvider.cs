using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace CodigoActivo.Infrastructure.Logs;

/// <summary>
/// Logging provider that stores the events of the host in <c>diagnostic_log_entries</c> through
/// a batching Serilog pipeline over <see cref="DiagnosticLogSink"/>. It applies no level of its
/// own: <c>Logging:LogLevel</c> (or <c>Logging:Database:LogLevel</c>) decides what reaches it.
/// Disposing it writes what is still queued.
/// </summary>
[ProviderAlias("Database")]
public sealed class DatabaseLoggerProvider : ILoggerProvider, ISupportExternalScope, IAsyncDisposable
{
    private readonly SerilogLoggerProvider provider;

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseLoggerProvider"/> class.
    /// </summary>
    /// <param name="database">Connections of the log tables.</param>
    /// <param name="partitions">Maintenance of the log partitions.</param>
    public DatabaseLoggerProvider(LogDatabase database, LogPartitionMaintainer partitions)
    {
        var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(new DiagnosticLogSink(database, partitions), LogBatching.CreateOptions())
            .CreateLogger();
        provider = new SerilogLoggerProvider(logger, dispose: true);
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName)
    {
        return provider.CreateLogger(categoryName);
    }

    /// <inheritdoc />
    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
    {
        provider.SetScopeProvider(scopeProvider);
    }

    /// <summary>
    /// Writes the queued events and closes the pipeline.
    /// </summary>
    public void Dispose()
    {
        provider.Dispose();
    }

    /// <summary>
    /// Writes the queued events and closes the pipeline.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public ValueTask DisposeAsync()
    {
        return provider.DisposeAsync();
    }
}
