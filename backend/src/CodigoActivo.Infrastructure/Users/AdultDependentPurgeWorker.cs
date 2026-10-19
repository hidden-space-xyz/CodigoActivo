using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Users.Commands;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Infrastructure.Users;

/// <summary>
/// Erases the dependents who came of age and then tells the guardians of those who come of age
/// within <see cref="User.ComingOfAgeNoticeDays"/> days. It runs shortly after startup, so a stopped
/// API only delays both until the next start, and then once per interval.
/// </summary>
/// <param name="scopes">Scope factory used to resolve the use cases of each run.</param>
/// <param name="options">Configuration values used by the component.</param>
/// <param name="logger">Logger used to record operational diagnostics.</param>
public sealed class AdultDependentPurgeWorker(
    IServiceScopeFactory scopes,
    AdultDependentPurgeOptions options,
    ILogger<AdultDependentPurgeWorker> logger
) : BackgroundService
{
    /// <summary>
    /// Erases every dependent who has come of age, then sends the notices that are due.
    /// </summary>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the number of erased dependents and of notices sent.</returns>
    public async Task<(int Erased, int Notified)> PurgeAsync(CancellationToken ct = default)
    {
        var erased = await RunAsync(new PurgeAdultDependentsCommand(), ct);
        var notified = await RunAsync(new SendComingOfAgeNoticesCommand(), ct);
        return (erased, notified);
    }

    /// <summary>
    /// Runs shortly after startup and then once per configured interval until shutdown. A failed
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

    private async Task<int> RunAsync<TCommand>(TCommand command, CancellationToken ct)
        where TCommand : ICommand<Result<int>>
    {
        await using var scope = scopes.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<
            ICommandHandler<TCommand, Result<int>>
        >();
        var result = await handler.HandleAsync(command, ct);
        return result.Value;
    }

    private async Task PurgeSafelyAsync(CancellationToken ct)
    {
        try
        {
            await PurgeAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.AdultDependentPurgeFailed(ex);
        }
    }
}
