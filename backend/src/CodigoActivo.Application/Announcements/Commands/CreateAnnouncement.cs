using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Announcements.Commands;

/// <summary>
/// Carries the input required to create an announcement.
/// </summary>
/// <param name="Request">Validated client request data.</param>
/// <param name="UserId">Identifier of the user.</param>
public sealed record CreateAnnouncementCommand(CreateAnnouncementRequest Request, Guid UserId)
    : ICommand<Result<AnnouncementResponse>>;

/// <summary>
/// Executes the command to create an announcement.
/// </summary>
/// <param name="announcements">Repository used to persist and retrieve announcements.</param>
/// <param name="files">Repository used to persist and retrieve files.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="cacheInvalidator">Service used to invalidate stale cached responses.</param>
public sealed class CreateAnnouncementCommandHandler(
    IAnnouncementRepository announcements,
    IFileRepository files,
    IClock clock,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<CreateAnnouncementCommand, Result<AnnouncementResponse>>
{
    /// <summary>
    /// Handles the request to create an announcement.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains an announcement on success, or an application error on failure.</returns>
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
