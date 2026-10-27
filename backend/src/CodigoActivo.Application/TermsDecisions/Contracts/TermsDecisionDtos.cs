using CodigoActivo.Application.Abstractions.Querying.ReadModel;

namespace CodigoActivo.Application.TermsDecisions.Contracts;

/// <summary>
/// Contains a terms document linked to an event as the signup shows it, returned by the API.
/// </summary>
/// <param name="TermsDocumentId">Identifier of the terms document.</param>
/// <param name="Name">Name of the terms document.</param>
/// <param name="Description">Content of the document as rich text JSON.</param>
/// <param name="Kind">Whether the document holds conditions or a consent.</param>
/// <param name="Required">Whether accepting the document is mandatory to complete the signup; never for a consent.</param>
/// <param name="RequiresSignature">Whether accepting the document asks for a handwritten signature.</param>
/// <param name="DisplayOrder">The position in which the document is displayed.</param>
/// <param name="Version">Version of the document shown, which the decisions about it send back.</param>
public record EventTermsDocumentStateResponse(
    Guid TermsDocumentId,
    string Name,
    string Description,
    TermsDocumentKind Kind,
    bool Required,
    bool RequiresSignature,
    int DisplayOrder,
    int Version
);

/// <summary>
/// Contains a decision taken about a terms document of an event for a person, returned by the API.
/// </summary>
/// <param name="TermsDocumentId">Identifier of the terms document.</param>
/// <param name="Accepted">Whether the document was accepted.</param>
/// <param name="DecidedAt">When it was decided.</param>
public record EventTermsPersonDecisionResponse(
    Guid TermsDocumentId,
    bool Accepted,
    DateTimeOffset DecidedAt
);

/// <summary>
/// Contains the terms decisions taken for a member of the current user's household, returned by
/// the API.
/// </summary>
/// <param name="UserId">Identifier of the member: the current user or one of their dependents.</param>
/// <param name="FirstName">First name of the member.</param>
/// <param name="LastName">Last name of the member.</param>
/// <param name="DecidesOwnConsents">
/// Whether the member decides their own consents: the current user, and a dependent from the age
/// of 14. The current user decides everything else for their dependents.
/// </param>
/// <param name="Decisions">
/// Decisions taken for the member about the documents of the event, the oldest first.
/// </param>
public record EventTermsPersonStateResponse(
    Guid UserId,
    string FirstName,
    string LastName,
    bool DecidesOwnConsents,
    IReadOnlyList<EventTermsPersonDecisionResponse> Decisions
);

/// <summary>
/// Contains the terms state of an event for the current user's household, returned by the API.
/// </summary>
/// <param name="Documents">The documents linked to the event, in display order.</param>
/// <param name="People">The current user first and then their dependents, with the decisions taken for each.</param>
public record EventTermsStateResponse(
    IReadOnlyList<EventTermsDocumentStateResponse> Documents,
    IReadOnlyList<EventTermsPersonStateResponse> People
);

/// <summary>
/// Contains a decision someone took about a terms document of an event, as administrators list it.
/// </summary>
/// <param name="UserId">Identifier of the person the decision is about.</param>
/// <param name="FirstName">First name of the person.</param>
/// <param name="LastName">Last name of the person.</param>
/// <param name="Email">Email of the person, if any.</param>
/// <param name="DecidedById">Identifier of who decided: the person or their guardian.</param>
/// <param name="DecidedByFirstName">First name of who decided.</param>
/// <param name="DecidedByLastName">Last name of who decided.</param>
/// <param name="TermsDocumentId">Identifier of the terms document.</param>
/// <param name="DocumentName">Name of the terms document.</param>
/// <param name="DocumentKind">Whether the document holds conditions or a consent.</param>
/// <param name="DocumentVersion">Version of the document the decision was last taken on.</param>
/// <param name="Accepted">Whether the document is accepted now.</param>
/// <param name="Withdrawn">Whether it was accepted and later withdrawn.</param>
/// <param name="DecidedAt">When it was decided.</param>
/// <param name="HasSignature">Whether a handwritten signature was drawn to accept it.</param>
public record EventTermsDecisionResponse(
    Guid UserId,
    string FirstName,
    string LastName,
    string? Email,
    Guid DecidedById,
    string DecidedByFirstName,
    string DecidedByLastName,
    Guid TermsDocumentId,
    string DocumentName,
    TermsDocumentKind DocumentKind,
    int DocumentVersion,
    bool Accepted,
    bool Withdrawn,
    DateTimeOffset DecidedAt,
    bool HasSignature
)
{
    /// <summary>
    /// Initializes an empty event terms decision response for serialization.
    /// </summary>
    public EventTermsDecisionResponse()
        : this(
            Guid.Empty,
            string.Empty,
            string.Empty,
            null,
            Guid.Empty,
            string.Empty,
            string.Empty,
            Guid.Empty,
            string.Empty,
            TermsDocumentKind.Conditions,
            0,
            false,
            false,
            default,
            false
        ) { }
}
