using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Extensions;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Application.Services.Abstractions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Storage;

namespace CodigoActivo.Application.Services;

public class FileService(
    IFileRepository files,
    IUnitOfWork uow,
    ILocalFileSystemRepository storage,
    FileStorageOptions options
) : IFileService
{
    private const int MaxNameLength = 260;
    private const string FallbackContentType = "application/octet-stream";

    public async Task<Result<FileResponse>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var file = await files.FindAsync(f => f.Id == id, ct);
        var response = file?.ToResponse();

        return response is null ? Error.NotFound(ErrorCode.FileNotFound) : Result.Success(response);
    }

    public async Task<Result<FileContentValueObject>> GetContentAsync(
        Guid id,
        CancellationToken ct = default
    )
    {
        var meta = await GetByIdAsync(id, ct);
        if (meta.IsFailure) return meta.Error!;

        var stream = await storage.OpenReadAsync(
            StoredName(meta.Value.Id, meta.Value.Extension),
            ct
        );
        if (stream is null) return Error.NotFound(ErrorCode.FileContentMissingFromStorage);

        var format = await stream.DetectImageFormatAsync(ct);
        stream.Position = 0;

        return new FileContentValueObject(
            stream,
            format?.ContentType ?? FallbackContentType,
            meta.Value.Name
        );
    }

    public async Task<Result<FileResponse>> CreateAsync(
        FileUploadRequest upload,
        Guid userId,
        CancellationToken ct = default
    )
    {
        var detection = await ValidateAndDetectAsync(upload, ct);
        if (detection.IsFailure) return detection.Error!;

        var format = detection.Value;
        var file = new FileEntity
        {
            Name = SanitizeName(upload.FileName),
            Extension = format.Extension,
            UploadedAt = DateTimeOffset.UtcNow,
            UploadedBy = userId
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
        FileUploadRequest upload,
        CancellationToken ct = default
    )
    {
        var file = await files.FindAsync(f => f.Id == id, ct);
        if (file is null) return Error.NotFound(ErrorCode.FileNotFound);

        var detection = await ValidateAndDetectAsync(upload, ct);
        if (detection.IsFailure) return detection.Error!;

        var format = detection.Value;
        var oldStoredName = StoredName(file.Id, file.Extension);
        var newStoredName = StoredName(file.Id, format.Extension);
        var extensionChanged = !string.Equals(
            oldStoredName,
            newStoredName,
            StringComparison.OrdinalIgnoreCase
        );

        await storage.SaveAsync(newStoredName, upload.Content, ct);

        file.Name = SanitizeName(upload.FileName);
        file.Extension = format.Extension;
        files.Update(file);

        try
        {
            await uow.SaveChangesAsync(ct);
        }
        catch
        {
            if (extensionChanged) storage.Delete(newStoredName);
            throw;
        }

        if (extensionChanged) storage.Delete(oldStoredName);

        return file.ToResponse();
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var file = await files.FindAsync(f => f.Id == id, ct);
        if (file is null) return Error.NotFound(ErrorCode.FileNotFound);

        var storedName = StoredName(file.Id, file.Extension);

        await files.RemoveAsync(f => f.Id == id, ct);
        await uow.SaveChangesAsync(ct);

        storage.Delete(storedName);
        return Result.Success();
    }

    private async Task<Result<ImageFormat>> ValidateAndDetectAsync(
        FileUploadRequest upload,
        CancellationToken ct
    )
    {
        if (upload is null) return Error.BadRequest(ErrorCode.FileUploadMissing);

        if (upload.Length <= 0) return Error.BadRequest(ErrorCode.FileUploadEmpty);

        if (upload.Length > options.MaxSizeBytes) return Error.BadRequest(ErrorCode.FileUploadTooLarge);

        if (!upload.Content.CanSeek) return Error.BadRequest(ErrorCode.FileUploadStreamNotSeekable);

        upload.Content.Position = 0;
        var format = await upload.Content.DetectImageFormatAsync(ct);
        if (format is null) return Error.BadRequest(ErrorCode.FileUploadUnsupportedFormat);

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
        if (string.IsNullOrEmpty(name)) name = "file";

        return name.Length > MaxNameLength ? name[..MaxNameLength] : name;
    }
}