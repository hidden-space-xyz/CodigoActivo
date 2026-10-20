using CodigoActivo.Application.Accounts.Commands;
using CodigoActivo.Infrastructure.Diagnostics;
using CodigoActivo.Infrastructure.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Infrastructure.Users;

/// <summary>
/// Deletes <c>user_sessions</c> rows whose expiry has passed. Expired rows are already refused by
/// the ticket validator and dropped when the same user logs in again; this worker also clears the
/// rows of accounts that never come back, so the table does not grow without bound.
/// </summary>
/// <param name="scopes">Scope factory used to resolve the use case of each run.</param>
/// <param name="options">Configuration values used by the component.</param>
/// <param name="logger">Logger used to record operational diagnostics.</param>
public sealed class ExpiredSessionCleanupWorker(
    IServiceScopeFactory scopes,
    SessionCleanupOptions options,
    ILogger<ExpiredSessionCleanupWorker> logger
) : BackgroundService
{
    /// <summary>
    /// Removes every session row that has already expired.
    /// </summary>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the number of deleted rows.</returns>
    public Task<int> PurgeAsync(CancellationToken ct = default)
    {
        return ScheduledCommandRunner.RunAsync(scopes, new RemoveExpiredSessionsCommand(), ct);
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
            logger.ExpiredSessionCleanupFailed,
            stoppingToken
        );
    }
}
