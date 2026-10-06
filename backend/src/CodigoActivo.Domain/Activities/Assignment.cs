using CodigoActivo.Domain.Users;

namespace CodigoActivo.Domain.Activities;

/// <summary>
/// Signup of a person to an activity with a role and a status; part of the activity aggregate.
/// </summary>
public class Assignment
{
    private Assignment() { }

    internal Assignment(
        UserId userId,
        ActivityId activityId,
        ActivityRole role,
        AssignmentStatus status,
        DateTimeOffset createdAt
    )
    {
        UserId = userId;
        ActivityId = activityId;
        Role = role;
        Status = status;
        CreatedAt = createdAt;
    }

    /// <summary>
    /// Gets the identifier of the signed-up person.
    /// </summary>
    public UserId UserId { get; private set; }

    /// <summary>
    /// Gets the identifier of the activity.
    /// </summary>
    public ActivityId ActivityId { get; private set; }

    /// <summary>
    /// Gets the role asked for.
    /// </summary>
    public ActivityRole Role { get; private set; }

    /// <summary>
    /// Gets the review state of the signup.
    /// </summary>
    public AssignmentStatus Status { get; private set; }

    /// <summary>
    /// Gets when the signup was requested.
    /// </summary>
    public DateTimeOffset CreatedAt { get; private set; }

    internal void ChangeStatus(AssignmentStatus status)
    {
        Status = status;
    }
}
