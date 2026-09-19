namespace CodigoActivo.Domain.Entities;

/// <summary>
/// Represents the persisted activity user role assignment domain entity and its relationships.
/// </summary>
public class ActivityUserRoleAssignment
{
    /// <summary>
    /// Gets or sets the identifier of the associated user.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the associated user.
    /// </summary>
    public User User { get; set; } = null!;

    /// <summary>
    /// Gets or sets the identifier of the associated activity.
    /// </summary>
    public Guid ActivityId { get; set; }

    /// <summary>
    /// Gets or sets the associated activity.
    /// </summary>
    public Activity Activity { get; set; } = null!;

    /// <summary>
    /// Gets or sets the identifier of the associated activity role type.
    /// </summary>
    public Guid ActivityRoleTypeId { get; set; }

    /// <summary>
    /// Gets or sets the associated activity role type.
    /// </summary>
    public ActivityRoleType ActivityRoleType { get; set; } = null!;

    /// <summary>
    /// Gets or sets the identifier of the associated assignment status.
    /// </summary>
    public Guid AssignmentStatusId { get; set; }

    /// <summary>
    /// Gets or sets the assignment status value.
    /// </summary>
    public AssignmentStatusType AssignmentStatus { get; set; } = null!;

    /// <summary>
    /// Gets or sets the UTC timestamp when the entity was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
}
