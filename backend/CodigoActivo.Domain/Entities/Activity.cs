using CodigoActivo.Domain.Entities.Abstractions;

namespace CodigoActivo.Domain.Entities;

public class Activity : AuditableEntity
{
    public string Title { get; set; } = null!;

    public string Description { get; set; } = null!;

    public DateTimeOffset? ActivityStartsAt { get; set; }
    public DateTimeOffset? ActivityEndsAt { get; set; }

    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;

    public Guid ThumbnailId { get; set; }
    public FileEntity Thumbnail { get; set; } = null!;

    public ICollection<ActivityAllowedRoleType> AllowedRoleTypes { get; set; } = [];
    public ICollection<ActivityUserRoleAssignment> Assignments { get; set; } = [];
}
