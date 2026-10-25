using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Domain.Activities;

/// <summary>
/// Describes what one removal of participation data takes away: the whole event, one of its
/// activities, the signup of one person to an activity, or the rating one account gave the event.
/// </summary>
/// <param name="Origin">What removes the participation.</param>
/// <param name="EventId">Identifier of the event the participation belongs to.</param>
/// <param name="ActivityId">Identifier of the activity, for an activity or a signup.</param>
/// <param name="PersonId">
/// Identifier of the person whose signup is cancelled, or of the author of the deleted rating.
/// </param>
public sealed record ParticipationDeletion(
    ParticipationDeletionOrigin Origin,
    EventId EventId,
    ActivityId? ActivityId,
    UserId? PersonId
)
{
    /// <summary>
    /// Describes the deletion of an event with its activities, signups, terms decisions and
    /// ratings.
    /// </summary>
    /// <param name="eventId">Identifier of the event.</param>
    /// <returns>The description of the removal.</returns>
    public static ParticipationDeletion OfEvent(EventId eventId)
    {
        return new ParticipationDeletion(
            ParticipationDeletionOrigin.EventDeleted,
            eventId,
            null,
            null
        );
    }

    /// <summary>
    /// Describes the deletion of an activity with its signups.
    /// </summary>
    /// <param name="activity">Activity being deleted.</param>
    /// <returns>The description of the removal.</returns>
    public static ParticipationDeletion OfActivity(Activity activity)
    {
        ArgumentNullException.ThrowIfNull(activity);

        return new ParticipationDeletion(
            ParticipationDeletionOrigin.ActivityDeleted,
            activity.EventId,
            activity.Id,
            null
        );
    }

    /// <summary>
    /// Describes the cancellation of the signup of one person to an activity.
    /// </summary>
    /// <param name="activity">Activity the person was signed up to.</param>
    /// <param name="personId">Identifier of the person whose signup is cancelled.</param>
    /// <returns>The description of the removal.</returns>
    public static ParticipationDeletion OfSignup(Activity activity, UserId personId)
    {
        ArgumentNullException.ThrowIfNull(activity);

        return new ParticipationDeletion(
            ParticipationDeletionOrigin.SignupCancelled,
            activity.EventId,
            activity.Id,
            personId
        );
    }

    /// <summary>
    /// Describes the deletion of a rating by its author.
    /// </summary>
    /// <param name="rating">Rating being deleted.</param>
    /// <returns>The description of the removal.</returns>
    public static ParticipationDeletion OfRating(EventRating rating)
    {
        ArgumentNullException.ThrowIfNull(rating);

        return new ParticipationDeletion(
            ParticipationDeletionOrigin.RatingDeleted,
            rating.EventId,
            null,
            rating.UserId
        );
    }
}
