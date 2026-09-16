namespace CodigoActivo.Application.Files;

/// <summary>
/// Represents a file upload value used by the application.
/// </summary>
/// <param name="Content">Content stream to store or inspect.</param>
/// <param name="FileName">The file name value.</param>
/// <param name="Length">The length value.</param>
public sealed record FileUpload(Stream Content, string FileName, long Length);
