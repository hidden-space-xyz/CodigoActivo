namespace CodigoActivo.Domain.Activities;

/// <summary>
/// Desired number of people for one role of an activity; part of the activity aggregate.
/// </summary>
public class ActivityRoleCapacity
{
    private ActivityRoleCapacity() { }

    internal ActivityRoleCapacity(ActivityId activityId, ActivityRole role, int desiredCount)
    {
        ActivityId = activityId;
        Role = role;
        DesiredCount = desiredCount;
    }

    /// <summary>
    /// Gets the identifier of the activity.
    /// </summary>
    public ActivityId ActivityId { get; private set; }

    /// <summary>
    /// Gets the role.
    /// </summary>
    public ActivityRole Role { get; private set; }

    /// <summary>
    /// Gets the desired number of people.
    /// </summary>
    public int DesiredCount { get; private set; }

    internal void Resize(int desiredCount)
    {
        DesiredCount = desiredCount;
    }
}
