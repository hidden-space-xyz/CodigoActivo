using CodigoActivo.Domain.BlockedData;

namespace CodigoActivo.Application.BlockedData.Commands;

/// <summary>
/// Carries the input required to lift a hold on the blocked data once its claim is closed.
/// </summary>
/// <param name="ConsultationId">Identifier of the open consultation that lifts it.</param>
/// <param name="HoldId">Identifier of the hold.</param>
public sealed record LiftBlockedDataHoldCommand(
    BlockedDataConsultationId ConsultationId,
    BlockedDataHoldId HoldId
) : ICommand<Result>;

/// <summary>
/// Executes the command that lifts a hold on the blocked data, so the retention of the history it
/// kept applies again. Lifting lets the purge destroy what the claim needed, so it takes an open
/// consultation, confirmed with the password and the second factor and announced to the legal
/// contact, like reading the history.
/// </summary>
/// <param name="officer">Policy that tells who may consult the blocked data.</param>
/// <param name="consultations">Repository of the consultations.</param>
/// <param name="holds">Repository of the holds.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class LiftBlockedDataHoldCommandHandler(
    PrivacyOfficer officer,
    IBlockedDataConsultationRepository consultations,
    IBlockedDataHoldRepository holds,
    IClock clock
) : ICommandHandler<LiftBlockedDataHoldCommand, Result>
{
    /// <summary>
    /// Handles the request to lift a hold on the blocked data.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result is success;
    /// <see cref="ApplicationErrorCode.BlockedDataOfficerOnly"/> for any other account,
    /// <see cref="DomainErrorCode.BlockedDataConsultationNotFound"/> or
    /// <see cref="DomainErrorCode.BlockedDataConsultationExpired"/> when the consultation may not
    /// be used, <see cref="DomainErrorCode.BlockedDataHoldNotFound"/> for a hold that does not
    /// exist, or <see cref="DomainErrorCode.BlockedDataHoldLifted"/> when it was already lifted.
    /// </returns>
    public async Task<Result> HandleAsync(
        LiftBlockedDataHoldCommand command,
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
        var consultation = ConsultationAccess.Check(
            await consultations.GetByIdAsync(command.ConsultationId, ct),
            signedIn.Value,
            now
        );
        if (consultation.IsFailure)
        {
            return consultation.Error!;
        }

        var hold = await holds.GetByIdAsync(command.HoldId, ct);
        if (hold is null)
        {
            return Error.NotFound(DomainErrorCode.BlockedDataHoldNotFound);
        }

        return hold.Lift(signedIn.Value.Id, now);
    }
}
