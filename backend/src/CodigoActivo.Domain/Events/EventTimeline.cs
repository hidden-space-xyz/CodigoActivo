namespace CodigoActivo.Domain.Events;

/// <summary>
/// Reads the days and the signup window of an event at a given moment. The aggregate and the read
/// side both answer through it, so the phase that gates signups and the stage people see agree.
/// </summary>
public static class EventTimeline
{
    /// <summary>
    /// Tells who may sign up at a given moment.
    /// </summary>
    /// <param name="earlySignupStartsAt">When the early signup opens, if there is one.</param>
    /// <param name="signupStartsAt">When the signup opens.</param>
    /// <param name="signupEndsAt">When the signup closes.</param>
    /// <param name="now">Moment to evaluate.</param>
    /// <returns>The signup phase at that moment.</returns>
    public static SignupPhase SignupPhaseAt(
        DateTimeOffset? earlySignupStartsAt,
        DateTimeOffset signupStartsAt,
        DateTimeOffset signupEndsAt,
        DateTimeOffset now
    )
    {
        if (now > signupEndsAt)
        {
            return SignupPhase.Closed;
        }

        if (now >= signupStartsAt)
        {
            return SignupPhase.Open;
        }

        return earlySignupStartsAt is { } earlyStart && now >= earlyStart
            ? SignupPhase.EarlyOnly
            : SignupPhase.Closed;
    }

    /// <summary>
    /// Tells where the event stands at a given moment: finished once its last day has passed,
    /// otherwise the state of its signup, telling a signup still to open from a closed one.
    /// </summary>
    /// <param name="eventEndsAt">Last day of the event.</param>
    /// <param name="earlySignupStartsAt">When the early signup opens, if there is one.</param>
    /// <param name="signupStartsAt">When the signup opens.</param>
    /// <param name="signupEndsAt">When the signup closes.</param>
    /// <param name="now">Moment to evaluate.</param>
    /// <param name="today">Current day in the configured time zone.</param>
    /// <returns>The stage of the event at that moment.</returns>
    public static EventStage StageAt(
        DateOnly eventEndsAt,
        DateTimeOffset? earlySignupStartsAt,
        DateTimeOffset signupStartsAt,
        DateTimeOffset signupEndsAt,
        DateTimeOffset now,
        DateOnly today
    )
    {
        if (eventEndsAt < today)
        {
            return EventStage.Finished;
        }

        return SignupPhaseAt(earlySignupStartsAt, signupStartsAt, signupEndsAt, now) switch
        {
            SignupPhase.Open => EventStage.SignupOpen,
            SignupPhase.EarlyOnly => EventStage.EarlySignupOpen,
            _ when now > signupEndsAt => EventStage.SignupClosed,
            _ => EventStage.Upcoming,
        };
    }
}
