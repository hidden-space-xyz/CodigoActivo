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
