using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Announcements.Commands;

public sealed record CreateAnnouncementCommand(CreateAnnouncementRequest Request, Guid UserId)
    : ICommand<Result<AnnouncementResponse>>;

public sealed class CreateAnnouncementCommandHandler(
    IAnnouncementRepository announcements,
    IFileRepository files,
    IClock clock,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<CreateAnnouncementCommand, Result<AnnouncementResponse>>
{
    public async Task<Result<AnnouncementResponse>> HandleAsync(
        CreateAnnouncementCommand command,
        CancellationToken ct = default
    )
    {
        var request = command.Request;

        if (!await files.ExistsAsync(f => f.Id == request.ThumbnailId, ct))
        {
            return Error.BadRequest(ErrorCode.AnnouncementThumbnailNotFound);
        }

        var announcement = new Announcement
        {
            Title = request.Title.Trim(),
            Subtitle = request.Subtitle.Trim(),
            Description = request.Description,
            ThumbnailId = request.ThumbnailId,
            CreatedAt = clock.UtcNow,
            CreatedBy = command.UserId,
        };
        await announcements.AddAsync(announcement, ct);
        await uow.SaveChangesAsync(ct);
        await cacheInvalidator.InvalidateAsync(CacheTags.Announcements);
        return announcement.ToResponse();
    }
}
