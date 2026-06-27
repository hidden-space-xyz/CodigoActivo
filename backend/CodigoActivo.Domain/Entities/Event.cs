using CodigoActivo.Domain.Entities.Abstractions;

namespace CodigoActivo.Domain.Entities;

public class Event : AuditableEntity
{
    public string Title { get; set; } = null!;
    public string Subtitle { get; set; } = null!;

    public string Description { get; set; } = "{}";

    public DateTimeOffset? EventStartsAt { get; set; }
    public DateTimeOffset? EventEndsAt { get; set; }
    public DateTimeOffset? SignupStartsAt { get; set; }
    public DateTimeOffset? SignupEndsAt { get; set; }

    public bool Featured { get; set; }

    public Guid ThumbnailId { get; set; }
    public FileEntity Thumbnail { get; set; } = null!;

    public ICollection<Activity> Activities { get; set; } = [];
}
