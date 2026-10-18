using CodigoActivo.Application.Abstractions.Auditing;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Files;

namespace CodigoActivo.Application.Files.Commands;

/// <summary>
/// Carries the request to delete the uploads no content used in time.
/// </summary>
public sealed record PurgeUnusedFilesCommand : ICommand<Result<int>>, IScheduledCommand;

/// <summary>
/// Executes the command that deletes the uploads that no content references
/// <see cref="StoredFile.UnusedRetentionDays"/> after they were uploaded, such as an image picked
/// for a form that was never saved. <see cref="IOrphanFileCleaner"/> checks the references again in
/// the transaction that deletes them.
/// </summary>
/// <param name="files">Repository used to persist and retrieve files.</param>
/// <param name="orphanCleaner">Service used to remove files that are no longer referenced.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class PurgeUnusedFilesCommandHandler(
    IStoredFileRepository files,
    IOrphanFileCleaner orphanCleaner,
    IClock clock
) : ICommandHandler<PurgeUnusedFilesCommand, Result<int>>
{
    /// <summary>
    /// Handles the request to delete the uploads no content used in time.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result carries the number of files deleted.</returns>
    public async Task<Result<int>> HandleAsync(
        PurgeUnusedFilesCommand command,
        CancellationToken ct = default
    )
    {
        var candidates = await files.ListIdsUploadedUpToAsync(
            StoredFile.UnusedPurgeCutoff(clock.UtcNow),
            ct
        );
        return await orphanCleaner.DeleteOrphanedAsync(candidates, ct);
    }
}
