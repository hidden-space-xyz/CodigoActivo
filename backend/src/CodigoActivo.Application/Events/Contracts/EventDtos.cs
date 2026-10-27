using CodigoActivo.Domain.Events;

namespace CodigoActivo.Application.Events.Contracts;

/// <summary>
/// Contains the event data returned by the API.
/// </summary>
/// <param name="Id">Identifier of the event.</param>
/// <param name="Title">Title of the event.</param>
/// <param name="Subtitle">Line shown below the title.</param>
/// <param name="Description">Body as rich text JSON.</param>
/// <param name="EventStartsAt">First day of the event.</param>
/// <param name="EventEndsAt">Last day of the event.</param>
/// <param name="EarlySignupStartsAt">
/// When the early signup opens, in UTC, or <see langword="null"/> when there is none.
/// </param>
/// <param name="SignupStartsAt">When the signup opens, in UTC.</param>
/// <param name="SignupEndsAt">When the signup closes, in UTC.</param>
/// <param name="CreatedAt">UTC timestamp when the record was created.</param>
/// <param name="UpdatedAt">UTC timestamp of the most recent update.</param>
/// <param name="ThumbnailId">Identifier of the thumbnail file.</param>
/// <param name="Featured">Whether this is the featured event.</param>
/// <param name="Categories">Categories that tag the event.</param>
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
/// <param name="Id">Identifier of the event.</param>
/// <param name="Title">Title of the event.</param>
/// <param name="Subtitle">Line shown below the title.</param>
/// <param name="EventStartsAt">First day of the event.</param>
/// <param name="EventEndsAt">Last day of the event.</param>
/// <param name="EarlySignupStartsAt">
/// When the early signup opens, in UTC, or <see langword="null"/> when there is none.
/// </param>
/// <param name="SignupStartsAt">When the signup opens, in UTC.</param>
/// <param name="SignupEndsAt">When the signup closes, in UTC.</param>
/// <param name="CreatedAt">UTC timestamp when the record was created.</param>
/// <param name="UpdatedAt">UTC timestamp of the most recent update.</param>
/// <param name="ThumbnailId">Identifier of the thumbnail file.</param>
/// <param name="Featured">Whether this is the featured event.</param>
/// <param name="Categories">Categories that tag the event.</param>
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
/// <param name="Name">Display name of the category.</param>
/// <param name="Color">Color the category is shown with.</param>
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
/// <param name="Name">Name of the terms document.</param>
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
