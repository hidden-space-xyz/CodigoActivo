using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Activities;

/// <summary>
/// When an activity takes place: it ends after it starts, and both days, in the local time zone,
/// fall within the days of its event.
/// </summary>
public sealed record ActivitySchedule
{
    private ActivitySchedule(DateTimeOffset startsAt, DateTimeOffset endsAt)
    {
        StartsAt = startsAt;
        EndsAt = endsAt;
    }

    /// <summary>
    /// Gets when the activity starts, in UTC.
    /// </summary>
    public DateTimeOffset StartsAt { get; }

    /// <summary>
    /// Gets when the activity ends, in UTC.
    /// </summary>
    public DateTimeOffset EndsAt { get; }

    /// <summary>
    /// Restores a schedule that was checked before it was stored.
    /// </summary>
    /// <param name="startsAt">Stored start.</param>
    /// <param name="endsAt">Stored end.</param>
    /// <returns>The schedule.</returns>
    public static ActivitySchedule FromStored(DateTimeOffset startsAt, DateTimeOffset endsAt)
    {
        return new ActivitySchedule(startsAt, endsAt);
    }

    /// <summary>
    /// Builds a schedule from the times supplied.
    /// </summary>
    /// <param name="startsAt">When the activity starts.</param>
    /// <param name="endsAt">When the activity ends.</param>
    /// <param name="eventCalendar">Days of the event, which the activity must lie within.</param>
    /// <param name="zone">Time zone the days of the event are counted in.</param>
    /// <returns>The schedule in UTC, or a validation error when it does not fit the event.</returns>
    public static Result<ActivitySchedule> Create(
        DateTimeOffset? startsAt,
        DateTimeOffset? endsAt,
        DateRange eventCalendar,
        TimeZoneInfo zone
    )
    {
        ArgumentNullException.ThrowIfNull(eventCalendar);
        ArgumentNullException.ThrowIfNull(zone);

        if (startsAt is not { } start || endsAt is not { } end)
        {
            return Error.Validation(DomainErrorCode.ActivityScheduleRequired);
        }

        if (end <= start)
        {
            return Error.Validation(DomainErrorCode.ActivityScheduleInvalidRange);
        }

        return eventCalendar.Covers(start, end, zone)
            ? new ActivitySchedule(start.ToUniversalTime(), end.ToUniversalTime())
            : Error.Validation(DomainErrorCode.ActivityScheduleOutsideEventRange);
    }
}
