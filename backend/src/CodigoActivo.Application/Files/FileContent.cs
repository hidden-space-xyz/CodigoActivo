namespace CodigoActivo.Application.Files;

/// <summary>
/// Stored content of one variant of a file, ready to be served.
/// </summary>
/// <param name="Content">Stream open on the stored content.</param>
/// <param name="ContentType">
/// Media type of the stored format, or <c>application/octet-stream</c> for an unknown extension.
/// </param>
/// <param name="FileName">Sanitized name the file was uploaded with.</param>
/// <param name="UploadedAt">UTC timestamp of the upload.</param>
public sealed record FileContent(
    Stream Content,
    string ContentType,
    string FileName,
    DateTimeOffset UploadedAt
);
