using System.ComponentModel.DataAnnotations;

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
        : this(default, "", "", "", default, null, default, null, default) { }
}

public record CreateResourceRequest(
    [Required, MaxLength(200)] string Title,
    [Required, MaxLength(300)] string Subtitle,
    string Description,
    Guid ThumbnailId
);

public record UpdateResourceRequest(
    [Required, MaxLength(200)] string Title,
    [Required, MaxLength(300)] string Subtitle,
    string Description,
    Guid ThumbnailId
);
