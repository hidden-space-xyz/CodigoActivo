using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Events;

/// <summary>
/// Days an event runs and the window in which people sign up to its activities. The signup
/// closes after it opens, an early signup opens before the regular one, and the regular signup
/// opens no later than the last day of the event.
/// </summary>
public sealed record EventSchedule
{
    private EventSchedule(
        DateOnly eventStartsAt,
        DateOnly eventEndsAt,
        DateTimeOffset? earlySignupStartsAt,
        DateTimeOffset signupStartsAt,
        DateTimeOffset signupEndsAt
    )
    {
        EventStartsAt = eventStartsAt;
        EventEndsAt = eventEndsAt;
        EarlySignupStartsAt = earlySignupStartsAt;
        SignupStartsAt = signupStartsAt;
        SignupEndsAt = signupEndsAt;
    }

    /// <summary>
    /// Gets the first day of the event.
    /// </summary>
    public DateOnly EventStartsAt { get; }

    /// <summary>
    /// Gets the last day of the event.
    /// </summary>
    public DateOnly EventEndsAt { get; }

    /// <summary>
    /// Gets when the early signup opens, in UTC, if there is one.
    /// </summary>
    public DateTimeOffset? EarlySignupStartsAt { get; }

    /// <summary>
    /// Gets when the signup opens, in UTC.
    /// </summary>
    public DateTimeOffset SignupStartsAt { get; }

    /// <summary>
    /// Gets when the signup closes, in UTC.
    /// </summary>
    public DateTimeOffset SignupEndsAt { get; }

    /// <summary>
    /// Builds a schedule from the dates supplied.
    /// </summary>
    /// <param name="eventStartsAt">First day of the event.</param>
    /// <param name="eventEndsAt">Last day of the event.</param>
    /// <param name="earlySignupStartsAt">When the early signup opens, if there is one.</param>
    /// <param name="signupStartsAt">When the signup opens.</param>
    /// <param name="signupEndsAt">When the signup closes.</param>
    /// <returns>The schedule in UTC, or a validation error when the dates do not fit together.</returns>
    public static Result<EventSchedule> Create(
        DateOnly? eventStartsAt,
        DateOnly? eventEndsAt,
        DateTimeOffset? earlySignupStartsAt,
        DateTimeOffset? signupStartsAt,
        DateTimeOffset? signupEndsAt
    )
    {
        if (
            eventStartsAt is not { } eventStart
            || eventEndsAt is not { } eventEnd
            || signupStartsAt is not { } signupStart
            || signupEndsAt is not { } signupEnd
        )
        {
            return Error.Validation(ErrorCode.EventScheduleRequired);
        }

        if (eventEnd < eventStart || signupEnd <= signupStart)
        {
            return Error.Validation(ErrorCode.EventScheduleInvalidRange);
        }

        if (earlySignupStartsAt is { } earlyStart && earlyStart >= signupStart)
        {
            return Error.Validation(ErrorCode.EventEarlySignupNotBeforeSignup);
        }

        if (DateOnly.FromDateTime(signupStart.UtcDateTime) > eventEnd)
        {
            return Error.Validation(ErrorCode.EventScheduleInvalidRange);
        }

        return new EventSchedule(
            eventStart,
            eventEnd,
            earlySignupStartsAt?.ToUniversalTime(),
            signupStart.ToUniversalTime(),
            signupEnd.ToUniversalTime()
        );
    }
}
