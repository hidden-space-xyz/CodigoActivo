namespace CodigoActivo.Application.DTOs;

public record EventHistoryResponse(
    Guid EventId,
    string Title,
    string Subtitle,
    DateOnly EventStartsAt,
    DateOnly EventEndsAt,
    Guid ThumbnailId,
    bool IsPast,
    bool CanRate,
    EventRatingResponse? MyRating,
    IReadOnlyList<EventHistoryActivityResponse> Activities
);

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
