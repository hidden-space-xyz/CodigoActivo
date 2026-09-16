namespace CodigoActivo.Application.Emails;

/// <summary>
/// Represents an email attachment upload value used by the application.
/// </summary>
/// <param name="Content">Content stream to store or inspect.</param>
/// <param name="FileName">The file name value.</param>
/// <param name="ContentType">The content type value.</param>
/// <param name="Length">The length value.</param>
public sealed record EmailAttachmentUpload(
    Stream Content,
    string FileName,
    string ContentType,
    long Length
);
