using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.Files;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Storage;

namespace CodigoActivo.Application.Announcements.Commands;

public sealed record DeleteAnnouncementCommand(Guid AnnouncementId) : ICommand<Result>;

public sealed class DeleteAnnouncementCommandHandler(
    IAnnouncementRepository announcements,
    IOrphanFileCleaner orphanCleaner,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<DeleteAnnouncementCommand, Result>
{
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
