namespace CodigoActivo.Application.Files;

/// <summary>
/// An image as the client uploaded it, before it is validated and processed.
/// </summary>
/// <param name="Content">Content of the upload; it must be seekable.</param>
/// <param name="FileName">Name the client gave the file, sanitized before it is stored.</param>
/// <param name="Length">Size of the upload in bytes.</param>
public sealed record FileUpload(Stream Content, string FileName, long Length);
