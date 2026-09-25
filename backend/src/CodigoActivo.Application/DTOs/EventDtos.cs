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
public record EventTermsDocumentRequest([Required] Guid TermsDocumentId, bool Required = false);

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
/// Contains an activity that the requesting user leads with a confirmed assignment, with the
/// attendees currently confirmed in it grouped by role, returned by the API.
/// </summary>
/// <param name="ActivityId">Identifier of the activity.</param>
/// <param name="Title">The title value.</param>
/// <param name="Location">Concrete place or platform where the activity takes place.</param>
/// <param name="ActivityStartsAt">The activity starts at value.</param>
/// <param name="ActivityEndsAt">The activity ends at value.</param>
/// <param name="Roles">Confirmed attendees grouped by role: leaders, volunteers, then participants.</param>
public record LeaderRosterActivityResponse(
    Guid ActivityId,
    string Title,
    string Location,
    DateTimeOffset ActivityStartsAt,
    DateTimeOffset ActivityEndsAt,
    IReadOnlyList<LeaderRosterRoleResponse> Roles
);

/// <summary>
/// Contains the confirmed attendees of one role in a led activity, split between independent
/// accounts and dependents because each kind carries different data, returned by the API.
/// </summary>
/// <param name="RoleTypeId">Identifier of the role type.</param>
/// <param name="RoleName">The role name value.</param>
/// <param name="Users">Attendees with an account of their own, reachable directly.</param>
/// <param name="Dependents">Minors signed up by their guardian, reachable through the guardian.</param>
public record LeaderRosterRoleResponse(
    Guid RoleTypeId,
    string RoleName,
    IReadOnlyList<LeaderRosterUserResponse> Users,
    IReadOnlyList<LeaderRosterDependentResponse> Dependents
);

/// <summary>
/// Contains what an activity leader may see about a confirmed attendee with an account of their
/// own, returned by the API.
/// </summary>
/// <param name="FirstName">Attendee's given name.</param>
/// <param name="LastName">Attendee's family name.</param>
/// <param name="Email">Attendee's email address.</param>
/// <param name="Phone">Attendee's primary phone.</param>
/// <param name="SignedUpAt">UTC timestamp when the attendee signed up for the activity.</param>
public record LeaderRosterUserResponse(
    string FirstName,
    string LastName,
    string? Email,
    string? Phone,
    DateTimeOffset SignedUpAt
);

/// <summary>
/// Contains what an activity leader may see about a confirmed dependent attendee, returned by the
/// API. The age is computed on the server for the day of the activity, so it neither reveals the
/// birth date nor changes while the list is available.
/// </summary>
/// <param name="FirstName">Dependent's given name.</param>
/// <param name="LastName">Dependent's family name.</param>
/// <param name="Age">Age in years on the local day the activity starts; <see langword="null"/> when no birth date is stored.</param>
/// <param name="Guardian">Guardian to contact about the dependent.</param>
/// <param name="SignedUpAt">UTC timestamp when the dependent was signed up for the activity.</param>
public record LeaderRosterDependentResponse(
    string FirstName,
    string LastName,
    int? Age,
    LeaderRosterGuardianResponse Guardian,
    DateTimeOffset SignedUpAt
);

/// <summary>
/// Contains the contact details of a dependent attendee's guardian shown to the activity leader,
/// returned by the API.
/// </summary>
/// <param name="FirstName">Guardian's given name.</param>
/// <param name="LastName">Guardian's family name.</param>
/// <param name="Email">Guardian's email address.</param>
/// <param name="Phone">Guardian's primary phone.</param>
public record LeaderRosterGuardianResponse(
    string FirstName,
    string LastName,
    string? Email,
    string? Phone
);
