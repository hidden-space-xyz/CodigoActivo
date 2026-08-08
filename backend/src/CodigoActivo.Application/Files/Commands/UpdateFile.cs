using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Storage;

namespace CodigoActivo.Application.Files.Commands;

public sealed record UpdateFileCommand(Guid FileId, FileUpload? Upload)
    : ICommand<Result<FileResponse>>;

public sealed class UpdateFileCommandHandler(
    IFileRepository files,
    IUnitOfWork uow,
    ILocalFileSystemRepository storage,
    IClock clock,
    FileUploadValidator validator,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<UpdateFileCommand, Result<FileResponse>>
{
    public async Task<Result<FileResponse>> HandleAsync(
        UpdateFileCommand command,
        CancellationToken ct = default
    )
    {
        var upload = command.Upload;

        var file = await files.FindAsync(f => f.Id == command.FileId, ct);
        if (file is null)
        {
            return Error.NotFound(ErrorCode.FileNotFound);
        }

        var detection = await validator.ValidateAndDetectAsync(upload, ct);
        if (detection.IsFailure)
        {
            return detection.Error!;
        }

        var format = detection.Value;
        var oldStoredName = FileNaming.StoredName(file.Id, file.Extension);
        var newStoredName = FileNaming.StoredName(file.Id, format.Extension);
        var extensionChanged = !string.Equals(
            oldStoredName,
            newStoredName,
            StringComparison.OrdinalIgnoreCase
        );

        await storage.SaveAsync(newStoredName, upload!.Content, ct);

        file.Name = FileNaming.SanitizeName(upload.FileName);
        file.Extension = format.Extension;
        file.UploadedAt = clock.UtcNow;

        try
        {
            await uow.SaveChangesAsync(ct);
        }
        catch
        {
            if (extensionChanged)
            {
                storage.Delete(newStoredName);
            }

            throw;
        }

        if (extensionChanged)
        {
            storage.Delete(oldStoredName);
        }

        await cacheInvalidator.InvalidateAsync(CacheTags.Files);
        return file.ToResponse();
    }
}
