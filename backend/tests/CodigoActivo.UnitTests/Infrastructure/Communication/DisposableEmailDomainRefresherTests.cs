using System.Net;
using AwesomeAssertions;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Infrastructure.Communication;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Communication;

public sealed class DisposableEmailDomainRefresherTests : IDisposable
{
    private static readonly TimeSpan WaitBudget = TimeSpan.FromSeconds(10);

    private readonly FakeDisposableEmailDomainRepository repository = new();
    private readonly RecordingLogger<DisposableEmailDomainRefresher> logger = new();
    private readonly List<IDisposable> owned = [];
    private IDisposableEmailDomainRepository storedIn;

    public DisposableEmailDomainRefresherTests()
    {
        storedIn = repository;
        repository.Add("previous.test");
    }

    public void Dispose()
    {
        foreach (var disposable in owned)
        {
            disposable.Dispose();
        }
    }

    private DisposableEmailDomainRefresher Build(
        StubHttpMessageHandler handler,
        DisposableEmailDomainOptions? options = null
    )
    {
        options ??= new DisposableEmailDomainOptions();
        var provider = new ServiceCollection().AddScoped(_ => storedIn).BuildServiceProvider();
        var downloader = new DisposableEmailDomainDownloader(handler, options);
        var refresher = new DisposableEmailDomainRefresher(
            downloader,
            provider.GetRequiredService<IServiceScopeFactory>(),
            options,
            logger
        );
        owned.AddRange([refresher, downloader, provider]);
        return refresher;
    }

    private static DisposableEmailDomainOptions Looping(TimeSpan refresh, TimeSpan retry)
    {
        return new DisposableEmailDomainOptions
        {
            StartupDelay = TimeSpan.Zero,
            RefreshInterval = refresh,
            RetryInterval = retry,
        };
    }

    [Fact]
    public async Task RefreshAsyncGenuineListReplacesTheStoredOneWithoutLogging()
    {
        var refresher = Build(
            StubHttpMessageHandler.Returning(() =>
                StubHttpMessageHandler.Text(DisposableEmailDomainLists.Genuine("mailinator.com"))
            )
        );

        var stored = await refresher.RefreshAsync(TestContext.Current.CancellationToken);

        stored.Should().BeTrue();
        repository.Domains.Should().HaveCount(DisposableEmailDomainList.MinDomains + 1);
        repository.Domains.Should().Contain("mailinator.com").And.NotContain("previous.test");
        logger.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task RefreshAsyncMissingSourceKeepsTheLastListAndWarns()
    {
        var refresher = Build(
            StubHttpMessageHandler.Returning(() =>
                StubHttpMessageHandler.Text("Not Found", HttpStatusCode.NotFound)
            )
        );

        var stored = await refresher.RefreshAsync(TestContext.Current.CancellationToken);

        stored.Should().BeFalse();
        repository.Domains.Should().Equal("previous.test");
        logger
            .LevelEntries.Should()
            .ContainSingle()
            .Which.Should()
            .Match<(LogLevel Level, string Message)>(entry =>
                entry.Level == LogLevel.Warning
                && entry.Message.Contains("could not be downloaded", StringComparison.Ordinal)
                && entry.Message.Contains("404", StringComparison.Ordinal)
            );
    }

    [Fact]
    public async Task RefreshAsyncChangedContentKeepsTheLastListAndWarnsWithTheReason()
    {
        var refresher = Build(
            StubHttpMessageHandler.Returning(() =>
                StubHttpMessageHandler.Text("{\"domains\":[\"mailinator.com\"]}")
            )
        );

        var stored = await refresher.RefreshAsync(TestContext.Current.CancellationToken);

        stored.Should().BeFalse();
        repository.Domains.Should().Equal("previous.test");
        logger
            .LevelEntries.Should()
            .ContainSingle()
            .Which.Should()
            .Match<(LogLevel Level, string Message)>(entry =>
                entry.Level == LogLevel.Warning
                && entry.Message.Contains("rejected as MalformedEntry", StringComparison.Ordinal)
            );
    }

    [Fact]
    public async Task RefreshAsyncTimeoutIsAFailedDownloadNotAShutdown()
    {
        var refresher = Build(
            new StubHttpMessageHandler(
                async (_, ct) =>
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, ct);
                    return new HttpResponseMessage(HttpStatusCode.OK);
                }
            ),
            new DisposableEmailDomainOptions { DownloadTimeout = TimeSpan.FromMilliseconds(50) }
        );

        var stored = await refresher.RefreshAsync(TestContext.Current.CancellationToken);

