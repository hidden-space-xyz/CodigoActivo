using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Extensions;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Application.Services.Abstractions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Storage;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Application.Services;

public class FileService(
    IFileRepository files,
    IUnitOfWork uow,
    ILocalFileSystemRepository storage,
    IClock clock,
    FileStorageOptions options,
    ILogger<FileService> logger,
    ICacheInvalidator cacheInvalidator
) : IFileService
{
    private const int MaxNameLength = 260;
    private const string FallbackContentType = "application/octet-stream";

    public async Task<Result<FileResponse>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var matches = await files.GetAsync(f => f.Id == id, ct);
        var response = matches.Count == 0 ? null : matches[0].ToResponse();

        return response is null ? Error.NotFound(ErrorCode.FileNotFound) : Result.Success(response);
    }

    public async Task<Result<FileContentValueObject>> GetContentAsync(
        Guid id,
        CancellationToken ct = default
    )
    {
        var meta = await GetByIdAsync(id, ct);
        if (meta.IsFailure)
            return meta.Error!;

        var stream = await storage.OpenReadAsync(
            StoredName(meta.Value.Id, meta.Value.Extension),
            ct
        );
        if (stream is null)
            return Error.NotFound(ErrorCode.FileContentMissingFromStorage);

        var format = await stream.DetectImageFormatAsync(ct);
        stream.Position = 0;

        return new FileContentValueObject(
            stream,
            format?.ContentType ?? FallbackContentType,
            meta.Value.Name,
            meta.Value.UploadedAt
        );
    }

    public async Task<Result<FileResponse>> CreateAsync(
        FileUploadRequest? upload,
        Guid userId,
        CancellationToken ct = default
    )
    {
        var detection = await ValidateAndDetectAsync(upload, ct);
        if (detection.IsFailure)
            return detection.Error!;

        var format = detection.Value;
        var file = new FileEntity
        {
            Name = SanitizeName(upload!.FileName),
            Extension = format.Extension,
            UploadedAt = clock.UtcNow,
            UploadedBy = userId,
        };

        var storedName = StoredName(file.Id, file.Extension);
        await storage.SaveAsync(storedName, upload.Content, ct);

        try
        {
            await files.AddAsync(file, ct);
            await uow.SaveChangesAsync(ct);
        }
        catch
        {
            storage.Delete(storedName);
            throw;
        }

        return file.ToResponse();
    }

    public async Task<Result<FileResponse>> UpdateAsync(
        Guid id,
        FileUploadRequest? upload,
        CancellationToken ct = default
    )
    {
        var file = await files.FindAsync(f => f.Id == id, ct);
        if (file is null)
            return Error.NotFound(ErrorCode.FileNotFound);

        var detection = await ValidateAndDetectAsync(upload, ct);
        if (detection.IsFailure)
            return detection.Error!;

        var format = detection.Value;
        var oldStoredName = StoredName(file.Id, file.Extension);
        var newStoredName = StoredName(file.Id, format.Extension);
        var extensionChanged = !string.Equals(
            oldStoredName,
            newStoredName,
            StringComparison.OrdinalIgnoreCase
        );

        await storage.SaveAsync(newStoredName, upload!.Content, ct);

        file.Name = SanitizeName(upload.FileName);
        file.Extension = format.Extension;
        file.UploadedAt = clock.UtcNow;

        try
        {
            await uow.SaveChangesAsync(ct);
        }
        catch
        {
            if (extensionChanged)
                storage.Delete(newStoredName);
            throw;
        }

        if (extensionChanged)
            storage.Delete(oldStoredName);

        await cacheInvalidator.InvalidateAsync(CacheTags.Files);
        return file.ToResponse();
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var file = await files.FindAsync(f => f.Id == id, ct);
        if (file is null)
            return Error.NotFound(ErrorCode.FileNotFound);

        if (await files.IsInUseAsync(id, ct))
            return Error.Conflict(ErrorCode.FileInUse);

        var storedName = StoredName(file.Id, file.Extension);

        files.Remove(file);
        await uow.SaveChangesAsync(ct);

        storage.Delete(storedName);
        await cacheInvalidator.InvalidateAsync(CacheTags.Files);
        return Result.Success();
    }

    public async Task DeleteIfOrphanedAsync(Guid fileId, CancellationToken ct = default)
    {
        try
        {
            _ = await DeleteAsync(fileId, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug(ex, "Best-effort orphan cleanup failed for file {FileId}", fileId);
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
            if (candidates.Count == 0)
                return;

            var inUse = await files.GetInUseAsync(candidates, ct);
            var orphanIds = candidates.Except(inUse).ToList();
            if (orphanIds.Count == 0)
                return;

            var orphans = await files.GetAsync(f => orphanIds.Contains(f.Id), ct);
            if (orphans.Count == 0)
                return;

            foreach (var file in orphans)
                files.Remove(file);
            await uow.SaveChangesAsync(ct);

            foreach (var file in orphans)
            {
                try
                {
                    storage.Delete(StoredName(file.Id, file.Extension));
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    if (logger.IsEnabled(LogLevel.Debug))
                        logger.LogDebug(
                            ex,
                            "Best-effort stored content deletion failed for file {FileId}",
                            file.Id
                        );
                }
            }

            await cacheInvalidator.InvalidateAsync(CacheTags.Files);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug(
                    ex,
                    "Best-effort orphan cleanup failed for files {FileIds}",
                    fileIds
                );
        }
    }

    private async Task<Result<ImageFormat>> ValidateAndDetectAsync(
        FileUploadRequest? upload,
        CancellationToken ct
    )
    {
        if (upload is null)
            return Error.BadRequest(ErrorCode.FileUploadMissing);

        if (upload.Length <= 0)
            return Error.BadRequest(ErrorCode.FileUploadEmpty);

        if (upload.Length > options.MaxSizeBytes)
            return Error.BadRequest(ErrorCode.FileUploadTooLarge);

        if (!upload.Content.CanSeek)
            return Error.BadRequest(ErrorCode.FileUploadStreamNotSeekable);

        upload.Content.Position = 0;
        var format = await upload.Content.DetectImageFormatAsync(ct);
        if (format is null)
            return Error.BadRequest(ErrorCode.FileUploadUnsupportedFormat);

        upload.Content.Position = 0;
        return format;
    }

    private static string StoredName(Guid id, string extension)
    {
        return $"{id}.{extension}";
    }

    private static string SanitizeName(string? fileName)
    {
        var name = Path.GetFileName(fileName ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(name))
            name = "file";

        return name.Length > MaxNameLength ? name[..MaxNameLength] : name;
    }
}
