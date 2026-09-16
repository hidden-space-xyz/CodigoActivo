using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.Files;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Storage;

namespace CodigoActivo.Application.Announcements.Commands;

/// <summary>
/// Carries the input required to delete the announcement.
/// </summary>
/// <param name="AnnouncementId">Identifier of the announcement.</param>
public sealed record DeleteAnnouncementCommand(Guid AnnouncementId) : ICommand<Result>;

/// <summary>
/// Executes the command to delete the announcement.
/// </summary>
/// <param name="announcements">Repository used to persist and retrieve announcements.</param>
/// <param name="orphanCleaner">Service used to remove files that are no longer referenced.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="cacheInvalidator">Service used to invalidate stale cached responses.</param>
public sealed class DeleteAnnouncementCommandHandler(
    IAnnouncementRepository announcements,
    IOrphanFileCleaner orphanCleaner,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<DeleteAnnouncementCommand, Result>
{
    /// <summary>
    /// Handles the request to delete the announcement.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result indicates success or contains the application error.</returns>
    public async Task<Result> HandleAsync(
        DeleteAnnouncementCommand command,
        CancellationToken ct = default
    )
    {
        var announcement = await announcements.FindAsync(a => a.Id == command.AnnouncementId, ct);
        if (announcement is null)
        {
            return Error.NotFound(ErrorCode.AnnouncementNotFound);
        }

        announcements.Remove(announcement);
        await uow.SaveChangesAsync(ct);
        await cacheInvalidator.InvalidateAsync(CacheTags.Announcements);

        var orphanCandidates = RichTextFileReferences
            .Extract(announcement.Description)
            .Append(announcement.ThumbnailId)
            .Distinct()
            .ToList();
        await orphanCleaner.DeleteOrphanedAsync(orphanCandidates, ct);

        return Result.Success();
    }
}
