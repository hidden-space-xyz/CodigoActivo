namespace CodigoActivo.Application.Participation.Contracts;

/// <summary>
/// Contains an event in the participation history of the signed-in user's household, returned by
/// the API.
/// </summary>
/// <param name="EventId">Identifier of the event.</param>
/// <param name="Title">Title of the event.</param>
/// <param name="Subtitle">Subtitle of the event.</param>
/// <param name="EventStartsAt">First day of the event.</param>
/// <param name="EventEndsAt">Last day of the event.</param>
/// <param name="ThumbnailId">Identifier of the thumbnail file of the event.</param>
/// <param name="IsPast">Whether the last day of the event has passed.</param>
/// <param name="CanRate">
/// Whether the signed-in account may rate the event: it has ended and someone in the household had
/// a confirmed place.
/// </param>
/// <param name="Rating">The signed-in account's rating of the event, if it gave one.</param>
/// <param name="Activities">
/// Signups of the household to its activities: every one for an upcoming event, only the confirmed
/// ones for a past event.
/// </param>
public record EventHistoryResponse(
    Guid EventId,
    string Title,
    string Subtitle,
    DateOnly EventStartsAt,
    DateOnly EventEndsAt,
    Guid ThumbnailId,
    bool IsPast,
    bool CanRate,
    EventHistoryRatingResponse? Rating,
    IReadOnlyList<EventHistoryActivityResponse> Activities
);

/// <summary>
/// Contains the signed-in account's own rating of an event in its history.
/// </summary>
/// <param name="Score">Score from 1 to 5, or <see langword="null"/> when the account gave none.</param>
/// <param name="MostLiked">
/// What the account liked most, or <see langword="null"/> without an answer.
/// </param>
/// <param name="LeastLiked">
/// What the account liked least, or <see langword="null"/> without an answer.
/// </param>
/// <param name="Suggestions">
/// Suggestions of the account, or <see langword="null"/> without any.
/// </param>
public record EventHistoryRatingResponse(
    int? Score,
    string? MostLiked,
    string? LeastLiked,
    string? Suggestions
);

/// <summary>
/// Contains one signup of a household member in the participation history, returned by the API.
/// </summary>
/// <param name="ActivityId">Identifier of the activity.</param>
/// <param name="Title">Title of the activity.</param>
/// <param name="Location">Where the activity takes place.</param>
/// <param name="ModalityName">Display name of how people attend it.</param>
/// <param name="UserId">Identifier of the person signed up.</param>
/// <param name="FirstName">Person's given name.</param>
/// <param name="LastName">Person's family name.</param>
/// <param name="IsSelf">Whether the person is the signed-in user rather than a dependent.</param>
/// <param name="RoleTypeId">Catalog identifier of the role taken.</param>
/// <param name="RoleTypeName">Display name of the role.</param>
/// <param name="StatusId">Catalog identifier of the review status.</param>
/// <param name="StatusName">Display name of the status.</param>
public record EventHistoryActivityResponse(
    Guid ActivityId,
    string Title,
    string Location,
    string ModalityName,
    Guid UserId,
    string FirstName,
    string LastName,
    bool IsSelf,
    Guid RoleTypeId,
    string RoleTypeName,
    Guid StatusId,
    string StatusName
);
