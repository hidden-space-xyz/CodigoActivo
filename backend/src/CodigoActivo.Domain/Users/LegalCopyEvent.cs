namespace CodigoActivo.Domain.Users;

/// <summary>
/// Event the household took part in or decided terms for, with those decisions and signups.
/// </summary>
/// <param name="Id">Identifier of the event.</param>
/// <param name="Title">Title of the event.</param>
/// <param name="StartsOn">First day of the event.</param>
/// <param name="EndsOn">Last day of the event.</param>
/// <param name="TermsDecisions">Decisions on the terms of the event, oldest first.</param>
/// <param name="Activities">Signups to activities of the event, earliest activity first.</param>
public sealed record LegalCopyEvent(
    Guid Id,
    string Title,
    DateOnly StartsOn,
    DateOnly EndsOn,
    IReadOnlyList<LegalCopyDecision> TermsDecisions,
    IReadOnlyList<LegalCopyActivity> Activities
);
