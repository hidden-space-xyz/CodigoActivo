using CodigoActivo.Application.Caching;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Storage;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Application.Files;

public interface IOrphanFileCleaner
{
    public Task DeleteIfOrphanedAsync(Guid fileId, CancellationToken ct = default);

    public Task DeleteOrphanedAsync(
        IReadOnlyCollection<Guid> fileIds,
        CancellationToken ct = default
    );
}

public sealed class OrphanFileCleaner(
    IFileRepository files,
    IUnitOfWork uow,
    ILocalFileSystemRepository storage,
    ICacheInvalidator cacheInvalidator,
    ILogger<OrphanFileCleaner> logger
) : IOrphanFileCleaner
{
    public async Task DeleteIfOrphanedAsync(Guid fileId, CancellationToken ct = default)
    {
        try
        {
            var file = await files.FindAsync(f => f.Id == fileId, ct);
            if (file is null)
            {
                return;
            }

            if (await files.IsInUseAsync(fileId, ct))
            {
                return;
            }

            var storedName = FileNaming.StoredName(file.Id, file.Extension);

            files.Remove(file);
            await uow.SaveChangesAsync(ct);

            storage.Delete(storedName);
            await cacheInvalidator.InvalidateAsync(CacheTags.Files);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug(ex, "Best-effort orphan cleanup failed for file {FileId}", fileId);
            }
        }
    }

    public async Task DeleteOrphanedAsync(
        IReadOnlyCollection<Guid> fileIds,
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

            var orphans = await files.GetAsync(f => orphanIds.Contains(f.Id), ct);
            if (orphans.Count is 0)
            {
                return;
            }

            foreach (var file in orphans)
            {
                files.Remove(file);
            }

            await uow.SaveChangesAsync(ct);

            foreach (var file in orphans)
            {
                DeleteStoredContent(file.Id, file.Extension);
            }

            await cacheInvalidator.InvalidateAsync(CacheTags.Files);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug(
                    ex,
                    "Best-effort orphan cleanup failed for files {FileIds}",
                    fileIds
                );
            }
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
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug(
                    ex,
                    "Best-effort stored content deletion failed for file {FileId}",
                    fileId
                );
            }
        }
    }
}
