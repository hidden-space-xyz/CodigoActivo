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
}
