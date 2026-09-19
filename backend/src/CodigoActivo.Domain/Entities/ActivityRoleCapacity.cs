namespace CodigoActivo.Domain.Entities;

/// <summary>
/// Represents the persisted activity role capacity domain entity and its relationships.
/// </summary>
public class ActivityRoleCapacity
{
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
    /// Gets or sets the number of desired.
    /// </summary>
    public int DesiredCount { get; set; }
}
