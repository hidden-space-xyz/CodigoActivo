using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Validation;

namespace CodigoActivo.Application.DTOs;

public record AnnouncementResponse(
    Guid Id,
    string Title,
    string Subtitle,
    string Description,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    Guid CreatedBy,
    Guid? UpdatedBy,
    Guid ThumbnailId,
    bool Featured
)
{
    public AnnouncementResponse()
        : this(
            Guid.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            default,
            null,
            Guid.Empty,
            null,
            Guid.Empty,
            false
        ) { }
}

public record AnnouncementListItemResponse(
    Guid Id,
    string Title,
    string Subtitle,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    Guid CreatedBy,
    Guid? UpdatedBy,
    Guid ThumbnailId,
    bool Featured
)
{
    public AnnouncementListItemResponse()
        : this(
            Guid.Empty,
            string.Empty,
            string.Empty,
            default,
            null,
            Guid.Empty,
            null,
            Guid.Empty,
            false
        ) { }
}

public record CreateAnnouncementRequest(
    [Required] [MaxLength(200)] [NotBlank] string Title,
    [Required] [MaxLength(300)] [NotBlank] string Subtitle,
    [JsonString] string Description,
    Guid ThumbnailId
);

public record UpdateAnnouncementRequest(
    [Required] [MaxLength(200)] [NotBlank] string Title,
    [Required] [MaxLength(300)] [NotBlank] string Subtitle,
    [JsonString] string Description,
    Guid ThumbnailId
);
