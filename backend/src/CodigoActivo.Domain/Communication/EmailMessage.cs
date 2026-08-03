namespace CodigoActivo.Domain.Communication;

public enum EmailKind
{
    AccountVerification,
    PasswordReset,
    ActivityNotification,
    Manual,
}

public sealed record EmailAttachment(string FileName, string ContentType, byte[] Content);

public sealed record EmailMessage(
    EmailKind Kind,
    string ToAddress,
    string ToName,
    string Subject,
    string HtmlBody,
    string TextBody,
    IReadOnlyList<EmailAttachment>? Attachments = null
);

public sealed record EmailBatchResult(int Sent, int Failed);
