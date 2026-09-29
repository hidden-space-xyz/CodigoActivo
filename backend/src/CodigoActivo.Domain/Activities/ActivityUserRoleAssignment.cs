namespace CodigoActivo.Domain.Activities;

/// <summary>
/// Signup of a person to an activity with a role and a status; part of the activity aggregate.
/// </summary>
public class ActivityUserRoleAssignment
{
    private ActivityUserRoleAssignment() { }

    internal ActivityUserRoleAssignment(
        Guid userId,
        Guid activityId,
        Guid activityRoleTypeId,
        Guid assignmentStatusId,
        DateTimeOffset createdAt
    )
    {
        UserId = userId;
        ActivityId = activityId;
        ActivityRoleTypeId = activityRoleTypeId;
        AssignmentStatusId = assignmentStatusId;
        CreatedAt = createdAt;
    }

    /// <summary>
    /// Gets the identifier of the signed-up person.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// Gets the identifier of the activity.
    /// </summary>
    public Guid ActivityId { get; private set; }

    /// <summary>
    /// Gets the identifier of the role.
    /// </summary>
    public Guid ActivityRoleTypeId { get; private set; }

    /// <summary>
    /// Gets the identifier of the status.
    /// </summary>
    public Guid AssignmentStatusId { get; private set; }

    /// <summary>
    /// Gets when the signup was requested.
    /// </summary>
    public DateTimeOffset CreatedAt { get; private set; }

    internal void ChangeStatus(Guid statusId)
    {
        AssignmentStatusId = statusId;
    }
}