        stored.Should().BeFalse();
        repository.Domains.Should().Equal("previous.test");
        logger.LevelEntries.Should().ContainSingle(entry => entry.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task RefreshAsyncCallerCancellationPropagatesWithoutLogging()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var refresher = Build(
            StubHttpMessageHandler.Returning(() =>
                StubHttpMessageHandler.Text(DisposableEmailDomainLists.Genuine())
            )
        );

        var refresh = () => refresher.RefreshAsync(cancelled.Token);

        await refresh.Should().ThrowAsync<OperationCanceledException>();
        repository.Domains.Should().Equal("previous.test");
        logger.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsyncFailedRefreshIsRetriedAfterTheRetryInterval()
    {
        var handler = StubHttpMessageHandler.Returning(
            () => StubHttpMessageHandler.Text("unavailable", HttpStatusCode.ServiceUnavailable),
            () => StubHttpMessageHandler.Text(DisposableEmailDomainLists.Genuine("retried.test"))
        );
        var refresher = Build(
            handler,
            Looping(refresh: TimeSpan.FromHours(1), retry: TimeSpan.FromMilliseconds(10))
        );

        await refresher.StartAsync(TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => repository.Domains.Contains("retried.test"));
        await refresher.StopAsync(TestContext.Current.CancellationToken);

        handler.RequestedUris.Should().HaveCount(2);
        logger.LevelEntries.Should().ContainSingle(entry => entry.Level == LogLevel.Warning);
        refresher.ExecuteTask!.IsFaulted.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsyncStoredListIsRefreshedAgainAfterTheRefreshInterval()
    {
        var handler = StubHttpMessageHandler.Returning(
            () => StubHttpMessageHandler.Text(DisposableEmailDomainLists.Genuine("first.test")),
            () => StubHttpMessageHandler.Text(DisposableEmailDomainLists.Genuine("second.test"))
        );
        var refresher = Build(
            handler,
            Looping(refresh: TimeSpan.FromMilliseconds(10), retry: TimeSpan.FromHours(1))
        );

        await refresher.StartAsync(TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => repository.Domains.Contains("second.test"));
        await refresher.StopAsync(TestContext.Current.CancellationToken);

        repository.Domains.Should().NotContain("first.test");
        logger.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsyncStoreFailureIsLoggedAndNeverFaultsTheHost()
    {
        var failing = Substitute.For<IDisposableEmailDomainRepository>();
        var attempts = 0;
        failing
            .ReplaceAsync(Arg.Any<IReadOnlySet<string>>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ =>
            {
                Interlocked.Increment(ref attempts);
                throw new InvalidOperationException("db down");
            });
        storedIn = failing;
        var refresher = Build(
            StubHttpMessageHandler.Returning(() =>
                StubHttpMessageHandler.Text(DisposableEmailDomainLists.Genuine())
            ),
            Looping(refresh: TimeSpan.FromHours(1), retry: TimeSpan.FromMilliseconds(10))
        );

        await refresher.StartAsync(TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => Volatile.Read(ref attempts) >= 2);
        await refresher.StopAsync(TestContext.Current.CancellationToken);

        refresher.ExecuteTask!.IsFaulted.Should().BeFalse();
        logger
            .LevelEntries.Should()
            .Contain(entry =>
                entry.Level == LogLevel.Error
                && entry.Message.Contains("could not be stored", StringComparison.Ordinal)
                && entry.Message.Contains("db down", StringComparison.Ordinal)
            );
    }

    [Fact]
    public async Task ExecuteAsyncShutdownBeforeTheFirstRunDownloadsNothing()
    {
        var handler = StubHttpMessageHandler.Returning(() =>
            StubHttpMessageHandler.Text(DisposableEmailDomainLists.Genuine())
        );
        var refresher = Build(
            handler,
            new DisposableEmailDomainOptions { StartupDelay = TimeSpan.FromHours(1) }
        );

        await refresher.StartAsync(TestContext.Current.CancellationToken);
        await refresher.StopAsync(TestContext.Current.CancellationToken);

        refresher.ExecuteTask!.IsCompleted.Should().BeTrue();
        refresher.ExecuteTask.IsFaulted.Should().BeFalse();
        handler.RequestedUris.Should().BeEmpty();
        repository.Domains.Should().Equal("previous.test");
        logger.Entries.Should().BeEmpty();
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken
        );
        budget.CancelAfter(WaitBudget);
        while (!condition())
        {
            await Task.Delay(TimeSpan.FromMilliseconds(5), budget.Token);
        }
    }
}
