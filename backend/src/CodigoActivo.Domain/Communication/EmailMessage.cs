namespace CodigoActivo.Domain.Communication;

/// <summary>
/// Identifies the supported email kind values.
/// </summary>
public enum EmailKind
{
    /// <summary>
    /// Selects the account verification option.
    /// </summary>
    AccountVerification,
    /// <summary>
    /// Selects the password reset option.
    /// </summary>
    PasswordReset,
    /// <summary>
    /// Selects the activity notification option.
    /// </summary>
    ActivityNotification,
    /// <summary>
    /// Selects the manual option.
    /// </summary>
    Manual,
}

/// <summary>
/// Represents an email attachment value used by the application.
/// </summary>
/// <param name="FileName">The file name value.</param>
/// <param name="ContentType">The content type value.</param>
/// <param name="Content">Content stream to store or inspect.</param>
public sealed record EmailAttachment(string FileName, string ContentType, byte[] Content);

/// <summary>
/// Represents an email inline image value used by the application.
/// </summary>
/// <param name="ContentId">The content identifier value.</param>
/// <param name="FileName">The file name value.</param>
/// <param name="ContentType">The content type value.</param>
/// <param name="Content">Content stream to store or inspect.</param>
public sealed record EmailInlineImage(
    string ContentId,
    string FileName,
    string ContentType,
    byte[] Content
);

/// <summary>
/// Represents an email message value used by the application.
/// </summary>
/// <param name="Kind">Email category whose limits are applied.</param>
/// <param name="ToAddress">The to address value.</param>
/// <param name="ToName">The to name value.</param>
/// <param name="Subject">The subject value.</param>
/// <param name="HtmlBody">The html body value.</param>
/// <param name="TextBody">The text body value.</param>
/// <param name="Attachments">The attachments value.</param>
/// <param name="InlineImages">The inline images value.</param>
public sealed record EmailMessage(
    EmailKind Kind,
    string ToAddress,
    string ToName,
    string Subject,
    string HtmlBody,
    string TextBody,
    IReadOnlyList<EmailAttachment>? Attachments = null,
    IReadOnlyList<EmailInlineImage>? InlineImages = null
);

/// <summary>
/// Contains the data produced for email batch.
/// </summary>
/// <param name="Sent">The sent value.</param>
/// <param name="Failed">The failed value.</param>
public sealed record EmailBatchResult(int Sent, int Failed);
