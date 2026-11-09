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
    /// a failure only leaves unreferenced data behind for the scheduled purge, so a failure to
    /// delete the rows is neither raised nor logged, and stored content that cannot be deleted
    /// only logs a warning. A failure discards the changes staged since the last commit, so a
    /// refused deletion is not left for a later commit of the same scope.
    /// </summary>
    /// <param name="fileIds">Identifiers of the candidate files.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result is <see langword="false"/> when a failure left the rows in place.
    /// </returns>
    public Task<bool> TryDeleteOrphanedAsync(
        IReadOnlyCollection<StoredFileId> fileIds,
        CancellationToken ct = default
    );

    /// <summary>
    /// Deletes the files among the candidates that nothing references any more and lets a failure
    /// of the database through, so a scheduled purge can report it; stored content that cannot be
    /// deleted only logs a warning.
    /// </summary>
    /// <param name="fileIds">Identifiers of the candidate files.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result contains how many file rows were deleted; zero when every candidate is
    /// still referenced or already gone.
    /// </returns>
    public Task<int> PurgeOrphanedAsync(
        IReadOnlyCollection<StoredFileId> fileIds,
        CancellationToken ct = default
    );
}

/// <summary>
/// Removes orphan file data that is no longer referenced. The stored content is only deleted once
/// the rows are gone; when that fails the content stays on the volume, a warning is logged and
/// <see cref="Commands.PurgeUnusedFilesCommand"/> removes it later.
/// </summary>
/// <param name="files">Repository used to persist and retrieve files.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="storage">Storage holding the content of the files.</param>
/// <param name="logger">Logger used to record operational diagnostics.</param>
public sealed class OrphanFileCleaner(
    IStoredFileRepository files,
    IUnitOfWork uow,
    IFileStorage storage,
    ILogger<OrphanFileCleaner> logger
) : IOrphanFileCleaner
{
    /// <inheritdoc />
    public async Task<bool> TryDeleteOrphanedAsync(
        IReadOnlyCollection<StoredFileId> fileIds,
        CancellationToken ct = default
    )
    {
        try
        {
            await PurgeOrphanedAsync(fileIds, ct);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            uow.DiscardChanges();
            return false;
        }
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

        var inUse = await files.GetInUseAsync(candidates, ct);
        var orphanIds = candidates.Except(inUse).ToList();
        if (orphanIds.Count is 0)
        {
            return 0;
        }

        var orphans = await files.ListByIdsAsync(orphanIds, ct);
        foreach (var file in orphans)
        {
            file.Delete();
            files.Remove(file);
        }

        await uow.SaveChangesAsync(ct);
        foreach (var file in orphans)
        {
            DeleteStoredContent(file.Id, file.Extension);
        }

        return orphans.Count;
    }

    private void DeleteStoredContent(StoredFileId fileId, string extension)
    {
        try
        {
            storage.DeleteVariants(fileId, extension);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.StoredFileContentDeleteFailed(ex);
        }
    }
}
