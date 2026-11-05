using CodigoActivo.Application.Abstractions.Auditing;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Domain.BlockedData;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.BlockedData.Commands;

/// <summary>
/// Carries the input required to keep the data history of an account past its retention while a
/// claim about it is open.
/// </summary>
/// <param name="ConsultationId">Identifier of the open consultation the claim is for.</param>
/// <param name="AccountId">
/// Identifier of an account a search of the consultation found, left out of the audit log so only
/// the privacy officer knows whose blocked data is held.
/// </param>
public sealed record PlaceBlockedDataHoldCommand(
    BlockedDataConsultationId ConsultationId,
    [property: Unaudited] UserId AccountId
) : ICommand<Result<BlockedDataHoldId>>;

/// <summary>
/// Executes the command that places a hold on the data history of an account a search of the
/// consultation found and of its minors, existing and erased, with the authority and the
/// reference of the consultation, so the purge keeps it until the hold is lifted.
/// </summary>
/// <param name="officer">Policy that tells who may consult the blocked data.</param>
/// <param name="consultations">Repository of the consultations.</param>
/// <param name="holds">Repository of the holds.</param>
/// <param name="minors">Finder of the minors of the account.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class PlaceBlockedDataHoldCommandHandler(
    PrivacyOfficer officer,
    IBlockedDataConsultationRepository consultations,
    IBlockedDataHoldRepository holds,
    BlockedDataMinors minors,
    IClock clock
) : ICommandHandler<PlaceBlockedDataHoldCommand, Result<BlockedDataHoldId>>
{
    /// <summary>
    /// Handles the request to place a hold on the data history of an account.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result carries the identifier of the hold;
    /// <see cref="ApplicationErrorCode.BlockedDataOfficerOnly"/> for any other account,
    /// <see cref="DomainErrorCode.BlockedDataConsultationNotFound"/> or
    /// <see cref="DomainErrorCode.BlockedDataConsultationExpired"/> when the consultation may not be
    /// used, or <see cref="DomainErrorCode.BlockedDataAccountNotFound"/> when none of its searches
    /// found the account.
    /// </returns>
    public async Task<Result<BlockedDataHoldId>> HandleAsync(
        PlaceBlockedDataHoldCommand command,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var signedIn = await officer.SignedInOfficerAsync(ct);
        if (signedIn.IsFailure)
        {
            return signedIn.Error!;
        }

        var now = clock.UtcNow;
        var opened = ConsultationAccess.Check(
            await consultations.GetByIdAsync(command.ConsultationId, ct),
            signedIn.Value,
            now
        );
        if (opened.IsFailure)
        {
            return opened.Error!;
        }

        var consultation = opened.Value;
        if (!consultation.Found(command.AccountId))
        {
            return Error.NotFound(DomainErrorCode.BlockedDataAccountNotFound);
        }

        var minorIds = await minors.OfAsync(command.AccountId.Value, now, ct);
        var placed = BlockedDataHold.Place(
            consultation,
            command.AccountId,
            minorIds.Select(UserId.From),
            now
        );
        if (placed.IsFailure)
        {
            return placed.Error!;
        }

        await holds.AddAsync(placed.Value, ct);
        return placed.Value.Id;
    }
}
