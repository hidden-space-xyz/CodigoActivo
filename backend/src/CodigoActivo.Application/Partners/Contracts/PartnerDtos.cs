namespace CodigoActivo.Application.Partners.Contracts;

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
/// <param name="ThumbnailId">Identifier of the thumbnail.</param>
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
