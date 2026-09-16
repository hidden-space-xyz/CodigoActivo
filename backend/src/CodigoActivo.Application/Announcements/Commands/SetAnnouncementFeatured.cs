using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Announcements.Queries;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Announcements.Commands;

/// <summary>
/// Carries the input required to set announcement featured.
/// </summary>
/// <param name="AnnouncementId">Identifier of the announcement.</param>
public sealed record SetAnnouncementFeaturedCommand(Guid AnnouncementId)
    : ICommand<Result<AnnouncementResponse>>;

/// <summary>
/// Executes the command to set announcement featured.
/// </summary>
/// <param name="announcements">Repository used to persist and retrieve announcements.</param>
/// <param name="cacheInvalidator">Service used to invalidate stale cached responses.</param>
/// <param name="getById">Handler used to retrieve announcement by identifier.</param>
public sealed class SetAnnouncementFeaturedCommandHandler(
    IAnnouncementRepository announcements,
    ICacheInvalidator cacheInvalidator,
    GetAnnouncementByIdQueryHandler getById
) : ICommandHandler<SetAnnouncementFeaturedCommand, Result<AnnouncementResponse>>
{
    /// <summary>
    /// Handles the request to set announcement featured.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains an announcement on success, or an application error on failure.</returns>
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
