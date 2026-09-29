namespace CodigoActivo.Domain.Events;

/// <summary>
/// Result of applying terms decisions.
/// </summary>
/// <param name="Recorded">New decisions to store.</param>
/// <param name="MissingRequired">Whether a required document is still not accepted.</param>
public sealed record TermsConsentOutcome(
    IReadOnlyList<EventTermsAcceptance> Recorded,
    bool MissingRequired
);
