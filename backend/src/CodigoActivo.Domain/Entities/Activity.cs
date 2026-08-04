using CodigoActivo.Domain.Entities.Abstractions;

namespace CodigoActivo.Domain.Entities;

public class Activity : AuditableEntity
{
    public required string Title { get; set; }

    public required string Description { get; set; }

    public required string Location { get; set; }

    public DateTimeOffset ActivityStartsAt { get; set; }
    public DateTimeOffset ActivityEndsAt { get; set; }

    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;

    public Guid ActivityModalityTypeId { get; set; }
    public ActivityModalityType ActivityModalityType { get; set; } = null!;

    public Guid ThumbnailId { get; set; }
    public FileEntity Thumbnail { get; set; } = null!;

    public ICollection<ActivityUserRoleAssignment> Assignments { get; set; } = [];

    public ICollection<ActivityRoleCapacity> RoleCapacities { get; set; } = [];
}
