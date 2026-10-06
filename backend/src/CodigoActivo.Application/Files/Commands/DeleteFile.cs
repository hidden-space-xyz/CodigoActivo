using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Storage;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Files;

namespace CodigoActivo.Application.Files.Commands;

/// <summary>
/// Carries the input required to delete the file.
/// </summary>
/// <param name="FileId">Identifier of the file.</param>
public sealed record DeleteFileCommand(StoredFileId FileId) : ICommand<Result>;

/// <summary>
/// Executes the command to delete the file.
/// </summary>
/// <param name="files">Repository used to persist and retrieve files.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="storage">Repository used to persist and retrieve storage.</param>
public sealed class DeleteFileCommandHandler(
    IStoredFileRepository files,
    IUnitOfWork uow,
    IFileStorage storage
) : ICommandHandler<DeleteFileCommand, Result>
{
    /// <summary>
    /// Handles the request to delete the file.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result indicates success or contains the application error.</returns>
    public async Task<Result> HandleAsync(DeleteFileCommand command, CancellationToken ct = default)
    {
        var file = await files.GetByIdAsync(command.FileId, ct);
        if (file is null)
        {
            return Error.NotFound(ApplicationErrorCode.FileNotFound);
        }

        if (await files.IsInUseAsync(command.FileId, ct))
        {
            return Error.Conflict(ApplicationErrorCode.FileInUse);
        }

        var storedName = FileNaming.StoredName(file.Id.Value, file.Extension);

        file.Delete();
        files.Remove(file);
        await uow.SaveChangesAsync(ct);

        storage.Delete(storedName);
        return Result.Success();
    }
}
