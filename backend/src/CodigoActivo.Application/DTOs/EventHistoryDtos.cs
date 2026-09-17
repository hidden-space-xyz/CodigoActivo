namespace CodigoActivo.Application.DTOs;

/// <summary>
/// Contains the event history data returned by the API.
/// </summary>
/// <param name="EventId">Identifier of the event.</param>
/// <param name="Title">The title value.</param>
/// <param name="Subtitle">The subtitle value.</param>
/// <param name="EventStartsAt">The event starts at value.</param>
/// <param name="EventEndsAt">The event ends at value.</param>
/// <param name="ThumbnailId">Identifier of the thumbnail.</param>
/// <param name="IsPast">Whether past.</param>
/// <param name="CanRate">Whether can rate.</param>
/// <param name="HasRated">Whether the signed-in user already submitted a rating for this event.</param>
/// <param name="Activities">The activities value.</param>
public record EventHistoryResponse(
    Guid EventId,
    string Title,
    string Subtitle,
    DateOnly EventStartsAt,
    DateOnly EventEndsAt,
    Guid ThumbnailId,
    bool IsPast,
    bool CanRate,
    bool HasRated,
    IReadOnlyList<EventHistoryActivityResponse> Activities
);

/// <summary>
/// Contains the event history activity data returned by the API.
/// </summary>
/// <param name="ActivityId">Identifier of the activity.</param>
/// <param name="Title">The title value.</param>
/// <param name="Location">The location value.</param>
/// <param name="ModalityName">The modality name value.</param>
/// <param name="UserId">Identifier of the user.</param>
/// <param name="FirstName">User's given name.</param>
/// <param name="LastName">User's family name.</param>
/// <param name="IsSelf">Whether self.</param>
/// <param name="RoleTypeId">Identifier of the role type.</param>
/// <param name="RoleTypeName">The role type name value.</param>
/// <param name="StatusId">Identifier of the status.</param>
/// <param name="StatusName">The status name value.</param>
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
