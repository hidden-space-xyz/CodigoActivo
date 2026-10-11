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
    /// Deletes the files among the candidates that nothing references any more.
    /// </summary>
    /// <param name="fileIds">Identifiers of the candidate files.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task DeleteOrphanedAsync(
        IReadOnlyCollection<StoredFileId> fileIds,
        CancellationToken ct = default
    );
}

/// <summary>
/// Removes orphan file data that is no longer referenced. The rows are deleted in one transaction,
/// so a refused delete leaves nothing staged for a later commit of the same request; the stored
/// content is only deleted once the rows are gone.
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
            var candidates = fileIds.Distinct().ToList();
            if (candidates.Count is 0)
            {
                return;
            }

            var removed = await uow.ExecuteInTransactionAsync(
                attempt => RemoveOrphansAsync(candidates, attempt),
                ct
            );

            foreach (var file in removed)
            {
                DeleteStoredContent(file.Id, file.Extension);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.OrphanFileCleanupFailed(ex);
        }
    }

    private async Task<IReadOnlyList<StoredFile>> RemoveOrphansAsync(
        List<StoredFileId> candidates,
        CancellationToken ct
    )
    {
        var inUse = await files.GetInUseAsync(candidates, ct);
        var orphanIds = candidates.Except(inUse).ToList();
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
            storage.Delete(FileNaming.StoredName(fileId, extension));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.OrphanFileCleanupFailed(ex);
        }
    }
}
