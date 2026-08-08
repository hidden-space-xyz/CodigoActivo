using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Announcements.Queries;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Announcements.Commands;

public sealed record SetAnnouncementFeaturedCommand(Guid AnnouncementId)
    : ICommand<Result<AnnouncementResponse>>;

public sealed class SetAnnouncementFeaturedCommandHandler(
    IAnnouncementRepository announcements,
    ICacheInvalidator cacheInvalidator,
    GetAnnouncementByIdQueryHandler getById
) : ICommandHandler<SetAnnouncementFeaturedCommand, Result<AnnouncementResponse>>
{
    public async Task<Result<AnnouncementResponse>> HandleAsync(
        SetAnnouncementFeaturedCommand command,
        CancellationToken ct = default
    )
    {
        if (!await announcements.SetFeaturedAsync(command.AnnouncementId, ct))
        {
            return Error.NotFound(ErrorCode.AnnouncementNotFound);
        }

        await cacheInvalidator.InvalidateAsync(CacheTags.Announcements);
        return await getById.HandleAsync(new GetAnnouncementByIdQuery(command.AnnouncementId), ct);
    }
}
