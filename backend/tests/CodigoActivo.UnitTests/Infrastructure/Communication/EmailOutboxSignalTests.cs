using AwesomeAssertions;
using CodigoActivo.Infrastructure.Communication;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Communication;

public sealed class EmailOutboxSignalTests
{
    [Fact]
    public async Task WaitAsyncAfterANotificationReturnsWithoutWaitingForTheTimeout()
    {
        var sut = new EmailOutboxSignal();
        sut.Notify();

        var signalled = await sut.WaitAsync(
            TimeSpan.FromMinutes(5),
            TestContext.Current.CancellationToken
        );

        signalled.Should().BeTrue();
    }

    [Fact]
    public async Task WaitAsyncWithoutNotificationReturnsWhenTheTimeoutElapses()
    {
        var sut = new EmailOutboxSignal();

        var first = await sut.WaitAsync(
            TimeSpan.FromMilliseconds(10),
            TestContext.Current.CancellationToken
        );
        var second = await sut.WaitAsync(
            TimeSpan.FromMilliseconds(10),
            TestContext.Current.CancellationToken
        );

        first.Should().BeFalse();
        second.Should().BeFalse();
    }

    [Fact]
    public async Task WaitAsyncRepeatedNotificationsAreCollapsedIntoOne()
    {
        var sut = new EmailOutboxSignal();
        sut.Notify();
        sut.Notify();
        sut.Notify();

        var signalled = await sut.WaitAsync(
            TimeSpan.FromMinutes(5),
            TestContext.Current.CancellationToken
        );
        var again = await sut.WaitAsync(
            TimeSpan.FromMilliseconds(10),
            TestContext.Current.CancellationToken
        );

        signalled.Should().BeTrue();
        again
            .Should()
            .BeFalse("three notifications release one wait, not one wait for each of them");
    }

    [Fact]
    public async Task WaitAsyncCancelledCallerThrowsOperationCanceled()
    {
        var sut = new EmailOutboxSignal();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var act = () => sut.WaitAsync(TimeSpan.FromMinutes(5), cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
