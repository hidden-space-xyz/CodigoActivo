using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Files.Commands;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Files;
using CodigoActivo.Infrastructure.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Infrastructure.Files;

/// <summary>
/// Deletes the uploads that no content references <see cref="StoredFile.UnusedRetentionDays"/>
/// after they were uploaded. It runs shortly after startup, so a stopped API only delays the
/// deletion until the next start, and then once per interval.
/// </summary>
/// <param name="scopes">Scope factory used to resolve the use case of each run.</param>
/// <param name="options">Configuration values used by the component.</param>
/// <param name="logger">Logger used to record operational diagnostics.</param>
public sealed class UnusedFilePurger(
    IServiceScopeFactory scopes,
    UnusedFilePurgeOptions options,
    ILogger<UnusedFilePurger> logger
) : BackgroundService
{
    /// <summary>
    /// Deletes every upload that no content used in time.
    /// </summary>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the number of deleted files.</returns>
    public async Task<int> PurgeAsync(CancellationToken ct = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var purge = scope.ServiceProvider.GetRequiredService<
            ICommandHandler<PurgeUnusedFilesCommand, Result<int>>
        >();
        var purged = await purge.HandleAsync(new PurgeUnusedFilesCommand(), ct);
        return purged.Value;
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
            logger.UnusedFilePurgeFailed(ex);
        }
    }
}
