using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Domain.Common;
using Microsoft.Extensions.DependencyInjection;

namespace CodigoActivo.Infrastructure.Messaging;

/// <summary>
/// Runs the scheduled commands of the purge workers: each command in a scope of its own, and the
/// runs shortly after startup and then once per interval until shutdown.
/// </summary>
internal static class ScheduledCommandRunner
{
    /// <summary>
    /// Runs <paramref name="run"/> after <paramref name="startupDelay"/> and then once per
    /// <paramref name="interval"/> until shutdown. A failed run is handed to
    /// <paramref name="logFailure"/> and never stops the host or the following runs.
    /// </summary>
    /// <param name="startupDelay">Delay before the first run, so startup work finishes first.</param>
    /// <param name="interval">Interval between two runs.</param>
    /// <param name="run">Work of one run.</param>
    /// <param name="logFailure">Records a failed run.</param>
    /// <param name="stoppingToken">Cancellation token that stops the runs at shutdown.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal static async Task RepeatAsync(
        TimeSpan startupDelay,
        TimeSpan interval,
        Func<CancellationToken, Task> run,
        Action<Exception> logFailure,
        CancellationToken stoppingToken
    )
    {
        using var timer = new PeriodicTimer(interval);

        try
        {
            await Task.Delay(startupDelay, stoppingToken);

            do
            {
                try
                {
                    await run(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logFailure(ex);
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            return;
        }
    }

    /// <summary>
    /// Runs one scheduled command in a scope of its own.
    /// </summary>
    /// <typeparam name="TCommand">Command to run.</typeparam>
    /// <param name="scopes">Scope factory used to resolve the handler of the command.</param>
    /// <param name="command">Command to run.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the count the command reports.</returns>
    internal static async Task<int> RunAsync<TCommand>(
        IServiceScopeFactory scopes,
        TCommand command,
        CancellationToken ct
    )
        where TCommand : ICommand<Result<int>>
    {
        await using var scope = scopes.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<
            ICommandHandler<TCommand, Result<int>>
        >();
        var result = await handler.HandleAsync(command, ct);
        return result.Value;
    }
}
