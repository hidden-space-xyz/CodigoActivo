namespace CodigoActivo.Application.Participation.Contracts;

/// <summary>
/// Contains a consent decided about a terms document of an event for the signed-in user or one of
/// their dependents, returned by the API.
/// </summary>
/// <param name="EventId">Identifier of the event.</param>
/// <param name="EventTitle">Title of the event.</param>
/// <param name="EventStartsAt">First day of the event.</param>
/// <param name="TermsDocumentId">Identifier of the terms document that holds the consent.</param>
/// <param name="DocumentName">Name of the terms document.</param>
/// <param name="PersonId">Identifier of the person the consent is about.</param>
/// <param name="PersonName">Full name of the person the consent is about.</param>
/// <param name="Accepted">Whether the consent is given now.</param>
/// <param name="Withdrawn">Whether it was given and later withdrawn.</param>
/// <param name="DecidedAt">When it was last decided.</param>
/// <param name="DecidedByName">Full name of who last decided: the person or their guardian.</param>
public sealed record MyConsentResponse(
    Guid EventId,
    string EventTitle,
    DateOnly EventStartsAt,
    Guid TermsDocumentId,
    string DocumentName,
    Guid PersonId,
    string PersonName,
    bool Accepted,
    bool Withdrawn,
    DateTimeOffset DecidedAt,
    string DecidedByName
);
