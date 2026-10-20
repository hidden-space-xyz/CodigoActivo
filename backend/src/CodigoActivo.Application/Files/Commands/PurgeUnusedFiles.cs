using CodigoActivo.Application.Abstractions.Auditing;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Storage;
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
/// for a form that was never saved. <see cref="IOrphanFileCleaner"/> locks them and checks the
/// references again in the transaction that deletes them, and a failure is raised so the scheduled
/// run reports it. It then removes the stored content that no file row owns
/// <see cref="StrayContentAge"/> after it was written: a write cut short, an upload whose row was
/// never committed or content whose deletion failed.
/// </summary>
/// <param name="files">Repository used to persist and retrieve files.</param>
/// <param name="orphanCleaner">Service used to remove files that are no longer referenced.</param>
/// <param name="storage">Storage holding the content of the files.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class PurgeUnusedFilesCommandHandler(
    IStoredFileRepository files,
    IOrphanFileCleaner orphanCleaner,
    IFileStorage storage,
    IClock clock
) : ICommandHandler<PurgeUnusedFilesCommand, Result<int>>
{
    /// <summary>
    /// Gets how long stored content may exist without a file row before it is removed, far longer
    /// than an upload takes to commit its row after writing the content.
    /// </summary>
    public static TimeSpan StrayContentAge { get; } = TimeSpan.FromHours(1);

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
        var now = clock.UtcNow;
        var candidates = await files.ListIdsUploadedUpToAsync(
            StoredFile.UnusedPurgeCutoff(now),
            ct
        );
        var purged = await orphanCleaner.PurgeOrphanedAsync(candidates, ct);
        await RemoveStrayContentAsync(now - StrayContentAge, ct);
        return purged;
    }

    private async Task RemoveStrayContentAsync(DateTimeOffset cutoff, CancellationToken ct)
    {
        var stored = storage.ListWrittenUpTo(cutoff);
        var owners = stored
            .Select(name => FileNaming.TryReadId(name, out var id) ? id : (StoredFileId?)null)
            .OfType<StoredFileId>()
            .Distinct()
            .ToList();
        IReadOnlyList<StoredFile> owned = owners.Count is 0
            ? []
            : await files.ListByIdsAsync(owners, ct);
        var kept = owned
            .SelectMany(file => FileNaming.StoredNames(file.Id, file.Extension))
            .ToHashSet(StringComparer.Ordinal);
        foreach (var name in stored.Where(name => !kept.Contains(name)))
        {
            storage.Delete(name);
        }
    }
}
