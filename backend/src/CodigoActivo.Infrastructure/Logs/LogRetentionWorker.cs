using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Infrastructure.Logs;

/// <summary>
/// Runs the log table maintenance once per hour, so the next days always have their partitions
/// and no entry outlives the retention of its table. Startup runs it once right after the
/// migrations; a failed run is logged and never stops the host or the following runs.
/// </summary>
/// <param name="maintainer">Maintenance of the log partitions.</param>
/// <param name="logger">Logger used to record operational diagnostics.</param>
public sealed class LogRetentionWorker(
    LogPartitionMaintainer maintainer,
    ILogger<LogRetentionWorker> logger
) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    /// <summary>
    /// Waits one interval, since startup has just maintained the tables, and then maintains them
    /// once per interval until shutdown.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await maintainer.TryMaintainAsync(logger, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
    }
}
