namespace CodigoActivo.Domain.Common;

/// <summary>
/// Abstracts the current time. <see cref="Today"/> is the current date in the application's
/// configured timezone (not UTC), so date-boundary reads — e.g. classifying an event as
/// upcoming vs past by its end date — match the date users actually see.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }

    DateOnly Today { get; }

    /// <summary>
    /// The application's configured timezone. Use it to map an instant to the calendar day users
    /// actually see (e.g. classifying an activity's start against an event's local date range),
    /// instead of taking the UTC day which drifts across midnight.
    /// </summary>
    TimeZoneInfo TimeZone { get; }
}
