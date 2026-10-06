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
    /// Deletes an if orphaned when it is no longer referenced.
    /// </summary>
    /// <param name="fileId">Identifier of the file.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task DeleteIfOrphanedAsync(StoredFileId fileId, CancellationToken ct = default);

    /// <summary>
    /// Deletes an orphaned when it is no longer referenced.
    /// </summary>
    /// <param name="fileIds">Identifiers of the file items.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task DeleteOrphanedAsync(
        IReadOnlyCollection<StoredFileId> fileIds,
        CancellationToken ct = default
    );
}

/// <summary>
/// Removes orphan file data that is no longer referenced.
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
    /// <summary>
    /// Deletes an if orphaned when it is no longer referenced.
    /// </summary>
    /// <param name="fileId">Identifier of the file.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task DeleteIfOrphanedAsync(StoredFileId fileId, CancellationToken ct = default)
    {
        try
        {
            var file = await files.GetByIdAsync(fileId, ct);
            if (file is null)
            {
                return;
            }

            if (await files.IsInUseAsync(fileId, ct))
            {
                return;
            }

            var storedName = FileNaming.StoredName(file.Id.Value, file.Extension);

            file.Delete();
            files.Remove(file);
            await uow.SaveChangesAsync(ct);

            storage.Delete(storedName);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.OrphanFileCleanupFailed(ex);
        }
    }

    /// <summary>
    /// Deletes an orphaned when it is no longer referenced.
    /// </summary>
    /// <param name="fileIds">Identifiers of the file items.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
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

            var inUse = await files.GetInUseAsync(candidates, ct);
            var orphanIds = candidates.Except(inUse).ToList();
            if (orphanIds.Count is 0)
            {
                return;
            }

            var orphans = await files.ListByIdsAsync(orphanIds, ct);
            if (orphans.Count is 0)
            {
                return;
            }

            foreach (var file in orphans)
            {
                file.Delete();
                files.Remove(file);
            }

            await uow.SaveChangesAsync(ct);

            foreach (var file in orphans)
            {
                DeleteStoredContent(file.Id.Value, file.Extension);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.OrphanFileCleanupFailed(ex);
        }
    }

    private void DeleteStoredContent(Guid fileId, string extension)
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
