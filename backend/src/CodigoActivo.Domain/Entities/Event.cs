using CodigoActivo.Domain.Entities.Abstractions;

namespace CodigoActivo.Domain.Entities;

public class Event : AuditableEntity, IFeaturable
{
    public required string Title { get; set; }
    public required string Subtitle { get; set; }

    public string Description { get; set; } = "{}";

    public DateOnly EventStartsAt { get; set; }
    public DateOnly EventEndsAt { get; set; }
    public DateTimeOffset? EarlySignupStartsAt { get; set; }
    public DateTimeOffset SignupStartsAt { get; set; }
    public DateTimeOffset SignupEndsAt { get; set; }

    public bool Featured { get; set; }

    public Guid ThumbnailId { get; set; }
    public FileEntity Thumbnail { get; set; } = null!;

    public ICollection<Activity> Activities { get; set; } = [];
    public ICollection<EventCategory> Categories { get; set; } = [];
    public ICollection<EventRating> Ratings { get; set; } = [];
}
