using CodigoActivo.Application.Abstractions.Caching;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Application.Partners.Contracts;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.Partners;

namespace CodigoActivo.Application.Partners.Commands;

/// <summary>
/// Carries the input required to create a partner.
/// </summary>
/// <param name="Request">Validated client request data.</param>
/// <param name="UserId">Identifier of the user.</param>
public sealed record CreatePartnerCommand(CreatePartnerRequest Request, Guid UserId)
    : ICommand<Result<Guid>>;

/// <summary>
/// Executes the command to create a partner.
/// </summary>
/// <param name="partners">Repository used to persist and retrieve partners.</param>
/// <param name="files">Repository used to persist and retrieve files.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="cacheInvalidator">Service used to invalidate stale cached responses.</param>
public sealed class CreatePartnerCommandHandler(
    IPartnerRepository partners,
    IFileRepository files,
    IClock clock,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<CreatePartnerCommand, Result<Guid>>
{
    /// <summary>
    /// Handles the request to create a partner.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the identifier of the created item, or an application error on failure.</returns>
    public async Task<Result<Guid>> HandleAsync(
        CreatePartnerCommand command,
        CancellationToken ct = default
    )
    {
        var request = command.Request;

        if (!await files.ExistsAsync(request.ThumbnailId, ct))
        {
            return Error.Validation(ErrorCode.PartnerThumbnailNotFound);
        }

        var partner = Partner.Create(
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
        await partners.AddAsync(partner, ct);
        await uow.SaveChangesAsync(ct);
        await cacheInvalidator.InvalidateAsync(CacheTags.Partners);
        return partner.Id;
    }
}
