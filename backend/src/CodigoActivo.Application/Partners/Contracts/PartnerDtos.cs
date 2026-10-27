namespace CodigoActivo.Application.Partners.Contracts;

/// <summary>
/// Contains the partner data returned by the API.
/// </summary>
/// <param name="Id">Identifier of the partner.</param>
/// <param name="Name">Name of the partner.</param>
/// <param name="FromDate">Day the collaboration started.</param>
/// <param name="Tier">Sponsorship tier, zero or more.</param>
/// <param name="Website">Website, or <see langword="null"/> when there is none.</param>
/// <param name="CreatedAt">UTC timestamp when the record was created.</param>
/// <param name="UpdatedAt">UTC timestamp of the most recent update.</param>
/// <param name="ThumbnailId">Identifier of the logo file.</param>
public record PartnerResponse(
    Guid Id,
    string Name,
    DateOnly FromDate,
    int Tier,
    string? Website,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    Guid ThumbnailId
)
{
    /// <summary>
    /// Initializes an empty partner response for serialization.
    /// </summary>
    public PartnerResponse()
        : this(Guid.Empty, string.Empty, default, default, null, default, null, Guid.Empty) { }
}
