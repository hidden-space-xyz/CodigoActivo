using CodigoActivo.Domain.Entities;

namespace CodigoActivo.UnitTests.Application.Seo;

internal static class SeoTestData
{
    public const string BaseUrl = "https://codigoactivo.test";

    public static Event NewEvent(DateTimeOffset createdAt, DateTimeOffset? updatedAt = null)
    {
        return new()
        {
            Id = Guid.NewGuid(),
            Title = "Evento",
            Subtitle = "Sub",
            Description = "{}",
            EventStartsAt = new DateOnly(2026, 8, 1),
            EventEndsAt = new DateOnly(2026, 8, 2),
            SignupStartsAt = createdAt,
            SignupEndsAt = createdAt.AddDays(30),
            ThumbnailId = Guid.NewGuid(),
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            CreatedBy = Guid.NewGuid(),
        };
    }

    public static Announcement NewAnnouncement(DateTimeOffset createdAt)
    {
        return new()
        {
            Id = Guid.NewGuid(),
            Title = "Anuncio",
            Subtitle = "Sub",
            Description = "{}",
            ThumbnailId = Guid.NewGuid(),
            CreatedAt = createdAt,
            CreatedBy = Guid.NewGuid(),
        };
    }

    public static Resource NewResource(string? url)
    {
        return new()
        {
            Id = Guid.NewGuid(),
            Title = "Recurso",
            Subtitle = "Sub",
            Description = "{}",
            Url = url,
            ResourceTypeId = Guid.NewGuid(),
            ThumbnailId = Guid.NewGuid(),
            CreatedAt = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
            CreatedBy = Guid.NewGuid(),
        };
    }
}
