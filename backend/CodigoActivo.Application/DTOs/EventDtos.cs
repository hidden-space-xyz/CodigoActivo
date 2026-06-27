using System.ComponentModel.DataAnnotations;

namespace CodigoActivo.Application.DTOs;

public record EventResponse(
    Guid Id,
    string Title,
    string Subtitle,
    string Description,
    DateTimeOffset? EventStartsAt,
    DateTimeOffset? EventEndsAt,
    DateTimeOffset? SignupStartsAt,
    DateTimeOffset? SignupEndsAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    Guid CreatedBy,
    Guid? UpdatedBy,
    Guid ThumbnailId,
    bool Featured
);

public record CreateEventRequest(
    [Required, MaxLength(200)] string Title,
    [Required, MaxLength(300)] string Subtitle,
    string Description,
    DateTimeOffset? EventStartsAt,
    DateTimeOffset? EventEndsAt,
    DateTimeOffset? SignupStartsAt,
    DateTimeOffset? SignupEndsAt,
    Guid ThumbnailId
);

public record UpdateEventRequest(
    [Required, MaxLength(200)] string Title,
    [Required, MaxLength(300)] string Subtitle,
    string Description,
    DateTimeOffset? EventStartsAt,
    DateTimeOffset? EventEndsAt,
    DateTimeOffset? SignupStartsAt,
    DateTimeOffset? SignupEndsAt,
    Guid ThumbnailId
);
