namespace CodigoActivo.Application.Files;

/// <summary>
/// Represents a file content value used by the application.
/// </summary>
/// <param name="Content">Content stream to store or inspect.</param>
/// <param name="ContentType">The content type value.</param>
/// <param name="FileName">The file name value.</param>
/// <param name="UploadedAt">The uploaded at value.</param>
public sealed record FileContent(
    Stream Content,
    string ContentType,
    string FileName,
    DateTimeOffset UploadedAt
);
