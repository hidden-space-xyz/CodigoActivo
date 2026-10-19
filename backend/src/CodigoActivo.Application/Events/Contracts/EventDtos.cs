using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Domain.Events;

namespace CodigoActivo.Application.Events.Contracts;

/// <summary>
/// Contains the event data returned by the API.
/// </summary>
/// <param name="Id">Identifier of the target entity.</param>
/// <param name="Title">The title value.</param>
/// <param name="Subtitle">The subtitle value.</param>
/// <param name="Description">The description value.</param>
/// <param name="EventStartsAt">The event starts at value.</param>
/// <param name="EventEndsAt">The event ends at value.</param>
/// <param name="EarlySignupStartsAt">The early signup starts at value.</param>
/// <param name="SignupStartsAt">The signup starts at value.</param>
/// <param name="SignupEndsAt">The signup ends at value.</param>
/// <param name="CreatedAt">UTC timestamp when the record was created.</param>
/// <param name="UpdatedAt">UTC timestamp of the most recent update.</param>
/// <param name="ThumbnailId">Identifier of the thumbnail.</param>
/// <param name="Featured">Whether featured.</param>
/// <param name="Categories">The categories value.</param>
/// <param name="TermsDocuments">The terms documents linked to this event, ordered for display.</param>
/// <param name="Stage">Where the event stands, by the server clock, when the response is built.</param>
public record EventResponse(
    Guid Id,
    string Title,
    string Subtitle,
    string Description,
    DateOnly EventStartsAt,
    DateOnly EventEndsAt,
    DateTimeOffset? EarlySignupStartsAt,
    DateTimeOffset SignupStartsAt,
    DateTimeOffset SignupEndsAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    Guid ThumbnailId,
    bool Featured,
    IReadOnlyList<EventCategoryResponse> Categories,
    IReadOnlyList<EventTermsDocumentResponse> TermsDocuments,
    EventStage Stage
)
{
    /// <summary>
    /// Initializes an empty event response for serialization.
    /// </summary>
    public EventResponse()
        : this(
            Guid.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            default,
            default,
            null,
            default,
            default,
            default,
            null,
            Guid.Empty,
            false,
            [],
            [],
            default
        ) { }
}

/// <summary>
/// Contains the compact event list item data returned in list results.
/// </summary>
/// <param name="Id">Identifier of the target entity.</param>
/// <param name="Title">The title value.</param>
/// <param name="Subtitle">The subtitle value.</param>
/// <param name="EventStartsAt">The event starts at value.</param>
/// <param name="EventEndsAt">The event ends at value.</param>
/// <param name="EarlySignupStartsAt">The early signup starts at value.</param>
/// <param name="SignupStartsAt">The signup starts at value.</param>
/// <param name="SignupEndsAt">The signup ends at value.</param>
/// <param name="CreatedAt">UTC timestamp when the record was created.</param>
/// <param name="UpdatedAt">UTC timestamp of the most recent update.</param>
/// <param name="ThumbnailId">Identifier of the thumbnail.</param>
/// <param name="Featured">Whether featured.</param>
/// <param name="Categories">The categories value.</param>
/// <param name="Stage">Where the event stands, by the server clock, when the response is built.</param>
public record EventListItemResponse(
    Guid Id,
    string Title,
    string Subtitle,
    DateOnly EventStartsAt,
    DateOnly EventEndsAt,
    DateTimeOffset? EarlySignupStartsAt,
    DateTimeOffset SignupStartsAt,
    DateTimeOffset SignupEndsAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    Guid ThumbnailId,
    bool Featured,
    IReadOnlyList<EventCategoryResponse> Categories,
    EventStage Stage
)
{
    /// <summary>
    /// Initializes an empty event list item response for serialization.
    /// </summary>
    public EventListItemResponse()
        : this(
            Guid.Empty,
            string.Empty,
            string.Empty,
            default,
            default,
            null,
            default,
            default,
            default,
            null,
            Guid.Empty,
            false,
            [],
            default
        ) { }
}

/// <summary>
/// Contains the event category data returned by the API.
/// </summary>
/// <param name="CategoryTypeId">Identifier of the category type.</param>
/// <param name="Name">The name value.</param>
/// <param name="Color">The color value.</param>
public record EventCategoryResponse(Guid CategoryTypeId, string Name, string Color)
{
    /// <summary>
    /// Initializes an empty event category response for serialization.
    /// </summary>
    public EventCategoryResponse()
        : this(Guid.Empty, string.Empty, string.Empty) { }
}

/// <summary>
/// Contains the event terms document link data returned by the API.
/// </summary>
/// <param name="TermsDocumentId">Identifier of the terms document.</param>
/// <param name="Name">The name value.</param>
/// <param name="Required">Whether accepting the document is mandatory to complete the signup.</param>
/// <param name="DisplayOrder">The position in which the document is displayed.</param>
public record EventTermsDocumentResponse(
    Guid TermsDocumentId,
    string Name,
    bool Required,
    int DisplayOrder
)
{
    /// <summary>
    /// Initializes an empty event terms document response for serialization.
    /// </summary>
    public EventTermsDocumentResponse()
        : this(Guid.Empty, string.Empty, false, 0) { }
}

/// <summary>
/// Contains a terms document linked to an event as the signup shows it, returned by the API.
/// </summary>
/// <param name="TermsDocumentId">Identifier of the terms document.</param>
/// <param name="Name">The name value.</param>
/// <param name="Description">The description value.</param>
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
/// <param name="Decisions">Decisions taken for the member about the documents of the event.</param>
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
