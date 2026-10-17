using System.Diagnostics;
using CodigoActivo.Application.Abstractions.Auditing;
using CodigoActivo.Application.Abstractions.Time;
using Serilog;
using Serilog.Core;

namespace CodigoActivo.Infrastructure.Logs;

/// <summary>
/// Audit log over a batching Serilog pipeline of its own that writes <c>audit_log_entries</c>
/// through <see cref="AuditLogSink"/>. Recording only enqueues, with the instant of the clock
/// and the trace of the current activity, so the command never waits for the database; the
/// pipeline shares the limits and failure handling of the diagnostic log. Disposing it writes what
/// is still queued.
/// </summary>
public sealed class SerilogAuditTrail : IAuditTrail, IDisposable, IAsyncDisposable
{
    private readonly Logger logger;
    private readonly IClock clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="SerilogAuditTrail"/> class.
    /// </summary>
    /// <param name="database">Connections of the log tables.</param>
    /// <param name="partitions">Maintenance of the log partitions.</param>
    /// <param name="clock">Clock used to obtain consistent application timestamps.</param>
    public SerilogAuditTrail(LogDatabase database, LogPartitionMaintainer partitions, IClock clock)
    {
        this.clock = clock;
        logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(new AuditLogSink(database, partitions), LogBatching.CreateOptions())
            .CreateLogger();
    }

    /// <inheritdoc />
    public void Record(AuditRecord record)
    {
        logger.Write(AuditLogEvents.ToEvent(record, clock.UtcNow, Activity.Current));
    }

    /// <summary>
    /// Writes the queued entries and closes the pipeline.
    /// </summary>
    public void Dispose()
    {
        logger.Dispose();
    }

    /// <summary>
    /// Writes the queued entries and closes the pipeline.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public ValueTask DisposeAsync()
    {
        return logger.DisposeAsync();
    }
}
