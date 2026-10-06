namespace CodigoActivo.Domain.Activities;

/// <summary>
/// Review state of a person's signup to an activity.
/// </summary>
public enum AssignmentStatus
{
    /// <summary>
    /// Signup waiting for a decision.
    /// </summary>
    Requested,

    /// <summary>
    /// Signup accepted: the person takes part in the activity.
    /// </summary>
    Confirmed,

    /// <summary>
    /// Signup refused.
    /// </summary>
    Denied,
}
