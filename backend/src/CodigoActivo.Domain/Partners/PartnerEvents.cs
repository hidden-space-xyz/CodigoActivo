using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Files;

namespace CodigoActivo.Domain.Partners;

/// <summary>
/// A partner was created.
/// </summary>
/// <param name="PartnerId">Identifier of the partner.</param>
public sealed record PartnerCreated(PartnerId PartnerId) : IDomainEvent;

/// <summary>
/// The profile of a partner was replaced.
/// </summary>
/// <param name="PartnerId">Identifier of the partner.</param>
/// <param name="PreviousThumbnailId">Identifier of the logo file it showed before.</param>
/// <param name="ThumbnailId">Identifier of the logo file it shows now.</param>
public sealed record PartnerUpdated(
    PartnerId PartnerId,
    StoredFileId PreviousThumbnailId,
    StoredFileId ThumbnailId
) : IReleasesFiles
{
    /// <inheritdoc />
    public IReadOnlyCollection<StoredFileId> ReleasedFileIds =>
        PreviousThumbnailId == ThumbnailId ? [] : [PreviousThumbnailId];
}

/// <summary>
/// A partner was deleted.
/// </summary>
/// <param name="PartnerId">Identifier of the partner.</param>
/// <param name="ThumbnailId">Identifier of the logo file it showed.</param>
public sealed record PartnerDeleted(PartnerId PartnerId, StoredFileId ThumbnailId) : IReleasesFiles
{
    /// <inheritdoc />
    public IReadOnlyCollection<StoredFileId> ReleasedFileIds => [ThumbnailId];
}
