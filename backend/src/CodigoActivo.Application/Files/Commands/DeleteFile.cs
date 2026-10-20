using CodigoActivo.Application.Abstractions.Messaging;
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
/// Executes the command to delete the file through <see cref="IOrphanFileCleaner"/>, which locks it
/// and checks its references again in the transaction that deletes it.
/// </summary>
/// <param name="files">Repository used to persist and retrieve files.</param>
/// <param name="orphanCleaner">Service used to remove files that are no longer referenced.</param>
public sealed class DeleteFileCommandHandler(
    IStoredFileRepository files,
    IOrphanFileCleaner orphanCleaner
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

        return await orphanCleaner.PurgeOrphanedAsync([command.FileId], ct) > 0
            ? Result.Success()
            : Error.Conflict(ApplicationErrorCode.FileInUse);
    }
}
