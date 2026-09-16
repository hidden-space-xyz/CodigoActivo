using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Storage;

namespace CodigoActivo.Application.Files.Commands;

/// <summary>
/// Carries the input required to create a file.
/// </summary>
/// <param name="Upload">The upload value.</param>
/// <param name="UserId">Identifier of the user.</param>
public sealed record CreateFileCommand(FileUpload? Upload, Guid UserId)
    : ICommand<Result<FileResponse>>;

/// <summary>
/// Executes the command to create a file.
/// </summary>
/// <param name="files">Repository used to persist and retrieve files.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="storage">Repository used to persist and retrieve storage.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="validator">The validator value.</param>
public sealed class CreateFileCommandHandler(
    IFileRepository files,
    IUnitOfWork uow,
    ILocalFileSystemRepository storage,
    IClock clock,
    FileUploadValidator validator
) : ICommandHandler<CreateFileCommand, Result<FileResponse>>
{
    /// <summary>
    /// Handles the request to create a file.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains a file on success, or an application error on failure.</returns>
    public async Task<Result<FileResponse>> HandleAsync(
        CreateFileCommand command,
        CancellationToken ct = default
    )
    {
        var upload = command.Upload;

        var detection = await validator.ValidateAndDetectAsync(upload, ct);
        if (detection.IsFailure)
        {
            return detection.Error!;
        }

        var format = detection.Value;
        var file = new FileEntity
        {
            Name = FileNaming.SanitizeName(upload!.FileName),
            Extension = format.Extension,
            UploadedAt = clock.UtcNow,
            UploadedBy = command.UserId,
        };

        var storedName = FileNaming.StoredName(file.Id, file.Extension);
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
}
