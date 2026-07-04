namespace CodigoActivo.Application.DTOs;

public record FileResponse(
    Guid Id,
    string Name,
    string Extension,
    DateTimeOffset UploadedAt,
    Guid UploadedBy
);

public sealed record FileUploadRequest(Stream Content, string FileName, long Length);

public sealed record FileContentValueObject(
    Stream Content,
    string ContentType,
    string FileName,
    DateTimeOffset UploadedAt
);