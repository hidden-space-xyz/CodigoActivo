using System.ComponentModel.DataAnnotations;

namespace CodigoActivo.Application.DTOs;

public record EventResponse(
    Guid Id,
    string Title,
    string Subtitle,
    string Description,
    DateOnly EventStartsAt,
    DateOnly EventEndsAt,
    DateTimeOffset SignupStartsAt,
    DateTimeOffset SignupEndsAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    Guid CreatedBy,
    Guid? UpdatedBy,
    Guid ThumbnailId,
    bool Featured
)
{
    public EventResponse()
        : this(
            default,
            "",
            "",
            "",
            default,
            default,
            default,
            default,
            default,
            null,
            default,
            null,
            default,
            false
        ) { }
}

public record CreateEventRequest(
    [Required, MaxLength(200)] string Title,
    [Required, MaxLength(300)] string Subtitle,
    string Description,
    [Required] DateOnly? EventStartsAt,
    [Required] DateOnly? EventEndsAt,
    [Required] DateTimeOffset? SignupStartsAt,
    [Required] DateTimeOffset? SignupEndsAt,
    Guid ThumbnailId
);

public record UpdateEventRequest(
    [Required, MaxLength(200)] string Title,
    [Required, MaxLength(300)] string Subtitle,
    string Description,
    [Required] DateOnly? EventStartsAt,
    [Required] DateOnly? EventEndsAt,
    [Required] DateTimeOffset? SignupStartsAt,
    [Required] DateTimeOffset? SignupEndsAt,
    Guid ThumbnailId
);
