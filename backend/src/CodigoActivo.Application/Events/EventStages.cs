using CodigoActivo.Application.Events.Contracts;
using CodigoActivo.Domain.Events;

namespace CodigoActivo.Application.Events;

/// <summary>
/// Stamps event responses with the stage <see cref="EventTimeline"/> gives them at a moment, once
/// they are materialized, so every reader of the API sees the stage the server clock decides.
/// </summary>
public static class EventStages
{
    /// <summary>
    /// Returns the event with its stage at the moment supplied.
    /// </summary>
    /// <param name="response">Event read from the database.</param>
    /// <param name="now">Moment to evaluate.</param>
    /// <param name="today">Current day in the configured time zone.</param>
    /// <returns>The same event with <see cref="EventResponse.Stage"/> set.</returns>
    public static EventResponse Stamp(EventResponse response, DateTimeOffset now, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(response);

        return response with
        {
            Stage = EventTimeline.StageAt(
                response.EventEndsAt,
                response.EarlySignupStartsAt,
                response.SignupStartsAt,
                response.SignupEndsAt,
                now,
                today
            ),
        };
    }

    /// <summary>
    /// Returns the list item with its stage at the moment supplied.
    /// </summary>
    /// <param name="item">List item read from the database.</param>
    /// <param name="now">Moment to evaluate.</param>
    /// <param name="today">Current day in the configured time zone.</param>
    /// <returns>The same list item with <see cref="EventListItemResponse.Stage"/> set.</returns>
    public static EventListItemResponse Stamp(
        EventListItemResponse item,
        DateTimeOffset now,
        DateOnly today
    )
    {
        ArgumentNullException.ThrowIfNull(item);

        return item with
        {
            Stage = EventTimeline.StageAt(
                item.EventEndsAt,
                item.EarlySignupStartsAt,
                item.SignupStartsAt,
                item.SignupEndsAt,
                now,
                today
            ),
        };
    }
}
