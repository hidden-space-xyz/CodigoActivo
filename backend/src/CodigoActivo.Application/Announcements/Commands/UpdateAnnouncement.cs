using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Files;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Storage;

namespace CodigoActivo.Application.Announcements.Commands;

/// <summary>
/// Carries the input required to update the announcement.
/// </summary>
/// <param name="AnnouncementId">Identifier of the announcement.</param>
/// <param name="Request">Validated client request data.</param>
/// <param name="UserId">Identifier of the user.</param>
public sealed record UpdateAnnouncementCommand(
    Guid AnnouncementId,
    UpdateAnnouncementRequest Request,
    Guid UserId
) : ICommand<Result<AnnouncementResponse>>;

/// <summary>
/// Executes the command to update the announcement.
/// </summary>
/// <param name="announcements">Repository used to persist and retrieve announcements.</param>
/// <param name="files">Repository used to persist and retrieve files.</param>
/// <param name="orphanCleaner">Service used to remove files that are no longer referenced.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="cacheInvalidator">Service used to invalidate stale cached responses.</param>
public sealed class UpdateAnnouncementCommandHandler(
    IAnnouncementRepository announcements,
    IFileRepository files,
    IOrphanFileCleaner orphanCleaner,
    IClock clock,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<UpdateAnnouncementCommand, Result<AnnouncementResponse>>
{
    /// <summary>
    /// Handles the request to update the announcement.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains an announcement on success, or an application error on failure.</returns>
    public async Task<Result<AnnouncementResponse>> HandleAsync(
        UpdateAnnouncementCommand command,
        CancellationToken ct = default
    )
    {
        var request = command.Request;

        var announcement = await announcements.FindAsync(a => a.Id == command.AnnouncementId, ct);
        if (announcement is null)
        {
            return Error.NotFound(ErrorCode.AnnouncementNotFound);
        }

        if (!await files.ExistsAsync(f => f.Id == request.ThumbnailId, ct))
        {
            return Error.BadRequest(ErrorCode.AnnouncementThumbnailNotFound);
        }

        var previousThumbnailId = announcement.ThumbnailId;
        var previousDescription = announcement.Description;

        announcement.Title = request.Title.Trim();
        announcement.Subtitle = request.Subtitle.Trim();
        announcement.Description = request.Description;
        announcement.ThumbnailId = request.ThumbnailId;
        announcement.UpdatedAt = clock.UtcNow;
        announcement.UpdatedBy = command.UserId;

        await uow.SaveChangesAsync(ct);
        await cacheInvalidator.InvalidateAsync(CacheTags.Announcements);

        var orphanCandidates = RichTextFileReferences
            .ExtractRemoved(previousDescription, announcement.Description)
            .ToList();
        if (previousThumbnailId != request.ThumbnailId)
        {
            orphanCandidates.Add(previousThumbnailId);
        }

        await orphanCleaner.DeleteOrphanedAsync(orphanCandidates, ct);

        return announcement.ToResponse();
    }
}
