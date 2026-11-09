using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Domain.Common;
using Microsoft.Extensions.DependencyInjection;

namespace CodigoActivo.Infrastructure.Messaging;

/// <summary>
/// Scheduled commands that <see cref="ScheduledJobsWorker"/> runs after
/// <see cref="StartupDelay"/>, so startup work finishes first and a stopped API only delays them
/// until the next start, and then once per interval. Each feature registers its own.
/// </summary>
/// <param name="Name">Name a failed run is logged with.</param>
/// <param name="Interval">Interval between two runs.</param>
/// <param name="Run">Work of one run.</param>
public sealed record ScheduledJob(
    string Name,
    TimeSpan Interval,
    Func<IServiceScopeFactory, CancellationToken, Task> Run
)
{
    /// <summary>
    /// Gets the delay before the first run.
    /// </summary>
    public TimeSpan StartupDelay { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Creates a job that runs <typeparamref name="TCommand"/> in a scope of its own.
    /// </summary>
    /// <typeparam name="TCommand">Command to run.</typeparam>
    /// <param name="interval">Interval between two runs.</param>
    /// <returns>The job.</returns>
    public static ScheduledJob Of<TCommand>(TimeSpan interval)
        where TCommand : ICommand<Result<int>>, new()
    {
        return new ScheduledJob(
            typeof(TCommand).Name,
            interval,
            (scopes, ct) => ScheduledCommandRunner.RunAsync(scopes, new TCommand(), ct)
        );
    }

    /// <summary>
    /// Creates a job that runs <typeparamref name="TFirst"/> and then
    /// <typeparamref name="TThen"/>, each in a scope of its own.
    /// </summary>
    /// <typeparam name="TFirst">Command to run first.</typeparam>
    /// <typeparam name="TThen">Command to run once the first one finished.</typeparam>
    /// <param name="interval">Interval between two runs.</param>
    /// <returns>The job.</returns>
    public static ScheduledJob Of<TFirst, TThen>(TimeSpan interval)
        where TFirst : ICommand<Result<int>>, new()
        where TThen : ICommand<Result<int>>, new()
    {
        return new ScheduledJob(
            typeof(TFirst).Name,
            interval,
            async (scopes, ct) =>
            {
                await ScheduledCommandRunner.RunAsync(scopes, new TFirst(), ct);
                await ScheduledCommandRunner.RunAsync(scopes, new TThen(), ct);
            }
        );
    }
}
