using System.ComponentModel.DataAnnotations;

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
);

public record CreatePartnerRequest(
    [Required, MaxLength(200)] string Name,
    DateOnly FromDate,
    [Range(0, int.MaxValue)] int Tier,
    [Url, MaxLength(500)] string? Website,
    Guid ThumbnailId
);

public record UpdatePartnerRequest(
    [Required, MaxLength(200)] string Name,
    DateOnly FromDate,
    [Range(0, int.MaxValue)] int Tier,
    [Url, MaxLength(500)] string? Website,
    Guid ThumbnailId
);
