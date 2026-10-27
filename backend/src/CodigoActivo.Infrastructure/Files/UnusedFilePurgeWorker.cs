using CodigoActivo.Application.Files.Commands;
using CodigoActivo.Domain.Files;
using CodigoActivo.Infrastructure.Diagnostics;
using CodigoActivo.Infrastructure.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Infrastructure.Files;

/// <summary>
/// Deletes the uploads that no content references <see cref="StoredFile.UnusedRetentionDays"/>
/// after they were uploaded, and then the stored content that no file row owns. It runs shortly
/// after startup, so a stopped API only delays the deletion until the next start, and then once
/// per interval.
/// </summary>
/// <param name="scopes">Scope factory used to resolve the use case of each run.</param>
/// <param name="options">Startup delay and interval of the purges.</param>
/// <param name="logger">Logger used to record operational diagnostics.</param>
public sealed class UnusedFilePurgeWorker(
    IServiceScopeFactory scopes,
    UnusedFilePurgeOptions options,
    ILogger<UnusedFilePurgeWorker> logger
) : BackgroundService
{
    /// <summary>
    /// Deletes every upload that no content used in time, then the stored content no file row owns.
    /// </summary>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the number of deleted uploads; stray content removed
    /// without a row is not counted.</returns>
    public Task<int> PurgeAsync(CancellationToken ct = default)
    {
        return ScheduledCommandRunner.RunAsync(scopes, new PurgeUnusedFilesCommand(), ct);
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
            options.StartupDelay,
            options.Interval,
            PurgeAsync,
            logger.UnusedFilePurgeFailed,
            stoppingToken
        );
    }
}
