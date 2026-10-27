namespace CodigoActivo.Application.Participation.Contracts;

/// <summary>
/// Contains the attendance certificate of a person for an event, returned by the API.
/// </summary>
/// <param name="Code">
/// Reference printed on the certificate, <c>CA-{year}-{8 hex digits}</c>, derived from the event,
/// the person and the year of the last day, so it is the same every time.
/// </param>
/// <param name="EventId">Identifier of the event.</param>
/// <param name="UserId">Identifier of the person who attended.</param>
/// <param name="FirstName">Person's given name.</param>
/// <param name="LastName">Person's family name.</param>
/// <param name="IsSelf">Whether the person is the signed-in user rather than a dependent.</param>
/// <param name="EventTitle">Title of the event.</param>
/// <param name="EventSubtitle">Subtitle of the event.</param>
/// <param name="EventStartsAt">First day of the event.</param>
/// <param name="EventEndsAt">Last day of the event.</param>
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
