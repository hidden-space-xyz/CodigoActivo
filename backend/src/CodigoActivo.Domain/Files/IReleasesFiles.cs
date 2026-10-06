using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Files;

/// <summary>
/// Domain event after which some files are no longer referenced by the aggregate that raised it,
/// so they can be removed unless something else still references them.
/// </summary>
public interface IReleasesFiles : IDomainEvent
{
    /// <summary>
    /// Gets the identifiers of the files the aggregate stopped referencing.
    /// </summary>
    public IReadOnlyCollection<StoredFileId> ReleasedFileIds { get; }
}
