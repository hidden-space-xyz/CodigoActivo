using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Activities;

/// <summary>
/// Records the terms decisions supplied for a signup and checks that the required documents of
/// the event end up accepted, following <see cref="TermsConsent"/>.
/// </summary>
/// <param name="events">Repository used to persist and retrieve events.</param>
/// <param name="termsAcceptances">Repository of the terms decisions people took.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class TermsGate(
    IEventRepository events,
    IEventTermsAcceptanceRepository termsAcceptances,
    IClock clock
)
{
    /// <summary>
    /// Ensures that every terms document required by the event has been accepted by the user,
    /// staging the new decisions. Nothing is persisted by this method: callers must call
    /// <see cref="IUnitOfWork.SaveChangesAsync"/> only after every other check in the same use case
    /// also succeeds.
    /// </summary>
    /// <param name="eventId">Identifier of the event of the activity signed up to.</param>
    /// <param name="userId">Identifier of the user who decides.</param>
    /// <param name="decisions">Terms decisions supplied with the signup.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result reports success, or an application error on failure.</returns>
    public async Task<Result> EnsureDecidedAsync(
        EventId eventId,
        UserId userId,
        IReadOnlyList<TermsDecision>? decisions,
        CancellationToken ct
    )
    {
        var ev = await events.GetByIdAsync(eventId, ct);
        if (ev is null || ev.TermsDocuments.Count is 0)
        {
            return Result.Success();
        }

        var outcome = TermsConsent.Apply(
            ev.Id,
            userId,
            ev.TermsDocuments,
            await termsAcceptances.ListAsync(ev.Id, userId, ct),
            decisions,
            clock.UtcNow
        );
        foreach (var acceptance in outcome.Recorded)
        {
            await termsAcceptances.AddAsync(acceptance, ct);
        }

        return outcome.MissingRequired
            ? Error.Validation(ApplicationErrorCode.EventTermsAcceptanceRequired)
            : Result.Success();
    }
}
