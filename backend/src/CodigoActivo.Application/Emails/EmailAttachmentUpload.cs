namespace CodigoActivo.Application.Emails;

/// <summary>
/// A file an administrator attaches to a manual email, as uploaded.
/// </summary>
/// <param name="Content">Stream the file is read from; exactly <c>Length</c> bytes are read.</param>
/// <param name="FileName">Name given by the client, sanitized before it is attached.</param>
/// <param name="ContentType">Media type given by the client.</param>
/// <param name="Length">
/// Size in bytes; it must be positive and counts toward
/// <see cref="ManualEmailOptions.MaxAttachmentsBytes"/>.
/// </param>
public sealed record EmailAttachmentUpload(
    Stream Content,
    string FileName,
    string ContentType,
    long Length
);
