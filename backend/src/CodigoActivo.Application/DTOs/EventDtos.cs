using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Validation;

namespace CodigoActivo.Application.DTOs;

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
/// <param name="CreatedBy">The created by value.</param>
/// <param name="UpdatedBy">The updated by value.</param>
/// <param name="ThumbnailId">Identifier of the thumbnail.</param>
/// <param name="Featured">Whether featured.</param>
/// <param name="Categories">The categories value.</param>
/// <param name="TermsDocuments">The terms documents linked to this event, ordered for display.</param>
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
    Guid CreatedBy,
    Guid? UpdatedBy,
    Guid ThumbnailId,
    bool Featured,
    IReadOnlyList<EventCategoryResponse> Categories,
    IReadOnlyList<EventTermsDocumentResponse> TermsDocuments
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
            null,
            Guid.Empty,
            false,
            [],
            []
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
/// <param name="CreatedBy">The created by value.</param>
/// <param name="UpdatedBy">The updated by value.</param>
/// <param name="ThumbnailId">Identifier of the thumbnail.</param>
/// <param name="Featured">Whether featured.</param>
/// <param name="Categories">The categories value.</param>
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
    Guid CreatedBy,
    Guid? UpdatedBy,
    Guid ThumbnailId,
    bool Featured,
    IReadOnlyList<EventCategoryResponse> Categories
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
            null,
            Guid.Empty,
            false,
            []
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
/// Contains the client-supplied data used to create an event.
/// </summary>
/// <param name="Title">The title value.</param>
/// <param name="Subtitle">The subtitle value.</param>
/// <param name="Description">The description value.</param>
/// <param name="EventStartsAt">The event starts at value.</param>
/// <param name="EventEndsAt">The event ends at value.</param>
/// <param name="EarlySignupStartsAt">The early signup starts at value.</param>
/// <param name="SignupStartsAt">The signup starts at value.</param>
/// <param name="SignupEndsAt">The signup ends at value.</param>
/// <param name="ThumbnailId">Identifier of the thumbnail.</param>
/// <param name="CategoryTypeIds">Identifiers of the category type items.</param>
/// <param name="TermsDocuments">The requested terms document links, in display order.</param>
public record CreateEventRequest(
    [Required] [MaxLength(200)] [NotBlank] string Title,
    [Required] [MaxLength(300)] [NotBlank] string Subtitle,
    [JsonString] [MaxLength(262144)] string Description,
    [Required] DateOnly? EventStartsAt,
    [Required] DateOnly? EventEndsAt,
    DateTimeOffset? EarlySignupStartsAt,
    [Required] DateTimeOffset? SignupStartsAt,
    [Required] DateTimeOffset? SignupEndsAt,
    Guid ThumbnailId,
    IReadOnlyList<Guid>? CategoryTypeIds,
    IReadOnlyList<EventTermsDocumentRequest>? TermsDocuments
);

/// <summary>
/// Contains the client-supplied data used to update the event.
/// </summary>
/// <param name="Title">The title value.</param>
/// <param name="Subtitle">The subtitle value.</param>
/// <param name="Description">The description value.</param>
/// <param name="EventStartsAt">The event starts at value.</param>
/// <param name="EventEndsAt">The event ends at value.</param>
/// <param name="EarlySignupStartsAt">The early signup starts at value.</param>
/// <param name="SignupStartsAt">The signup starts at value.</param>
/// <param name="SignupEndsAt">The signup ends at value.</param>
/// <param name="ThumbnailId">Identifier of the thumbnail.</param>
/// <param name="CategoryTypeIds">Identifiers of the category type items.</param>
/// <param name="TermsDocuments">The requested terms document links, in display order.</param>
public record UpdateEventRequest(
    [Required] [MaxLength(200)] [NotBlank] string Title,
    [Required] [MaxLength(300)] [NotBlank] string Subtitle,
    [JsonString] [MaxLength(262144)] string Description,
    [Required] DateOnly? EventStartsAt,
    [Required] DateOnly? EventEndsAt,
    DateTimeOffset? EarlySignupStartsAt,
    [Required] DateTimeOffset? SignupStartsAt,
    [Required] DateTimeOffset? SignupEndsAt,
    Guid ThumbnailId,
    IReadOnlyList<Guid>? CategoryTypeIds,
    IReadOnlyList<EventTermsDocumentRequest>? TermsDocuments
);

/// <summary>
/// Contains the event category type data returned by the API.
/// </summary>
/// <param name="Id">Identifier of the target entity.</param>
/// <param name="Name">The name value.</param>
/// <param name="Color">The color value.</param>
public record EventCategoryTypeResponse(Guid Id, string Name, string Color)
{
    /// <summary>
    /// Initializes an empty event category type response for serialization.
    /// </summary>
    public EventCategoryTypeResponse()
        : this(Guid.Empty, string.Empty, string.Empty) { }
}

/// <summary>
/// Contains the client-supplied data used to create an event category type.
/// </summary>
/// <param name="Name">The name value.</param>
/// <param name="Color">The color value.</param>
public record CreateEventCategoryTypeRequest(
    [Required] [MaxLength(120)] [NotBlank] string Name,
    [Required] [MaxLength(9)] [RegularExpression("^#[0-9A-Fa-f]{6}$")] string Color
);

/// <summary>
/// Contains the client-supplied data used to update the event category type.
/// </summary>
/// <param name="Name">The name value.</param>
/// <param name="Color">The color value.</param>
public record UpdateEventCategoryTypeRequest(
    [Required] [MaxLength(120)] [NotBlank] string Name,
    [Required] [MaxLength(9)] [RegularExpression("^#[0-9A-Fa-f]{6}$")] string Color
);

