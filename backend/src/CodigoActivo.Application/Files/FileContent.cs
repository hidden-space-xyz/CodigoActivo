namespace CodigoActivo.Application.Files;

public sealed record FileContent(
    Stream Content,
    string ContentType,
    string FileName,
    DateTimeOffset UploadedAt
);
