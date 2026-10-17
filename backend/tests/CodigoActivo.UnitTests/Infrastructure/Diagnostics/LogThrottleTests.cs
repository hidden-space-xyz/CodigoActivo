using AwesomeAssertions;
using CodigoActivo.Infrastructure.Diagnostics;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Diagnostics;

public sealed class LogThrottleTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 4, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    [Fact]
    public void TryEnterFirstOccurrenceLetsItThrough()
    {
        var throttle = new LogThrottle(Interval);

        throttle.TryEnter(Now).Should().BeTrue();
    }

    [Fact]
    public void TryEnterRepeatWithinTheIntervalDropsIt()
    {
        var throttle = new LogThrottle(Interval);
        throttle.TryEnter(Now);

        throttle.TryEnter(Now + Interval - TimeSpan.FromSeconds(1)).Should().BeFalse();
    }

    [Fact]
    public void TryEnterRepeatOnceTheIntervalElapsedLetsItThroughAndRestartsTheInterval()
    {
        var throttle = new LogThrottle(Interval);
        throttle.TryEnter(Now);

        throttle.TryEnter(Now + Interval).Should().BeTrue();
        throttle.TryEnter(Now + Interval + TimeSpan.FromMinutes(1)).Should().BeFalse();
    }

    [Fact]
    public void TryEnterFromManyThreadsLetsExactlyOneOccurrenceThrough()
    {
        var throttle = new LogThrottle(Interval);
        var entered = 0;

        Parallel.For(
            0,
            64,
            _ =>
            {
                if (throttle.TryEnter(Now))
                {
                    Interlocked.Increment(ref entered);
                }
            }
        );

        entered.Should().Be(1);
    }
}
