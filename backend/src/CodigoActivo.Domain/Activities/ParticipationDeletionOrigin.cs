namespace CodigoActivo.Domain.Activities;

/// <summary>
/// Identifies what removed the participation a <see cref="DeletedParticipation"/> keeps.
/// </summary>
public enum ParticipationDeletionOrigin
{
    /// <summary>
    /// An administrator deleted the event, with its activities, signups and terms decisions.
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
