namespace CodigoActivo.Domain.Activities;

/// <summary>
/// Desired number of people for one role of an activity; part of the activity aggregate.
/// </summary>
public class ActivityRoleCapacity
{
    private ActivityRoleCapacity() { }

    internal ActivityRoleCapacity(Guid activityId, Guid activityRoleTypeId, int desiredCount)
    {
        ActivityId = activityId;
        ActivityRoleTypeId = activityRoleTypeId;
        DesiredCount = desiredCount;
    }

    /// <summary>
    /// Gets the identifier of the activity.
    /// </summary>
    public Guid ActivityId { get; private set; }

    /// <summary>
    /// Gets the identifier of the role.
    /// </summary>
    public Guid ActivityRoleTypeId { get; private set; }

    /// <summary>
    /// Gets the desired number of people.
    /// </summary>
    public int DesiredCount { get; private set; }

    internal void Resize(int desiredCount)
    {
        DesiredCount = desiredCount;
    }
}
