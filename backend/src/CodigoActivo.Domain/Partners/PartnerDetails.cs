namespace CodigoActivo.Domain.Partners;

/// <summary>
/// Profile of a partner as it is created or replaced.
/// </summary>
/// <param name="Name">Human-readable name; surrounding spaces are removed.</param>
/// <param name="FromDate">Date the partnership started.</param>
/// <param name="Tier">Sponsorship tier.</param>
/// <param name="Web">Website; blank means none.</param>
/// <param name="ThumbnailId">Identifier of the logo file.</param>
public sealed record PartnerDetails(
    string Name,
    DateOnly FromDate,
    int Tier,
    string? Web,
    Guid ThumbnailId
);
