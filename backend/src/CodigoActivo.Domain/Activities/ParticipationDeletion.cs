using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Domain.Activities;

/// <summary>
/// Describes one removal of participation data: what is removed, who removes it and when. The
/// scope is the whole event, one of its activities, or the signup of one person to an activity.
/// </summary>
/// <param name="Origin">What removes the participation.</param>
/// <param name="EventId">Identifier of the event the participation belongs to.</param>
/// <param name="ActivityId">Identifier of the activity, unless the whole event is removed.</param>
/// <param name="PersonId">Identifier of the person whose signup is cancelled, for a cancellation.</param>
/// <param name="ActorId">Identifier of the user who removes it.</param>
/// <param name="DeletedAt">UTC timestamp of the removal.</param>
public sealed record ParticipationDeletion(
    ParticipationDeletionOrigin Origin,
    EventId EventId,
    ActivityId? ActivityId,
    UserId? PersonId,
    UserId ActorId,
    DateTimeOffset DeletedAt
)
{
    /// <summary>
    /// Describes the deletion of an event with its activities, signups and terms decisions.
    /// </summary>
    /// <param name="eventId">Identifier of the event.</param>
    /// <param name="actorId">Identifier of the user who deletes it.</param>
    /// <param name="now">Current time.</param>
    /// <returns>The description of the removal.</returns>
    public static ParticipationDeletion OfEvent(EventId eventId, UserId actorId, DateTimeOffset now)
    {
        return new ParticipationDeletion(
            ParticipationDeletionOrigin.EventDeleted,
            eventId,
            null,
            null,
            actorId,
            now
        );
    }

    /// <summary>
    /// Describes the deletion of an activity with its signups.
    /// </summary>
    /// <param name="activity">Activity being deleted.</param>
    /// <param name="actorId">Identifier of the user who deletes it.</param>
    /// <param name="now">Current time.</param>
    /// <returns>The description of the removal.</returns>
    public static ParticipationDeletion OfActivity(
        Activity activity,
        UserId actorId,
        DateTimeOffset now
    )
    {
        ArgumentNullException.ThrowIfNull(activity);

        return new ParticipationDeletion(
            ParticipationDeletionOrigin.ActivityDeleted,
            activity.EventId,
            activity.Id,
            null,
            actorId,
            now
        );
    }

    /// <summary>
    /// Describes the cancellation of the signup of one person to an activity.
    /// </summary>
    /// <param name="activity">Activity the person was signed up to.</param>
    /// <param name="personId">Identifier of the person whose signup is cancelled.</param>
    /// <param name="actorId">Identifier of the user who cancels it.</param>
    /// <param name="now">Current time.</param>
    /// <returns>The description of the removal.</returns>
    public static ParticipationDeletion OfSignup(
        Activity activity,
        UserId personId,
        UserId actorId,
        DateTimeOffset now
    )
    {
        ArgumentNullException.ThrowIfNull(activity);

        return new ParticipationDeletion(
            ParticipationDeletionOrigin.SignupCancelled,
            activity.EventId,
            activity.Id,
            personId,
            actorId,
            now
        );
    }
}
