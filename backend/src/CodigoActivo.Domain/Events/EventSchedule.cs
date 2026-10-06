using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Events;

/// <summary>
/// Days an event runs and the window in which people sign up to its activities. The signup
/// closes after it opens, an early signup opens before the regular one, and the signup closes no
/// later than the last day of the event.
/// </summary>
public sealed record EventSchedule
{
    private EventSchedule(DateRange calendar, SignupWindow signupWindow)
    {
        Calendar = calendar;
        SignupWindow = signupWindow;
    }

    /// <summary>
    /// Gets the days the event runs.
    /// </summary>
    public DateRange Calendar { get; }

    /// <summary>
    /// Gets when people may sign up, in UTC.
    /// </summary>
    public SignupWindow SignupWindow { get; }

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
            return Error.Validation(DomainErrorCode.EventScheduleRequired);
        }

        if (
            DateRange.TryCreate(eventStart, eventEnd) is not { } calendar
            || signupEnd <= signupStart
        )
        {
            return Error.Validation(DomainErrorCode.EventScheduleInvalidRange);
        }

        if (SignupWindow.TryCreate(earlySignupStartsAt, signupStart, signupEnd) is not { } window)
        {
            return Error.Validation(DomainErrorCode.EventEarlySignupNotBeforeSignup);
        }

        if (calendar.EndsBefore(DateOnly.FromDateTime(signupStart.UtcDateTime)))
        {
            return Error.Validation(DomainErrorCode.EventScheduleInvalidRange);
        }

        if (calendar.EndsBefore(DateOnly.FromDateTime(signupEnd.UtcDateTime)))
        {
            return Error.Validation(DomainErrorCode.EventSignupEndsAfterEvent);
        }

        return new EventSchedule(calendar, window);
    }
}
