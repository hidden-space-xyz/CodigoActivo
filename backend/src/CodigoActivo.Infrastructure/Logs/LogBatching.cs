using Serilog.Configuration;

namespace CodigoActivo.Infrastructure.Logs;

/// <summary>
/// Sets how the log sinks queue and batch events. Emitting an event only enqueues it: the queue is
/// bounded, so a database that stops accepting writes costs at most its limit in memory and drops
/// events instead of slowing the requests down, and a batch that keeps failing is dropped once its
/// retry time is spent.
/// </summary>
public static class LogBatching
{
    /// <summary>
    /// Creates the batching options of a log sink.
    /// </summary>
    /// <returns>New options, since Serilog keeps the instance it is handed.</returns>
    public static BatchingOptions CreateOptions()
    {
        return new BatchingOptions
        {
            BatchSizeLimit = 1_000,
            BufferingTimeLimit = TimeSpan.FromSeconds(2),
            QueueLimit = 10_000,
            RetryTimeLimit = TimeSpan.FromMinutes(2),
        };
    }
}
