using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Files;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Storage;

namespace CodigoActivo.Application.Announcements.Commands;

public sealed record UpdateAnnouncementCommand(
    Guid AnnouncementId,
    UpdateAnnouncementRequest Request,
    Guid UserId
) : ICommand<Result<AnnouncementResponse>>;

public sealed class UpdateAnnouncementCommandHandler(
    IAnnouncementRepository announcements,
    IFileRepository files,
    IOrphanFileCleaner orphanCleaner,
    IClock clock,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<UpdateAnnouncementCommand, Result<AnnouncementResponse>>
{
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