/// <summary>
/// Contains the terms document data returned by the API.
/// </summary>
/// <param name="Id">Identifier of the target entity.</param>
/// <param name="Name">The name value.</param>
/// <param name="Description">The description value.</param>
public record TermsDocumentResponse(Guid Id, string Name, string Description)
{
    /// <summary>
    /// Initializes an empty terms document response for serialization.
    /// </summary>
    public TermsDocumentResponse()
        : this(Guid.Empty, string.Empty, string.Empty) { }
}

/// <summary>
/// Contains the client-supplied data used to create a terms document.
/// </summary>
/// <param name="Name">The name value.</param>
/// <param name="Description">The description value.</param>
public record CreateTermsDocumentRequest(
    [Required] [MaxLength(120)] [NotBlank] string Name,
    [Required] [JsonString] [MaxLength(262144)] string Description
);

/// <summary>
/// Contains the client-supplied data used to update the terms document.
/// </summary>
/// <param name="Name">The name value.</param>
/// <param name="Description">The description value.</param>
public record UpdateTermsDocumentRequest(
    [Required] [MaxLength(120)] [NotBlank] string Name,
    [Required] [JsonString] [MaxLength(262144)] string Description
);

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
/// Contains the client-supplied data used to link a terms document to an event.
/// </summary>
/// <param name="TermsDocumentId">Identifier of the terms document.</param>
/// <param name="Required">Whether accepting the document is mandatory to complete the signup.</param>
public record EventTermsDocumentRequest(
    [Required] Guid TermsDocumentId,
    bool Required = false
);

/// <summary>
/// Contains the per-user state of a terms document linked to an event, returned by the API.
/// </summary>
/// <param name="TermsDocumentId">Identifier of the terms document.</param>
/// <param name="Name">The name value.</param>
/// <param name="Description">The description value.</param>
/// <param name="Required">Whether accepting the document is mandatory to complete the signup.</param>
/// <param name="DisplayOrder">The position in which the document is displayed.</param>
/// <param name="Accepted">Whether the current user accepted the document, or <see langword="null"/> when undecided.</param>
/// <param name="DecidedAt">When the current user decided, or <see langword="null"/> when undecided.</param>
public record EventTermsDocumentStateResponse(
    Guid TermsDocumentId,
    string Name,
    string Description,
    bool Required,
    int DisplayOrder,
    bool? Accepted,
    DateTimeOffset? DecidedAt
);

/// <summary>
/// Contains the current user's terms state for an event, returned by the API.
/// </summary>
/// <param name="Documents">The documents linked to the event, with the current user's decision.</param>
/// <param name="SignupBlocked">Whether at least one required document is still undecided or rejected.</param>
public record EventTermsStateResponse(
    IReadOnlyList<EventTermsDocumentStateResponse> Documents,
    bool SignupBlocked
);

/// <summary>
/// Contains the activity role type data used to label a signup statistics column, returned by
/// the API.
/// </summary>
/// <param name="Id">Identifier of the target entity.</param>
/// <param name="Name">The name value.</param>
public record EventSignupStatsRoleResponse(Guid Id, string Name);

/// <summary>
/// Contains the assignment status type data used to label a signup statistics column, returned
/// by the API.
/// </summary>
/// <param name="Id">Identifier of the target entity.</param>
/// <param name="Name">The name value.</param>
public record EventSignupStatsStatusResponse(Guid Id, string Name);

/// <summary>
/// Contains a single aggregated signup count for a role and status combination, returned by the
/// API.
/// </summary>
/// <param name="ActivityRoleTypeId">Identifier of the activity role type.</param>
/// <param name="AssignmentStatusId">Identifier of the assignment status.</param>
/// <param name="Count">Number of assignments matching the combination.</param>
public record EventSignupStatsCellResponse(
    Guid ActivityRoleTypeId,
    Guid AssignmentStatusId,
    int Count
);

/// <summary>
/// Contains the aggregated signup statistics for a single activity, returned by the API.
/// </summary>
/// <param name="ActivityId">Identifier of the activity.</param>
/// <param name="Title">The title value.</param>
/// <param name="StartsAt">The starts at value.</param>
/// <param name="Cells">The non-zero role and status combinations for the activity.</param>
public record EventSignupStatsActivityResponse(
    Guid ActivityId,
    string Title,
    DateTimeOffset StartsAt,
    IReadOnlyList<EventSignupStatsCellResponse> Cells
);

/// <summary>
/// Contains the aggregated signup totals for an event, returned by the API.
/// </summary>
/// <param name="Total">Total number of assignments across all activities.</param>
/// <param name="Requested">Number of assignments with the requested status.</param>
/// <param name="Confirmed">Number of assignments with the confirmed status.</param>
/// <param name="Denied">Number of assignments with the denied status.</param>
public record EventSignupStatsTotalsResponse(int Total, int Requested, int Confirmed, int Denied);

/// <summary>
/// Contains the aggregated signup statistics for an event, returned by the API. Only aggregated
/// counts and catalogs are exposed: no user identifiers, names or contact details.
/// </summary>
/// <param name="EventId">Identifier of the event.</param>
/// <param name="Roles">The activity role types referenced by the statistics.</param>
/// <param name="Statuses">The assignment status types referenced by the statistics.</param>
/// <param name="Activities">The per-activity aggregated statistics.</param>
/// <param name="Totals">The event-wide aggregated totals.</param>
public record EventSignupStatsResponse(
    Guid EventId,
    IReadOnlyList<EventSignupStatsRoleResponse> Roles,
    IReadOnlyList<EventSignupStatsStatusResponse> Statuses,
    IReadOnlyList<EventSignupStatsActivityResponse> Activities,
    EventSignupStatsTotalsResponse Totals
);
