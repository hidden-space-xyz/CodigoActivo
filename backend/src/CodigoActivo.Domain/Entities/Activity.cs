using CodigoActivo.Domain.Entities.Abstractions;

namespace CodigoActivo.Domain.Entities;

/// <summary>
/// Represents the persisted activity domain entity and its relationships.
/// </summary>
public class Activity : AuditableEntity
{
    /// <summary>
    /// Gets or sets the title displayed to users.
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Gets or sets the detailed description.
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    /// Gets or sets the location value.
    /// </summary>
    public required string Location { get; set; }

    /// <summary>
    /// Gets or sets the date and time when the activity starts.
    /// </summary>
    public DateTimeOffset ActivityStartsAt { get; set; }
    /// <summary>
    /// Gets or sets the date and time when the activity ends.
    /// </summary>
    public DateTimeOffset ActivityEndsAt { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the associated event.
    /// </summary>
    public Guid EventId { get; set; }
    /// <summary>
    /// Gets or sets the associated event.
    /// </summary>
    public Event Event { get; set; } = null!;

    /// <summary>
    /// Gets or sets the identifier of the associated activity modality type.
    /// </summary>
    public Guid ActivityModalityTypeId { get; set; }
    /// <summary>
    /// Gets or sets the associated activity modality type.
    /// </summary>
    public ActivityModalityType ActivityModalityType { get; set; } = null!;

    /// <summary>
    /// Gets or sets the identifier of the associated thumbnail.
    /// </summary>
    public Guid ThumbnailId { get; set; }
    /// <summary>
    /// Gets or sets the associated thumbnail.
    /// </summary>
    public FileEntity Thumbnail { get; set; } = null!;

    /// <summary>
    /// Gets or sets the related assignments collection.
    /// </summary>
    public ICollection<ActivityUserRoleAssignment> Assignments { get; set; } = [];

    /// <summary>
    /// Gets or sets the related role capacities collection.
    /// </summary>
    public ICollection<ActivityRoleCapacity> RoleCapacities { get; set; } = [];
}
