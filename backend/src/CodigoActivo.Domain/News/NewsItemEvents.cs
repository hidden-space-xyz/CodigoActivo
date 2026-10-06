using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Files;

namespace CodigoActivo.Domain.News;

/// <summary>
/// A news item was created.
/// </summary>
/// <param name="NewsItemId">Identifier of the news item.</param>
public sealed record NewsItemCreated(NewsItemId NewsItemId) : IDomainEvent;

/// <summary>
/// The content of a news item was replaced.
/// </summary>
/// <param name="NewsItemId">Identifier of the news item.</param>
/// <param name="ReleasedFileIds">Identifiers of the files the previous content referenced and the new one does not.</param>
public sealed record NewsItemUpdated(
    NewsItemId NewsItemId,
    IReadOnlyCollection<StoredFileId> ReleasedFileIds
) : IReleasesFiles;

/// <summary>
/// A news item was deleted.
/// </summary>
/// <param name="NewsItemId">Identifier of the news item.</param>
/// <param name="ReleasedFileIds">Identifiers of the files it referenced.</param>
public sealed record NewsItemDeleted(
    NewsItemId NewsItemId,
    IReadOnlyCollection<StoredFileId> ReleasedFileIds
) : IReleasesFiles;

/// <summary>
/// A news item started or stopped being the featured one.
/// </summary>
/// <param name="NewsItemId">Identifier of the news item.</param>
/// <param name="Featured">Whether it is featured now.</param>
public sealed record NewsItemFeaturedChanged(NewsItemId NewsItemId, bool Featured) : IDomainEvent;
