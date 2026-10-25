using CodigoActivo.Application.BlockedData.Commands;
using CodigoActivo.Infrastructure.Diagnostics;
using CodigoActivo.Infrastructure.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Infrastructure.DataHistory;

/// <summary>
/// Physically deletes the data history whose retention has ended. It runs shortly after startup,
/// so a stopped API only delays the purge until the next start, and then once per interval.
/// </summary>
/// <param name="scopes">Scope factory used to resolve the use cases of each run.</param>
/// <param name="options">Configuration values used by the component.</param>
/// <param name="logger">Logger used to record operational diagnostics.</param>
public sealed class DataHistoryPurgeWorker(
    IServiceScopeFactory scopes,
    DataHistoryRetentionOptions options,
    ILogger<DataHistoryPurgeWorker> logger
) : BackgroundService
{
    /// <summary>
    /// Removes every entry whose retention has already ended.
    /// </summary>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the number of purged entries.</returns>
    public Task<int> PurgeAsync(CancellationToken ct = default)
    {
        return ScheduledCommandRunner.RunAsync(scopes, new PurgeDataHistoryCommand(), ct);
    }

    /// <summary>
    /// Purges shortly after startup and then once per configured interval until shutdown. A failed
    /// run is logged and never stops the host or the following runs.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return ScheduledCommandRunner.RepeatAsync(
            options.PurgeStartupDelay,
            options.PurgeInterval,
            PurgeAsync,
            logger.DataHistoryPurgeFailed,
            stoppingToken
        );
    }
}
