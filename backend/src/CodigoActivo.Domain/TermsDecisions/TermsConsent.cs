using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.TermsDocuments;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Domain.TermsDecisions;

/// <summary>
/// Applies the terms decisions taken for one person to the documents of an event. Each document is
/// decided by whoever <see cref="DeciderOf"/> names: the conditions by the account holder, so by
/// the guardian for a dependent, and the consents by the person from the age of 14 and by their
/// guardian before. Decisions about documents the event does not link are ignored, a declined
/// document can be accepted later, and declining a required document is not recorded, so it can
/// still be accepted to sign up; a consent is never required. Accepting a document that asks for a
/// signature needs one drawn; other documents keep none.
/// </summary>
public static class TermsConsent
{
    /// <summary>
    /// Applies the decisions and tells whether every required document ends up accepted. Nothing
    /// changes when a decision is refused.
    /// </summary>
    /// <param name="eventId">Identifier of the event.</param>
    /// <param name="person">Person the decisions are about.</param>
    /// <param name="documents">Terms documents linked to the event.</param>
    /// <param name="signedDocumentIds">Linked documents whose acceptance asks for a signature.</param>
    /// <param name="consentDocumentIds">Linked documents that hold a consent.</param>
    /// <param name="documentVersions">Current version of each linked document.</param>
    /// <param name="acceptances">Decisions already taken about them for the person.</param>
    /// <param name="decisions">New decisions, if any.</param>
    /// <param name="recordedById">Identifier of the account that records the decisions.</param>
    /// <param name="today">Local day on which the age of the person is evaluated.</param>
    /// <param name="now">Current time.</param>
    /// <returns>
    /// The decisions to record and whether a required document is still not accepted, or
    /// <see cref="DomainErrorCode.EventTermsSignatureRequired"/> or
    /// <see cref="DomainErrorCode.EventTermsSignatureInvalid"/> when a document that asks for a
    /// signature is accepted without a valid one, or
    /// <see cref="DomainErrorCode.EventTermsDocumentChanged"/> when a decision names a version of its
    /// document that is no longer current.
    /// </returns>
    public static Result<TermsConsentOutcome> Apply(
        EventId eventId,
        User person,
        IReadOnlyCollection<EventTermsDocument> documents,
        IReadOnlyCollection<TermsDocumentId> signedDocumentIds,
        IReadOnlyCollection<TermsDocumentId> consentDocumentIds,
        IReadOnlyDictionary<TermsDocumentId, int> documentVersions,
        IReadOnlyCollection<EventTermsAcceptance> acceptances,
        IReadOnlyList<TermsDecision>? decisions,
        UserId recordedById,
        DateOnly today,
        DateTimeOffset now
    )
    {
        ArgumentNullException.ThrowIfNull(person);
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(signedDocumentIds);
        ArgumentNullException.ThrowIfNull(consentDocumentIds);
        ArgumentNullException.ThrowIfNull(documentVersions);
        ArgumentNullException.ThrowIfNull(acceptances);

        var acceptanceByDocument = acceptances.ToDictionary(a => a.TermsDocumentId);
        var requiredById = documents.ToDictionary(
            d => d.TermsDocumentId,
            d => IsRequired(d, consentDocumentIds)
        );
        var linked = (decisions ?? [])
            .Where(decision => requiredById.ContainsKey(decision.TermsDocumentId))
            .ToList();
        if (
            linked.Exists(decision =>
                decision.Version is { } version
                && version != documentVersions[decision.TermsDocumentId]
            )
        )
        {
            return Error.Conflict(DomainErrorCode.EventTermsDocumentChanged);
        }

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
            var decidedById = DeciderOf(
                person,
                consentDocumentIds.Contains(decision.TermsDocumentId),
                today
            );
            if (acceptanceByDocument.TryGetValue(decision.TermsDocumentId, out var existing))
            {
                if (!existing.Accepted && decision.Accepted)
                {
                    existing.Accept(
                        decidedById,
                        documentVersions[decision.TermsDocumentId],
                        signature,
                        recordedById,
                        now
                    );
                }

                continue;
            }

            if (requiredById[decision.TermsDocumentId] && !decision.Accepted)
            {
                continue;
            }

            var acceptance = EventTermsAcceptance.Record(
                eventId,
                person.Id,
                decidedById,
                decision.TermsDocumentId,
                documentVersions[decision.TermsDocumentId],
                decision.Accepted,
                signature,
                recordedById,
                now
            );
            recorded.Add(acceptance);
            acceptanceByDocument[decision.TermsDocumentId] = acceptance;
        }

        var missingRequired = documents.Any(document =>
            requiredById[document.TermsDocumentId]
            && (
                !acceptanceByDocument.TryGetValue(document.TermsDocumentId, out var acceptance)
                || !acceptance.Accepted
            )
        );
        return new TermsConsentOutcome(recorded, missingRequired);
    }

    /// <summary>
    /// Names who decides a terms document for a person: the person, except for a dependent, whose
    /// guardian decides the conditions and, before the age of 14, the consents.
    /// </summary>
    /// <param name="person">Person the decision is about.</param>
    /// <param name="consent">Whether the document holds a consent.</param>
    /// <param name="today">Local day on which the age of the person is evaluated.</param>
    /// <returns>The identifier of whoever decides.</returns>
    public static UserId DeciderOf(User person, bool consent, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(person);

        return person.ParentId is { } guardianId && !(consent && person.GivesOwnConsent(today))
            ? guardianId
            : person.Id;
    }

    /// <summary>
    /// Tells whether a document linked to an event must be accepted to sign up: the event requires
    /// it and it is not a consent.
    /// </summary>
    /// <param name="document">Document linked to the event.</param>
    /// <param name="consentDocumentIds">Linked documents that hold a consent.</param>
    /// <returns><see langword="true"/> when the document must be accepted.</returns>
    public static bool IsRequired(
        EventTermsDocument document,
        IReadOnlyCollection<TermsDocumentId> consentDocumentIds
    )
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(consentDocumentIds);

        return document.IsRequired && !consentDocumentIds.Contains(document.TermsDocumentId);
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
