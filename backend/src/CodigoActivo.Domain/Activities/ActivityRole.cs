namespace CodigoActivo.Domain.Activities;

/// <summary>
/// Role a person takes in an activity, declared in display order.
/// </summary>
public enum ActivityRole
{
    /// <summary>
    /// Coordinates the activity and its team.
    /// </summary>
    Leader,

    /// <summary>
    /// Helps run the activity.
    /// </summary>
    Volunteer,

    /// <summary>
    /// Attends the activity.
    /// </summary>
    Participant,
}
