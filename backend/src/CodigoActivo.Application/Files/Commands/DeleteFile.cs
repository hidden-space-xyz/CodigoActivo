using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Storage;

namespace CodigoActivo.Application.Files.Commands;

/// <summary>
/// Carries the input required to delete the file.
/// </summary>
/// <param name="FileId">Identifier of the file.</param>
public sealed record DeleteFileCommand(Guid FileId) : ICommand<Result>;

/// <summary>
/// Executes the command to delete the file.
/// </summary>
/// <param name="files">Repository used to persist and retrieve files.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="storage">Repository used to persist and retrieve storage.</param>
/// <param name="cacheInvalidator">Service used to invalidate stale cached responses.</param>
public sealed class DeleteFileCommandHandler(
    IFileRepository files,
    IUnitOfWork uow,
    ILocalFileSystemRepository storage,
    ICacheInvalidator cacheInvalidator
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
        var file = await files.FindAsync(f => f.Id == command.FileId, ct);
        if (file is null)
        {
            return Error.NotFound(ErrorCode.FileNotFound);
        }

        if (await files.IsInUseAsync(command.FileId, ct))
        {
            return Error.Conflict(ErrorCode.FileInUse);
        }

        var storedName = FileNaming.StoredName(file.Id, file.Extension);

        files.Remove(file);
        await uow.SaveChangesAsync(ct);

        storage.Delete(storedName);
        await cacheInvalidator.InvalidateAsync(CacheTags.Files);
        return Result.Success();
    }
}
