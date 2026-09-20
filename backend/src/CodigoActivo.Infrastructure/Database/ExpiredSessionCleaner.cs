using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Infrastructure.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Infrastructure.Database;

/// <summary>
/// Deletes <c>user_sessions</c> rows whose expiry has passed. Expired rows are already refused by
/// the ticket validator and dropped when the same user logs in again; this worker also clears the
/// rows of accounts that never come back, so the table does not grow without bound.
/// </summary>
/// <param name="scopes">Scope factory used to resolve the scoped repository of each run.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="options">Configuration values used by the component.</param>
/// <param name="logger">Logger used to record operational diagnostics.</param>
public sealed class ExpiredSessionCleaner(
    IServiceScopeFactory scopes,
    IClock clock,
    SessionCleanupOptions options,
    ILogger<ExpiredSessionCleaner> logger
) : BackgroundService
{
    /// <summary>
    /// Removes every session row that has already expired.
    /// </summary>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the number of deleted rows.</returns>
    public async Task<int> PurgeAsync(CancellationToken ct = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var sessions = scope.ServiceProvider.GetRequiredService<IUserSessionRepository>();
        var now = clock.UtcNow;
        return await sessions.RemoveAsync(session => session.ExpiresAt <= now, ct);
    }

    /// <summary>
    /// Purges shortly after startup and then once per configured interval until shutdown. A failed
    /// run is logged and never stops the host or the following runs.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Interval);

        try
        {
            await Task.Delay(options.StartupDelay, stoppingToken);

            do
            {
                await PurgeSafelyAsync(stoppingToken);
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            return;
        }
    }

    private async Task PurgeSafelyAsync(CancellationToken ct)
    {
        try
        {
            await PurgeAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.ExpiredSessionCleanupFailed(ex);
        }
    }
}
