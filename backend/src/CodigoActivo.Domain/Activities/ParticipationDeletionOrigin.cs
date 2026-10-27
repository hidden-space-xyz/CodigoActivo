namespace CodigoActivo.Domain.Activities;

/// <summary>
/// Identifies what a <see cref="ParticipationDeletion"/> removes.
/// </summary>
public enum ParticipationDeletionOrigin
{
    /// <summary>
    /// An administrator deleted the event, with its activities, signups, terms decisions and
    /// ratings.
    /// </summary>
    EventDeleted = 1,

    /// <summary>
    /// An administrator deleted an activity, with its signups.
    /// </summary>
    ActivityDeleted = 2,

    /// <summary>
    /// The person, their guardian or an administrator cancelled one signup.
    /// </summary>
    SignupCancelled = 3,

    /// <summary>
    /// The author of a rating of the event deleted it.
    /// </summary>
    RatingDeleted = 4,
}
