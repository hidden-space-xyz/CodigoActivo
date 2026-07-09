using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Validation;

namespace CodigoActivo.Application.DTOs;

public record ResourceResponse(
    Guid Id,
    string Title,
    string Subtitle,
    string Description,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    Guid CreatedBy,
    Guid? UpdatedBy,
    Guid ThumbnailId
)
{
    public ResourceResponse()
        : this(
            Guid.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            default,
            null,
            Guid.Empty,
            null,
            Guid.Empty
        ) { }
}

public record ResourceListItemResponse(
    Guid Id,
    string Title,
    string Subtitle,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    Guid CreatedBy,
    Guid? UpdatedBy,
    Guid ThumbnailId
)
{
    public ResourceListItemResponse()
        : this(Guid.Empty, string.Empty, string.Empty, default, null, Guid.Empty, null, Guid.Empty)
    { }
}

public record CreateResourceRequest(
    [Required] [MaxLength(200)] [NotBlank] string Title,
    [Required] [MaxLength(300)] [NotBlank] string Subtitle,
    [JsonString] string Description,
    Guid ThumbnailId
);

public record UpdateResourceRequest(
    [Required] [MaxLength(200)] [NotBlank] string Title,
    [Required] [MaxLength(300)] [NotBlank] string Subtitle,
    [JsonString] string Description,
    Guid ThumbnailId
);
