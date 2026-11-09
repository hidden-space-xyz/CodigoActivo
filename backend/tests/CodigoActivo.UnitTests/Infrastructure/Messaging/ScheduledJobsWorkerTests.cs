using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Users.Commands;
using CodigoActivo.Domain.Common;
using CodigoActivo.Infrastructure.Messaging;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Messaging;

public sealed class ScheduledJobsWorkerTests : IDisposable
{
    private static readonly TimeSpan WaitBudget = TimeSpan.FromSeconds(10);

    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);

    private readonly ICommandHandler<PurgeAdultDependentsCommand, Result<int>> purge =
        Substitute.For<ICommandHandler<PurgeAdultDependentsCommand, Result<int>>>();
    private readonly ICommandHandler<SendComingOfAgeNoticesCommand, Result<int>> notices =
        Substitute.For<ICommandHandler<SendComingOfAgeNoticesCommand, Result<int>>>();
    private readonly RecordingLogger<ScheduledJobsWorker> logger = new();
    private readonly ServiceProvider provider;

    public ScheduledJobsWorkerTests()
    {
        provider = new ServiceCollection()
            .AddScoped(_ => purge)
            .AddScoped(_ => notices)
            .BuildServiceProvider();
    }

    public void Dispose()
    {
        provider.Dispose();
    }

    private ScheduledJobsWorker Build(params ScheduledJob[] jobs)
    {
        return new ScheduledJobsWorker(
            jobs,
            provider.GetRequiredService<IServiceScopeFactory>(),
            logger
        );
    }

    private static TaskCompletionSource Signal<TCommand>(
        ICommandHandler<TCommand, Result<int>> handler,
        Exception? failure = null
    )
        where TCommand : ICommand<Result<int>>
    {
        var signal = new TaskCompletionSource();
        handler
            .HandleAsync(Arg.Any<TCommand>(), Arg.Any<CancellationToken>())
            .Returns<Task<Result<int>>>(_ =>
            {
                signal.TrySetResult();
                return failure is null ? Task.FromResult<Result<int>>(1) : throw failure;
            });
        return signal;
    }

    [Fact]
    public async Task ExecuteAsyncFirstRunRunsTheCommandsInTurnAfterTheStartupDelayWithoutLogging()
    {
        var purged = Signal(purge);
        var notified = Signal(notices);
        var worker = Build(
            ScheduledJob.Of<PurgeAdultDependentsCommand, SendComingOfAgeNoticesCommand>(Hour) with
            {
                StartupDelay = TimeSpan.Zero,
            }
        );

        await worker.StartAsync(TestContext.Current.CancellationToken);
        await Task.WhenAll(purged.Task, notified.Task)
            .WaitAsync(WaitBudget, TestContext.Current.CancellationToken);
        await worker.StopAsync(TestContext.Current.CancellationToken);

        Received.InOrder(() =>
        {
            _ = purge.HandleAsync(
                Arg.Any<PurgeAdultDependentsCommand>(),
                Arg.Any<CancellationToken>()
            );
            _ = notices.HandleAsync(
                Arg.Any<SendComingOfAgeNoticesCommand>(),
                Arg.Any<CancellationToken>()
            );
        });
        logger.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsyncFailedRunIsLoggedWithItsJobAndNeverStopsTheHostOrTheOtherJobs()
    {
        var purged = Signal(purge, new InvalidOperationException("db down"));
        var notified = Signal(notices);
        var worker = Build(
            ScheduledJob.Of<PurgeAdultDependentsCommand>(Hour) with
            {
                StartupDelay = TimeSpan.Zero,
            },
            ScheduledJob.Of<SendComingOfAgeNoticesCommand>(Hour) with
            {
                StartupDelay = TimeSpan.Zero,
            }
        );

        await worker.StartAsync(TestContext.Current.CancellationToken);
        await Task.WhenAll(purged.Task, notified.Task)
            .WaitAsync(WaitBudget, TestContext.Current.CancellationToken);
        await worker.StopAsync(TestContext.Current.CancellationToken);

        worker.ExecuteTask!.IsFaulted.Should().BeFalse();
        logger
            .LevelEntries.Should()
            .ContainSingle()
            .Which.Should()
            .Match<(LogLevel Level, string Message)>(entry =>
                entry.Level == LogLevel.Error
                && entry.Message.Contains(nameof(PurgeAdultDependentsCommand))
                && entry.Message.Contains("db down")
            );
    }

    [Fact]
    public async Task ExecuteAsyncShutdownBeforeTheFirstRunTouchesNothing()
    {
        var worker = Build(ScheduledJob.Of<PurgeAdultDependentsCommand>(Hour));

        await worker.StartAsync(TestContext.Current.CancellationToken);
        await worker.StopAsync(TestContext.Current.CancellationToken);

        worker.ExecuteTask!.IsCompleted.Should().BeTrue();
        worker.ExecuteTask.IsFaulted.Should().BeFalse();
        await purge
            .DidNotReceiveWithAnyArgs()
            .HandleAsync(default!, TestContext.Current.CancellationToken);
        logger.Entries.Should().BeEmpty();
    }
}
