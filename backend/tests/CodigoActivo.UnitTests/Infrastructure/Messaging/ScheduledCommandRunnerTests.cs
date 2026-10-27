using AwesomeAssertions;
using CodigoActivo.Infrastructure.Messaging;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Messaging;

public sealed class ScheduledCommandRunnerTests : IDisposable
{
    private static readonly TimeSpan WaitBudget = TimeSpan.FromSeconds(10);

    private readonly CancellationTokenSource shutdown =
        CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly List<Exception> failures = [];

    public void Dispose()
    {
        shutdown.Dispose();
    }

    private Task RepeatAsync(Func<CancellationToken, Task> run, TimeSpan interval)
    {
        return ScheduledCommandRunner
            .RepeatAsync(TimeSpan.Zero, interval, run, failures.Add, shutdown.Token)
            .WaitAsync(WaitBudget, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task RepeatAsyncForeignCancellationIsLoggedAndTheRunsGoOn()
    {
        using var timeout = new CancellationTokenSource();
        await timeout.CancelAsync();
        var runs = 0;

        await RepeatAsync(
            async _ =>
            {
                runs++;
                if (runs == 1)
                {
                    await Task.FromCanceled(timeout.Token);
                }

                await shutdown.CancelAsync();
            },
            TimeSpan.FromMilliseconds(10)
        );

        runs.Should().Be(2);
        failures.Should().ContainSingle().Which.Should().BeOfType<TaskCanceledException>();
    }

    [Fact]
    public async Task RepeatAsyncShutdownDuringARunEndsTheRunsWithoutLogging()
    {
        var runs = 0;

        await RepeatAsync(
            async ct =>
            {
                runs++;
                await shutdown.CancelAsync();
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
                linked.Token.ThrowIfCancellationRequested();
            },
            TimeSpan.FromMilliseconds(10)
        );

        runs.Should().Be(1);
        failures.Should().BeEmpty();
    }

    [Fact]
    public async Task RepeatAsyncFailureDuringShutdownIsStillLogged()
    {
        await RepeatAsync(
            async _ =>
            {
                await shutdown.CancelAsync();
                throw new InvalidOperationException("db down");
            },
            TimeSpan.FromHours(1)
        );

        failures.Should().ContainSingle().Which.Should().BeOfType<InvalidOperationException>();
    }
}
