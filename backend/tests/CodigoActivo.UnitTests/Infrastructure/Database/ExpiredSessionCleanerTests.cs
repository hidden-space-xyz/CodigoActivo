using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Accounts.Commands;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Database;
using CodigoActivo.Infrastructure.Users;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Database;

public sealed class ExpiredSessionCleanerTests : IDisposable
{
    private static readonly TimeSpan WaitBudget = TimeSpan.FromSeconds(10);

    private readonly IUserSessionRepository sessions = Substitute.For<IUserSessionRepository>();
    private readonly TestClock clock = new();
    private readonly RecordingLogger<ExpiredSessionCleaner> logger = new();
    private readonly ServiceProvider provider;

    public ExpiredSessionCleanerTests()
    {
        provider = new ServiceCollection()
            .AddScoped<IUserSessionRepository>(_ => sessions)
            .AddSingleton<IClock>(clock)
            .AddScoped<
                ICommandHandler<RemoveExpiredSessionsCommand, int>,
                RemoveExpiredSessionsCommandHandler
            >()
            .BuildServiceProvider();
    }

    public void Dispose()
    {
        provider.Dispose();
    }

    private ExpiredSessionCleaner Build(SessionCleanupOptions? options = null)
    {
        return new ExpiredSessionCleaner(
            provider.GetRequiredService<IServiceScopeFactory>(),
            options ?? new SessionCleanupOptions(),
            logger
        );
    }

    private void RemovesAndSignals(TaskCompletionSource signal, int removed)
    {
        sessions
            .RemoveExpiredAsync(
                Arg.Any<DateTimeOffset>(),
                Arg.Any<UserId?>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(_ =>
            {
                signal.TrySetResult();
                return removed;
            });
    }

    [Fact]
    public async Task PurgeAsyncRemovesEverySessionExpiredAtTheCurrentClockTime()
    {
        sessions
            .RemoveExpiredAsync(
                Arg.Any<DateTimeOffset>(),
                Arg.Any<UserId?>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(3);

        var removed = await Build().PurgeAsync(TestContext.Current.CancellationToken);

        removed.Should().Be(3);
        await sessions
            .Received(1)
            .RemoveExpiredAsync(clock.UtcNow, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsyncFirstRunPurgesAfterTheStartupDelayWithoutLogging()
    {
        var signal = new TaskCompletionSource();
        RemovesAndSignals(signal, removed: 2);
        var cleaner = Build(
            new SessionCleanupOptions
            {
                StartupDelay = TimeSpan.Zero,
                Interval = TimeSpan.FromHours(1),
            }
        );

        await cleaner.StartAsync(TestContext.Current.CancellationToken);
        await signal.Task.WaitAsync(WaitBudget, TestContext.Current.CancellationToken);
        await cleaner.StopAsync(TestContext.Current.CancellationToken);

        logger.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsyncEmptyRunLogsNothing()
    {
        var signal = new TaskCompletionSource();
        RemovesAndSignals(signal, removed: 0);
        var cleaner = Build(
            new SessionCleanupOptions
            {
                StartupDelay = TimeSpan.Zero,
                Interval = TimeSpan.FromHours(1),
            }
        );

        await cleaner.StartAsync(TestContext.Current.CancellationToken);
        await signal.Task.WaitAsync(WaitBudget, TestContext.Current.CancellationToken);
        await cleaner.StopAsync(TestContext.Current.CancellationToken);

        logger.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsyncFailedRunIsLoggedAndNeverFaultsTheHost()
    {
        var signal = new TaskCompletionSource();
        sessions
            .RemoveExpiredAsync(
                Arg.Any<DateTimeOffset>(),
                Arg.Any<UserId?>(),
                Arg.Any<CancellationToken>()
            )
            .Returns<Task<int>>(_ =>
            {
                signal.TrySetResult();
                throw new InvalidOperationException("db down");
            });
        var cleaner = Build(
            new SessionCleanupOptions
            {
                StartupDelay = TimeSpan.Zero,
                Interval = TimeSpan.FromHours(1),
            }
        );

        await cleaner.StartAsync(TestContext.Current.CancellationToken);
        await signal.Task.WaitAsync(WaitBudget, TestContext.Current.CancellationToken);
        await cleaner.StopAsync(TestContext.Current.CancellationToken);

        cleaner.ExecuteTask!.IsFaulted.Should().BeFalse();
        logger
            .LevelEntries.Should()
            .Contain(entry => entry.Level == LogLevel.Error && entry.Message.Contains("db down"));
    }

    [Fact]
    public async Task ExecuteAsyncShutdownBeforeTheFirstRunTouchesNothing()
    {
        var cleaner = Build(
            new SessionCleanupOptions
            {
                StartupDelay = TimeSpan.FromHours(1),
                Interval = TimeSpan.FromHours(1),
            }
        );

        await cleaner.StartAsync(TestContext.Current.CancellationToken);
        await cleaner.StopAsync(TestContext.Current.CancellationToken);

        cleaner.ExecuteTask!.IsCompleted.Should().BeTrue();
        cleaner.ExecuteTask.IsFaulted.Should().BeFalse();
        await sessions
            .DidNotReceiveWithAnyArgs()
            .RemoveExpiredAsync(default, default, TestContext.Current.CancellationToken);
        logger.Entries.Should().BeEmpty();
    }
}
