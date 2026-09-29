namespace CodigoActivo.Domain.Events;

/// <summary>
/// Applies the terms decisions of a person to the documents of an event. Decisions about
/// documents the event does not link are ignored, a declined document can be accepted later, and
/// declining a required document is not recorded, so the person can still accept it and sign up.
/// </summary>
public static class TermsConsent
{
    /// <summary>
    /// Applies the decisions and tells whether every required document ends up accepted.
    /// </summary>
    /// <param name="eventId">Identifier of the event.</param>
    /// <param name="userId">Identifier of the person who decides.</param>
    /// <param name="documents">Terms documents linked to the event.</param>
    /// <param name="acceptances">Decisions the person already took about them.</param>
    /// <param name="decisions">New decisions, if any.</param>
    /// <param name="now">Current time.</param>
    /// <returns>The decisions to record and whether a required document is still not accepted.</returns>
    public static TermsConsentOutcome Apply(
        Guid eventId,
        Guid userId,
        IReadOnlyCollection<EventTermsDocument> documents,
        IReadOnlyCollection<EventTermsAcceptance> acceptances,
        IReadOnlyList<TermsDecision>? decisions,
        DateTimeOffset now
    )
    {
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(acceptances);

        var acceptanceByDocument = acceptances.ToDictionary(a => a.TermsDocumentId);
        var recorded = new List<EventTermsAcceptance>();
        var requiredById = documents.ToDictionary(d => d.TermsDocumentId, d => d.IsRequired);
        foreach (var decision in decisions ?? [])
        {
            if (!requiredById.TryGetValue(decision.TermsDocumentId, out var isRequired))
            {
                continue;
            }

            if (acceptanceByDocument.TryGetValue(decision.TermsDocumentId, out var existing))
            {
                if (!existing.Accepted && decision.Accepted)
                {
                    existing.Accept(now);
                }

                continue;
            }

            if (isRequired && !decision.Accepted)
            {
                continue;
            }

            var acceptance = EventTermsAcceptance.Record(
                eventId,
                userId,
                decision.TermsDocumentId,
                decision.Accepted,
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
}
