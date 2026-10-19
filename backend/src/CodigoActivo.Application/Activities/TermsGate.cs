using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.TermsDocuments;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Activities;

/// <summary>
/// Records the terms decisions supplied for a signup, person by person, and checks that the
/// required documents of the event end up accepted for each of them, following
/// <see cref="TermsConsent"/>.
/// </summary>
/// <param name="events">Repository used to persist and retrieve events.</param>
/// <param name="termsDocuments">Repository that tells which documents ask for a signature and which hold a consent.</param>
/// <param name="termsAcceptances">Repository of the terms decisions taken.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class TermsGate(
    IEventRepository events,
    ITermsDocumentRepository termsDocuments,
    IEventTermsAcceptanceRepository termsAcceptances,
    IClock clock
)
{
    /// <summary>
    /// Ensures that every terms document required by the event has been accepted for each person
    /// signed up, staging the new decisions. Nothing is persisted by this method: callers must call
    /// <see cref="IUnitOfWork.SaveChangesAsync"/> only after every other check in the same use case
    /// also succeeds.
    /// </summary>
    /// <param name="eventId">Identifier of the event of the activity signed up to.</param>
    /// <param name="signups">People signed up, each with the decisions supplied for them.</param>
    /// <param name="recordedById">Identifier of the account that signs them up.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result reports success, or an application error on failure.</returns>
    public async Task<Result> EnsureDecidedAsync(
        EventId eventId,
        IReadOnlyList<PersonTermsDecisions> signups,
        UserId recordedById,
        CancellationToken ct
    )
    {
        ArgumentNullException.ThrowIfNull(signups);

        var ev = await events.GetByIdAsync(eventId, ct);
        if (ev is null || ev.TermsDocuments.Count is 0 || signups.Count is 0)
        {
            return Result.Success();
        }

        IReadOnlyCollection<TermsDocumentId> linkedIds =
        [
            .. ev.TermsDocuments.Select(document => document.TermsDocumentId),
        ];
        var signedDocumentIds = signups.Any(signup => signup.Decisions is { Count: > 0 })
            ? await termsDocuments.ListRequiringSignatureAsync(linkedIds, ct)
            : [];
        var consentDocumentIds = await termsDocuments.ListConsentsAsync(linkedIds, ct);

        var missingRequired = false;
        foreach (var signup in signups)
        {
            var outcome = TermsConsent.Apply(
                ev.Id,
                signup.Person,
                ev.TermsDocuments,
                signedDocumentIds,
                consentDocumentIds,
                await termsAcceptances.ListAsync(ev.Id, signup.Person.Id, ct),
                signup.Decisions,
                recordedById,
                clock.Today,
                clock.UtcNow
            );
            if (outcome.IsFailure)
            {
                return outcome.Error!;
            }

            foreach (var acceptance in outcome.Value.Recorded)
            {
                await termsAcceptances.AddAsync(acceptance, ct);
            }

            missingRequired |= outcome.Value.MissingRequired;
        }

        return missingRequired
            ? Error.Validation(ApplicationErrorCode.EventTermsAcceptanceRequired)
            : Result.Success();
    }
}
