namespace CodigoActivo.Application.DTOs;

public record FileResponse(
    Guid Id,
    string Name,
    string Extension,
    DateTimeOffset UploadedAt,
    Guid UploadedBy
);
