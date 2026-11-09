using CodigoActivo.Infrastructure.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Infrastructure.Messaging;

/// <summary>
/// Runs every registered <see cref="ScheduledJob"/> on its own schedule until shutdown. A failed
/// run is logged and never stops the host, the following runs or the other jobs.
/// </summary>
/// <param name="jobs">Jobs to run.</param>
/// <param name="scopes">Scope factory used to resolve the use cases of each run.</param>
/// <param name="logger">Logger used to record operational diagnostics.</param>
public sealed class ScheduledJobsWorker(
    IEnumerable<ScheduledJob> jobs,
    IServiceScopeFactory scopes,
    ILogger<ScheduledJobsWorker> logger
) : BackgroundService
{
    /// <summary>
    /// Runs the jobs side by side until shutdown.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token that stops the runs at shutdown.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.WhenAll(
            jobs.Select(job =>
                ScheduledCommandRunner.RepeatAsync(
                    job.StartupDelay,
                    job.Interval,
                    ct => job.Run(scopes, ct),
                    exception => logger.ScheduledJobFailed(exception, job.Name),
                    stoppingToken
                )
            )
        );
    }
}
