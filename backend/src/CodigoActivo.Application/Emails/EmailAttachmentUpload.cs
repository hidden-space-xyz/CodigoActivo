namespace CodigoActivo.Application.Emails;

public sealed record EmailAttachmentUpload(
    Stream Content,
    string FileName,
    string ContentType,
    long Length
);
