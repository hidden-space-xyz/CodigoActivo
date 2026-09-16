using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Validation;

namespace CodigoActivo.Application.DTOs;

/// <summary>
/// Contains the partner data returned by the API.
/// </summary>
/// <param name="Id">Identifier of the target entity.</param>
/// <param name="Name">The name value.</param>
/// <param name="FromDate">The from date value.</param>
/// <param name="Tier">The tier value.</param>
/// <param name="Website">The website value.</param>
/// <param name="CreatedAt">UTC timestamp when the record was created.</param>
/// <param name="UpdatedAt">UTC timestamp of the most recent update.</param>
/// <param name="CreatedBy">The created by value.</param>
/// <param name="UpdatedBy">The updated by value.</param>
/// <param name="ThumbnailId">Identifier of the thumbnail.</param>
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
    /// <summary>
    /// Initializes an empty partner response for serialization.
    /// </summary>
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

/// <summary>
/// Contains the client-supplied data used to create a partner.
/// </summary>
/// <param name="Name">The name value.</param>
/// <param name="FromDate">The from date value.</param>
/// <param name="Tier">The tier value.</param>
/// <param name="Website">The website value.</param>
/// <param name="ThumbnailId">Identifier of the thumbnail.</param>
public record CreatePartnerRequest(
    [Required] [MaxLength(200)] [NotBlank] string Name,
    [Required] DateOnly? FromDate,
    [Range(0, int.MaxValue)] int Tier,
    [HttpUrl] [MaxLength(500)] string? Website,
    Guid ThumbnailId
);

/// <summary>
/// Contains the client-supplied data used to update the partner.
/// </summary>
/// <param name="Name">The name value.</param>
/// <param name="FromDate">The from date value.</param>
/// <param name="Tier">The tier value.</param>
/// <param name="Website">The website value.</param>
/// <param name="ThumbnailId">Identifier of the thumbnail.</param>
public record UpdatePartnerRequest(
    [Required] [MaxLength(200)] [NotBlank] string Name,
    [Required] DateOnly? FromDate,
    [Range(0, int.MaxValue)] int Tier,
    [HttpUrl] [MaxLength(500)] string? Website,
    Guid ThumbnailId
);
