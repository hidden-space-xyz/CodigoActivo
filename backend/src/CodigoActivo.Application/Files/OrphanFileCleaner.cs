using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Storage;
using CodigoActivo.Application.Common.Diagnostics;
using CodigoActivo.Domain.Files;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Application.Files;

/// <summary>
/// Removes orphan file data that is no longer referenced.
/// </summary>
public interface IOrphanFileCleaner
{
    /// <summary>
    /// Deletes the files among the candidates that nothing references any more, as a best effort:
    /// a failure only leaves unreferenced data behind, so it is neither raised nor logged.
    /// </summary>
    /// <param name="fileIds">Identifiers of the candidate files.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task DeleteOrphanedAsync(
        IReadOnlyCollection<StoredFileId> fileIds,
        CancellationToken ct = default
    );

    /// <summary>
    /// Deletes the files among the candidates that nothing references any more and lets a failure
    /// of the database through, so a scheduled purge can report it.
    /// </summary>
    /// <param name="fileIds">Identifiers of the candidate files.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains how many files were deleted.</returns>
    public Task<int> PurgeOrphanedAsync(
        IReadOnlyCollection<StoredFileId> fileIds,
        CancellationToken ct = default
    );
}

/// <summary>
/// Removes orphan file data that is no longer referenced. The candidates are locked for deletion
/// (<see cref="IStoredFileRepository.LockForDeletionAsync"/>) before their references are read, so a
/// save that locked them to reference them is waited for, and the rows are deleted in that
/// transaction, so a refused delete leaves nothing staged for a later commit of the same request.
/// The stored content is only deleted once the rows are gone; when that fails the content stays on
/// the volume, a warning is logged and <see cref="Commands.PurgeUnusedFilesCommand"/> removes it later.
/// </summary>
/// <param name="files">Repository used to persist and retrieve files.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="storage">Repository used to persist and retrieve storage.</param>
/// <param name="logger">Logger used to record operational diagnostics.</param>
public sealed class OrphanFileCleaner(
    IStoredFileRepository files,
    IUnitOfWork uow,
    IFileStorage storage,
    ILogger<OrphanFileCleaner> logger
) : IOrphanFileCleaner
{
    /// <inheritdoc />
    public async Task DeleteOrphanedAsync(
        IReadOnlyCollection<StoredFileId> fileIds,
        CancellationToken ct = default
    )
    {
        try
        {
            await PurgeOrphanedAsync(fileIds, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { }
    }

    /// <inheritdoc />
    public async Task<int> PurgeOrphanedAsync(
        IReadOnlyCollection<StoredFileId> fileIds,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(fileIds);

        var candidates = fileIds.Distinct().ToList();
        if (candidates.Count is 0)
        {
            return 0;
        }

        var removed = await uow.ExecuteInTransactionAsync(
            attempt => RemoveOrphansAsync(candidates, attempt),
            ct
        );

        foreach (var file in removed)
        {
            DeleteStoredContent(file.Id, file.Extension);
        }

        return removed.Count;
    }

    private async Task<IReadOnlyList<StoredFile>> RemoveOrphansAsync(
        List<StoredFileId> candidates,
        CancellationToken ct
    )
    {
        var existing = await files.LockForDeletionAsync(candidates, ct);
        var inUse = await files.GetInUseAsync(existing, ct);
        var orphanIds = existing.Except(inUse).ToList();
        if (orphanIds.Count is 0)
        {
            return [];
        }

        var orphans = await files.ListByIdsAsync(orphanIds, ct);
        foreach (var file in orphans)
        {
            file.Delete();
            files.Remove(file);
        }

        await uow.SaveChangesAsync(ct);
        return orphans;
    }

    private void DeleteStoredContent(StoredFileId fileId, string extension)
    {
        try
        {
            foreach (var storedName in FileNaming.StoredNames(fileId, extension))
            {
                storage.Delete(storedName);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.StoredFileContentDeleteFailed(ex);
        }
    }
}
