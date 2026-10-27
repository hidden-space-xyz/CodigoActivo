using CodigoActivo.Application.Accounts.Commands;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Diagnostics;
using CodigoActivo.Infrastructure.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Infrastructure.Users;

/// <summary>
/// Erases the accounts nobody verified within <see cref="User.UnverifiedRetentionDays"/> of their
/// registration. It runs shortly after startup, so a stopped API only delays the erasure until the
/// next start, and then once per interval.
/// </summary>
/// <param name="scopes">Scope factory used to resolve the use case of each run.</param>
/// <param name="options">Startup delay and interval of the purges.</param>
/// <param name="logger">Logger used to record operational diagnostics.</param>
public sealed class UnverifiedAccountPurgeWorker(
    IServiceScopeFactory scopes,
    UnverifiedAccountPurgeOptions options,
    ILogger<UnverifiedAccountPurgeWorker> logger
) : BackgroundService
{
    /// <summary>
    /// Erases every account whose verification has already expired.
    /// </summary>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the number of erased accounts.</returns>
    public Task<int> PurgeAsync(CancellationToken ct = default)
    {
        return ScheduledCommandRunner.RunAsync(scopes, new PurgeUnverifiedAccountsCommand(), ct);
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
            logger.UnverifiedAccountPurgeFailed,
            stoppingToken
        );
    }
}
