using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Storage;

namespace CodigoActivo.Application.Files.Commands;

/// <summary>
/// Carries the input required to update the file.
/// </summary>
/// <param name="FileId">Identifier of the file.</param>
/// <param name="Upload">The upload value.</param>
public sealed record UpdateFileCommand(Guid FileId, FileUpload? Upload)
    : ICommand<Result<FileResponse>>;

/// <summary>
/// Executes the command to update the file.
/// </summary>
/// <param name="files">Repository used to persist and retrieve files.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="storage">Repository used to persist and retrieve storage.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="validator">The validator value.</param>
/// <param name="cacheInvalidator">Service used to invalidate stale cached responses.</param>
public sealed class UpdateFileCommandHandler(
    IFileRepository files,
    IUnitOfWork uow,
    ILocalFileSystemRepository storage,
    IClock clock,
    FileUploadValidator validator,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<UpdateFileCommand, Result<FileResponse>>
{
    /// <summary>
    /// Handles the request to update the file.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains a file on success, or an application error on failure.</returns>
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
