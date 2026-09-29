using CodigoActivo.Application.Abstractions.Caching;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Application.Files;
using CodigoActivo.Application.Partners.Contracts;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.Partners;

namespace CodigoActivo.Application.Partners.Commands;

/// <summary>
/// Carries the input required to update the partner.
/// </summary>
/// <param name="PartnerId">Identifier of the partner.</param>
/// <param name="Request">Validated client request data.</param>
/// <param name="UserId">Identifier of the user.</param>
public sealed record UpdatePartnerCommand(Guid PartnerId, UpdatePartnerRequest Request, Guid UserId)
    : ICommand<Result>;

/// <summary>
/// Executes the command to update the partner.
/// </summary>
/// <param name="partners">Repository used to persist and retrieve partners.</param>
/// <param name="files">Repository used to persist and retrieve files.</param>
/// <param name="orphanCleaner">Service used to remove files that are no longer referenced.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="cacheInvalidator">Service used to invalidate stale cached responses.</param>
public sealed class UpdatePartnerCommandHandler(
    IPartnerRepository partners,
    IFileRepository files,
    IOrphanFileCleaner orphanCleaner,
    IClock clock,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<UpdatePartnerCommand, Result>
{
    /// <summary>
    /// Handles the request to update the partner.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result reports success, or an application error on failure.</returns>
    public async Task<Result> HandleAsync(
        UpdatePartnerCommand command,
        CancellationToken ct = default
    )
    {
        var request = command.Request;

        var partner = await partners.GetByIdAsync(command.PartnerId, ct);
        if (partner is null)
        {
            return Error.NotFound(ErrorCode.PartnerNotFound);
        }

        if (!await files.ExistsAsync(request.ThumbnailId, ct))
        {
            return Error.Validation(ErrorCode.PartnerThumbnailNotFound);
        }

        var previousThumbnailId = partner.ThumbnailId;

        partner.Update(
            new PartnerDetails(
                request.Name,
                request.FromDate!.Value,
                request.Tier,
                request.Website,
                request.ThumbnailId
            ),
            command.UserId,
            clock.UtcNow
        );

        await uow.SaveChangesAsync(ct);
        await cacheInvalidator.InvalidateAsync(CacheTags.Partners);

        if (previousThumbnailId != request.ThumbnailId)
        {
            await orphanCleaner.DeleteIfOrphanedAsync(previousThumbnailId, ct);
        }

        return Result.Success();
    }
}
