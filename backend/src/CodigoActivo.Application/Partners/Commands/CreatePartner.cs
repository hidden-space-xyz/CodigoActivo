using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Extensions;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Partners.Commands;

/// <summary>
/// Carries the input required to create a partner.
/// </summary>
/// <param name="Request">Validated client request data.</param>
/// <param name="UserId">Identifier of the user.</param>
public sealed record CreatePartnerCommand(CreatePartnerRequest Request, Guid UserId)
    : ICommand<Result<PartnerResponse>>;

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
) : ICommandHandler<CreatePartnerCommand, Result<PartnerResponse>>
{
    /// <summary>
    /// Handles the request to create a partner.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains a partner on success, or an application error on failure.</returns>
    public async Task<Result<PartnerResponse>> HandleAsync(
        CreatePartnerCommand command,
        CancellationToken ct = default
    )
    {
        var request = command.Request;

        if (!await files.ExistsAsync(f => f.Id == request.ThumbnailId, ct))
        {
            return Error.BadRequest(ErrorCode.PartnerThumbnailNotFound);
        }

        var partner = new Partner
        {
            Name = request.Name.Trim(),
            FromDate = request.FromDate!.Value,
            Tier = request.Tier,
            Web = request.Website.NormalizeOrNull(),
            ThumbnailId = request.ThumbnailId,
            CreatedAt = clock.UtcNow,
            CreatedBy = command.UserId,
        };
        await partners.AddAsync(partner, ct);
        await uow.SaveChangesAsync(ct);
        await cacheInvalidator.InvalidateAsync(CacheTags.Partners);
        return partner.ToResponse();
    }
}
