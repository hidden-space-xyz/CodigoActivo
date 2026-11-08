using CodigoActivo.Application.Accounts.Commands;
using CodigoActivo.Infrastructure.Diagnostics;
using CodigoActivo.Infrastructure.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Infrastructure.Users;

/// <summary>
/// Drops the email changes and authenticator enrollments that can no longer be confirmed. An
/// expired one is already refused; this worker also removes the address and the secret of the
/// accounts that never ask again, so neither is kept longer than it can be used.
/// </summary>
/// <param name="scopes">Scope factory used to resolve the use case of each run.</param>
/// <param name="options">Startup delay and interval of the cleanups.</param>
/// <param name="logger">Logger used to record operational diagnostics.</param>
public sealed class ExpiredAccountChangeCleanupWorker(
    IServiceScopeFactory scopes,
    ExpiredAccountChangeCleanupOptions options,
    ILogger<ExpiredAccountChangeCleanupWorker> logger
) : BackgroundService
{
    /// <summary>
    /// Drops every email change and authenticator enrollment that has already expired.
    /// </summary>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the number of accounts changed.</returns>
    public Task<int> CleanUpAsync(CancellationToken ct = default)
    {
        return ScheduledCommandRunner.RunAsync(
            scopes,
            new DiscardExpiredAccountChangesCommand(),
            ct
        );
    }

    /// <summary>
    /// Cleans up shortly after startup and then once per configured interval until shutdown. A
    /// failed run is logged and never stops the host or the following runs.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token that stops the runs at shutdown.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return ScheduledCommandRunner.RepeatAsync(
            options.StartupDelay,
            options.Interval,
            CleanUpAsync,
            logger.ExpiredAccountChangeCleanupFailed,
            stoppingToken
        );
    }
}
