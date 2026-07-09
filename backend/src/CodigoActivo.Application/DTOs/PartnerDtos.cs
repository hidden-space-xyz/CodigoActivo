using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Validation;

namespace CodigoActivo.Application.DTOs;

public record PartnerResponse(
    Guid Id,
    string Name,
    DateOnly FromDate,
    int Tier,
    string? Website,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    Guid CreatedBy,
    Guid? UpdatedBy,
    Guid ThumbnailId
)
{
    public PartnerResponse()
        : this(
            Guid.Empty,
            string.Empty,
            default,
            default,
            null,
            default,
            null,
            Guid.Empty,
            null,
            Guid.Empty
        ) { }
}

public record CreatePartnerRequest(
    [Required] [MaxLength(200)] [NotBlank] string Name,
    [Required] DateOnly? FromDate,
    [Range(0, int.MaxValue)] int Tier,
    [Url] [MaxLength(500)] string? Website,
    Guid ThumbnailId
);

public record UpdatePartnerRequest(
    [Required] [MaxLength(200)] [NotBlank] string Name,
    [Required] DateOnly? FromDate,
    [Range(0, int.MaxValue)] int Tier,
    [Url] [MaxLength(500)] string? Website,
    Guid ThumbnailId
);
