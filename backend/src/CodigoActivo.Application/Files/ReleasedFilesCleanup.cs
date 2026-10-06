using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Files;

namespace CodigoActivo.Application.Files;

/// <summary>
/// Removes, once per commit, the files the committed changes stopped referencing when nothing
/// else references them any more.
/// </summary>
/// <param name="orphanCleaner">Service used to remove files that are no longer referenced.</param>
public sealed class ReleasedFilesCleanup(IOrphanFileCleaner orphanCleaner) : ICommittedEventsHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(IReadOnlyList<IDomainEvent> domainEvents, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(domainEvents);
        var released = domainEvents
            .OfType<IReleasesFiles>()
            .SelectMany(domainEvent => domainEvent.ReleasedFileIds)
            .Distinct()
            .ToList();
        if (released.Count > 0)
        {
            await orphanCleaner.DeleteOrphanedAsync(released, ct);
        }
    }
}
