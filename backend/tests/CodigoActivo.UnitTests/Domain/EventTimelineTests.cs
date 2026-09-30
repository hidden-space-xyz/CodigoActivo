using AwesomeAssertions;
using CodigoActivo.Domain.Events;
using Xunit;

namespace CodigoActivo.UnitTests.Domain;

public sealed class EventTimelineTests
{
    private static readonly DateOnly EventEnd = new(2026, 8, 3);
    private static readonly DateTimeOffset EarlyStart = new(2026, 6, 20, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset SignupStart = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset SignupEnd = new(2026, 7, 20, 0, 0, 0, TimeSpan.Zero);

    public static TheoryData<DateTimeOffset, DateOnly, EventStage> StagesWithEarlySignup =>
        new()
        {
            { EarlyStart.AddSeconds(-1), new DateOnly(2026, 6, 19), EventStage.Upcoming },
            { EarlyStart, new DateOnly(2026, 6, 20), EventStage.EarlySignupOpen },
            { SignupStart.AddSeconds(-1), new DateOnly(2026, 6, 30), EventStage.EarlySignupOpen },
            { SignupStart, new DateOnly(2026, 7, 1), EventStage.SignupOpen },
            { SignupEnd, new DateOnly(2026, 7, 20), EventStage.SignupOpen },
            { SignupEnd.AddSeconds(1), new DateOnly(2026, 7, 20), EventStage.SignupClosed },
            { SignupEnd.AddDays(14), EventEnd, EventStage.SignupClosed },
            { SignupEnd.AddDays(15), EventEnd.AddDays(1), EventStage.Finished },
        };

    [Theory]
    [MemberData(nameof(StagesWithEarlySignup))]
    public void StageAtWithEarlySignupFollowsTheTimeline(
        DateTimeOffset now,
        DateOnly today,
        EventStage expected
    )
    {
        EventTimeline
            .StageAt(EventEnd, EarlyStart, SignupStart, SignupEnd, now, today)
            .Should()
            .Be(expected);
    }

    [Fact]
    public void StageAtWithoutEarlySignupIsUpcomingUntilTheSignupOpens()
    {
        EventTimeline
            .StageAt(EventEnd, null, SignupStart, SignupEnd, EarlyStart, new DateOnly(2026, 6, 20))
            .Should()
            .Be(EventStage.Upcoming);
    }

    [Fact]
    public void StageAtLastDayPassedIsFinishedEvenWhileTheSignupIsOpen()
    {
        EventTimeline
            .StageAt(EventEnd, null, SignupStart, SignupEnd, SignupStart, EventEnd.AddDays(1))
            .Should()
            .Be(EventStage.Finished);
    }
}
