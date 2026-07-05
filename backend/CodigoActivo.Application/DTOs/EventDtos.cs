using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Validation;

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
    bool Featured,
    IReadOnlyList<EventCategoryResponse> Categories
)
{
    public EventResponse()
        : this(
            Guid.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            default,
            default,
            default,
            default,
            default,
            null,
            Guid.Empty,
            null,
            Guid.Empty,
            false,
            []
        )
    {
    }
}

/// <summary>List-read shape: the full <see cref="EventResponse"/> minus the heavy Description.</summary>
public record EventListItemResponse(
    Guid Id,
    string Title,
    string Subtitle,
    DateOnly EventStartsAt,
    DateOnly EventEndsAt,
    DateTimeOffset SignupStartsAt,
    DateTimeOffset SignupEndsAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    Guid CreatedBy,
    Guid? UpdatedBy,
    Guid ThumbnailId,
    bool Featured,
    IReadOnlyList<EventCategoryResponse> Categories
)
{
    public EventListItemResponse()
        : this(
            Guid.Empty,
            string.Empty,
            string.Empty,
            default,
            default,
            default,
            default,
            default,
            null,
            Guid.Empty,
            null,
            Guid.Empty,
            false,
            []
        )
    {
    }
}

public record EventCategoryResponse(Guid CategoryTypeId, string Name, string Color);

public record CreateEventRequest(
    [Required] [MaxLength(200)] [NotBlank] string Title,
    [Required] [MaxLength(300)] [NotBlank] string Subtitle,
    [JsonString] string Description,
    [Required] DateOnly? EventStartsAt,
    [Required] DateOnly? EventEndsAt,
    [Required] DateTimeOffset? SignupStartsAt,
    [Required] DateTimeOffset? SignupEndsAt,
    Guid ThumbnailId,
    IReadOnlyList<Guid>? CategoryTypeIds
);

public record UpdateEventRequest(
    [Required] [MaxLength(200)] [NotBlank] string Title,
    [Required] [MaxLength(300)] [NotBlank] string Subtitle,
    [JsonString] string Description,
    [Required] DateOnly? EventStartsAt,
    [Required] DateOnly? EventEndsAt,
    [Required] DateTimeOffset? SignupStartsAt,
    [Required] DateTimeOffset? SignupEndsAt,
    Guid ThumbnailId,
    IReadOnlyList<Guid>? CategoryTypeIds
);

public record EventCategoryTypeResponse(Guid Id, string Name, string Color)
{
    public EventCategoryTypeResponse()
        : this(Guid.Empty, string.Empty, string.Empty)
    {
    }
}

public record CreateEventCategoryTypeRequest(
    [Required] [MaxLength(120)] [NotBlank] string Name,
    [Required] [MaxLength(9)] [RegularExpression("^#[0-9A-Fa-f]{6}$")] string Color
);

public record UpdateEventCategoryTypeRequest(
    [Required] [MaxLength(120)] [NotBlank] string Name,
    [Required] [MaxLength(9)] [RegularExpression("^#[0-9A-Fa-f]{6}$")] string Color
);