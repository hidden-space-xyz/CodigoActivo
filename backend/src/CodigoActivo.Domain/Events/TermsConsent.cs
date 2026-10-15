using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.TermsDocuments;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Domain.Events;

/// <summary>
/// Applies the terms decisions of a person to the documents of an event. Decisions about
/// documents the event does not link are ignored, a declined document can be accepted later, and
/// declining a required document is not recorded, so the person can still accept it and sign up.
/// Accepting a document that asks for a signature needs one drawn; other documents keep none.
/// </summary>
public static class TermsConsent
{
    /// <summary>
    /// Applies the decisions and tells whether every required document ends up accepted. Nothing
    /// changes when a decision is refused.
    /// </summary>
    /// <param name="eventId">Identifier of the event.</param>
    /// <param name="userId">Identifier of the person who decides.</param>
    /// <param name="documents">Terms documents linked to the event.</param>
    /// <param name="signedDocumentIds">Linked documents whose acceptance asks for a signature.</param>
    /// <param name="acceptances">Decisions the person already took about them.</param>
    /// <param name="decisions">New decisions, if any.</param>
    /// <param name="now">Current time.</param>
    /// <returns>
    /// The decisions to record and whether a required document is still not accepted, or
    /// <see cref="DomainErrorCode.EventTermsSignatureRequired"/> or
    /// <see cref="DomainErrorCode.EventTermsSignatureInvalid"/> when a document that asks for a
    /// signature is accepted without a valid one.
    /// </returns>
    public static Result<TermsConsentOutcome> Apply(
        EventId eventId,
        UserId userId,
        IReadOnlyCollection<EventTermsDocument> documents,
        IReadOnlyCollection<TermsDocumentId> signedDocumentIds,
        IReadOnlyCollection<EventTermsAcceptance> acceptances,
        IReadOnlyList<TermsDecision>? decisions,
        DateTimeOffset now
    )
    {
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(signedDocumentIds);
        ArgumentNullException.ThrowIfNull(acceptances);

        var acceptanceByDocument = acceptances.ToDictionary(a => a.TermsDocumentId);
        var requiredById = documents.ToDictionary(d => d.TermsDocumentId, d => d.IsRequired);
        var linked = (decisions ?? [])
            .Where(decision => requiredById.ContainsKey(decision.TermsDocumentId))
            .ToList();
        var signatures = Signatures(linked, signedDocumentIds, acceptanceByDocument);
        if (signatures.IsFailure)
        {
            return signatures.Error!;
        }

        var recorded = new List<EventTermsAcceptance>();
        for (var index = 0; index < linked.Count; index++)
        {
            var decision = linked[index];
            var signature = signatures.Value[index];
            if (acceptanceByDocument.TryGetValue(decision.TermsDocumentId, out var existing))
            {
                if (!existing.Accepted && decision.Accepted)
                {
                    existing.Accept(signature, now);
                }

                continue;
            }

            if (requiredById[decision.TermsDocumentId] && !decision.Accepted)
            {
                continue;
            }

            var acceptance = EventTermsAcceptance.Record(
                eventId,
                userId,
                decision.TermsDocumentId,
                decision.Accepted,
                signature,
                now
            );
            recorded.Add(acceptance);
            acceptanceByDocument[decision.TermsDocumentId] = acceptance;
        }

        var missingRequired = documents.Any(document =>
            document.IsRequired
            && (
                !acceptanceByDocument.TryGetValue(document.TermsDocumentId, out var acceptance)
                || !acceptance.Accepted
            )
        );
        return new TermsConsentOutcome(recorded, missingRequired);
    }

    private static Result<HandwrittenSignature?[]> Signatures(
        List<TermsDecision> decisions,
        IReadOnlyCollection<TermsDocumentId> signedDocumentIds,
        Dictionary<TermsDocumentId, EventTermsAcceptance> acceptanceByDocument
    )
    {
        var signatures = new HandwrittenSignature?[decisions.Count];
        for (var index = 0; index < decisions.Count; index++)
        {
            var decision = decisions[index];
            var alreadyAccepted =
                acceptanceByDocument.TryGetValue(decision.TermsDocumentId, out var existing)
                && existing.Accepted;
            if (
                !decision.Accepted
                || alreadyAccepted
                || !signedDocumentIds.Contains(decision.TermsDocumentId)
            )
            {
                continue;
            }

            if (decision.Signature.IsEmpty)
            {
                return Error.Validation(DomainErrorCode.EventTermsSignatureRequired);
            }

            var signature = HandwrittenSignature.Create(decision.Signature.Span);
            if (signature.IsFailure)
            {
                return signature.Error!;
            }

            signatures[index] = signature.Value;
        }

        return signatures;
    }
}
