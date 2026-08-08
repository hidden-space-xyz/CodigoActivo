namespace CodigoActivo.Application.DTOs;

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
