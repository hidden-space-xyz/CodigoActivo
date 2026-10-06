using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Files;

namespace CodigoActivo.Domain.Resources;

/// <summary>
/// A resource was created.
/// </summary>
/// <param name="ResourceId">Identifier of the resource.</param>
public sealed record ResourceCreated(ResourceId ResourceId) : IDomainEvent;

/// <summary>
/// The content of a resource was replaced.
/// </summary>
/// <param name="ResourceId">Identifier of the resource.</param>
/// <param name="ReleasedFileIds">Identifiers of the files the previous content referenced and the new one does not.</param>
public sealed record ResourceUpdated(
    ResourceId ResourceId,
    IReadOnlyCollection<StoredFileId> ReleasedFileIds
) : IReleasesFiles;

/// <summary>
/// A resource was deleted.
/// </summary>
/// <param name="ResourceId">Identifier of the resource.</param>
/// <param name="ReleasedFileIds">Identifiers of the files it referenced.</param>
public sealed record ResourceDeleted(
    ResourceId ResourceId,
    IReadOnlyCollection<StoredFileId> ReleasedFileIds
) : IReleasesFiles;
