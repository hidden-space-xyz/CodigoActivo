namespace CodigoActivo.Application.DTOs;

/// <summary>
/// Contains the event certificate data returned by the API.
/// </summary>
/// <param name="Code">The code value.</param>
/// <param name="EventId">Identifier of the event.</param>
/// <param name="UserId">Identifier of the user.</param>
/// <param name="FirstName">User's given name.</param>
/// <param name="LastName">User's family name.</param>
/// <param name="IsSelf">Whether self.</param>
/// <param name="EventTitle">The event title value.</param>
/// <param name="EventSubtitle">The event subtitle value.</param>
/// <param name="EventStartsAt">The event starts at value.</param>
/// <param name="EventEndsAt">The event ends at value.</param>
public record EventCertificateResponse(
    string Code,
    Guid EventId,
    Guid UserId,
    string FirstName,
    string LastName,
    bool IsSelf,
    string EventTitle,
    string EventSubtitle,
    DateOnly EventStartsAt,
    DateOnly EventEndsAt
);
