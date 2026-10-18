using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Files.Commands;
using CodigoActivo.Domain.Common;
using CodigoActivo.Infrastructure.Files;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Files;

public sealed class UnusedFilePurgerTests : IDisposable
{
    private static readonly TimeSpan WaitBudget = TimeSpan.FromSeconds(10);

    private static readonly UnusedFilePurgeOptions Immediate = new()
    {
        StartupDelay = TimeSpan.Zero,
        Interval = TimeSpan.FromHours(1),
    };

    private readonly ICommandHandler<PurgeUnusedFilesCommand, Result<int>> purge = Substitute.For<
        ICommandHandler<PurgeUnusedFilesCommand, Result<int>>
    >();
    private readonly RecordingLogger<UnusedFilePurger> logger = new();
    private readonly ServiceProvider provider;

    public UnusedFilePurgerTests()
    {
        provider = new ServiceCollection().AddScoped(_ => purge).BuildServiceProvider();
    }

    public void Dispose()
    {
        provider.Dispose();
    }

    private UnusedFilePurger Build(UnusedFilePurgeOptions? options = null)
    {
        return new UnusedFilePurger(
            provider.GetRequiredService<IServiceScopeFactory>(),
            options ?? new UnusedFilePurgeOptions(),
            logger
        );
    }

    [Fact]
    public async Task PurgeAsyncRunsTheUseCaseAndReturnsTheDeletedCount()
    {
        purge
            .HandleAsync(Arg.Any<PurgeUnusedFilesCommand>(), Arg.Any<CancellationToken>())
            .Returns(3);

        var purged = await Build().PurgeAsync(TestContext.Current.CancellationToken);

        purged.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsyncFirstRunPurgesAfterTheStartupDelayWithoutLogging()
    {
        var signal = new TaskCompletionSource();
        purge
            .HandleAsync(Arg.Any<PurgeUnusedFilesCommand>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                signal.TrySetResult();
                return 1;
            });
        var purger = Build(Immediate);

        await purger.StartAsync(TestContext.Current.CancellationToken);
        await signal.Task.WaitAsync(WaitBudget, TestContext.Current.CancellationToken);
        await purger.StopAsync(TestContext.Current.CancellationToken);

        logger.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsyncFailedRunIsLoggedAndNeverFaultsTheHost()
    {
        var signal = new TaskCompletionSource();
        purge
            .HandleAsync(Arg.Any<PurgeUnusedFilesCommand>(), Arg.Any<CancellationToken>())
            .Returns<Task<Result<int>>>(_ =>
            {
                signal.TrySetResult();
                throw new InvalidOperationException("db down");
            });
        var purger = Build(Immediate);

        await purger.StartAsync(TestContext.Current.CancellationToken);
        await signal.Task.WaitAsync(WaitBudget, TestContext.Current.CancellationToken);
        await purger.StopAsync(TestContext.Current.CancellationToken);

        purger.ExecuteTask!.IsFaulted.Should().BeFalse();
        logger
            .LevelEntries.Should()
            .Contain(entry => entry.Level == LogLevel.Error && entry.Message.Contains("db down"));
    }

    [Fact]
    public async Task ExecuteAsyncShutdownBeforeTheFirstRunTouchesNothing()
    {
        var purger = Build(
            new UnusedFilePurgeOptions
            {
                StartupDelay = TimeSpan.FromHours(1),
                Interval = TimeSpan.FromHours(1),
            }
        );

        await purger.StartAsync(TestContext.Current.CancellationToken);
        await purger.StopAsync(TestContext.Current.CancellationToken);

        purger.ExecuteTask!.IsCompleted.Should().BeTrue();
        await purge
            .DidNotReceiveWithAnyArgs()
            .HandleAsync(default!, TestContext.Current.CancellationToken);
        logger.Entries.Should().BeEmpty();
    }
}
