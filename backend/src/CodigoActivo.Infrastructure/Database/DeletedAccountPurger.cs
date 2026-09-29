using CodigoActivo.Application.Users.Commands;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Infrastructure.Database;

/// <summary>
/// Physically deletes the copies of erased accounts once <see cref="DeletedAccount.RetentionYears"/>
/// have passed since the deletion. It runs shortly after startup, so restoring an old backup or a
/// stopped API only delays the purge until the next start, and then once per interval.
/// </summary>
/// <param name="scopes">Scope factory used to resolve the use case of each run.</param>
/// <param name="options">Configuration values used by the component.</param>
/// <param name="logger">Logger used to record operational diagnostics.</param>
public sealed class DeletedAccountPurger(
    IServiceScopeFactory scopes,
    DeletedAccountPurgeOptions options,
    ILogger<DeletedAccountPurger> logger
) : BackgroundService
{
    /// <summary>
    /// Removes every copy whose retention has already ended.
    /// </summary>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the number of purged copies.</returns>
    public async Task<int> PurgeAsync(CancellationToken ct = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var purge = scope.ServiceProvider.GetRequiredService<PurgeDeletedAccountsCommandHandler>();
        return await purge.HandleAsync(new PurgeDeletedAccountsCommand(), ct);
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
            logger.DeletedAccountPurgeFailed(ex);
        }
    }
}
