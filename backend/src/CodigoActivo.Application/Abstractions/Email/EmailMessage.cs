namespace CodigoActivo.Application.Abstractions.Email;

/// <summary>
/// Names the kind of an outbound email, which sets whether the email guard limits it, its delivery
/// priority and how long it is worth delivering.
/// </summary>
public enum EmailKind
{
    /// <summary>
    /// Selects the option for the mail a registration or an email change sends to the address it
    /// names: the link that verifies the address, or the notice that it already has an account.
    /// </summary>
    AccountVerification,

    /// <summary>
    /// Selects the password reset option, the email that recovers access to an account.
    /// </summary>
    AccessRecovery,

    /// <summary>
    /// Selects the one-time code of the second factor: the login code and the codes that confirm
    /// deleting an account or opening a consultation of the blocked data.
    /// </summary>
    TwoFactorCode,

    /// <summary>
    /// Selects the notice that tells a participant, or their guardian, how an activity signup was
    /// decided.
    /// </summary>
    ActivityNotification,

    /// <summary>
    /// Selects the notice that tells the holder of an account about a security change to it, such
    /// as a new password or a lock after wrong passwords.
    /// </summary>
    SecurityAlert,

    /// <summary>
    /// Selects the email an administrator writes, sent in bulk to the recipients they choose.
    /// </summary>
    Manual,

    /// <summary>
    /// Selects the notice that tells a guardian that a dependent will be erased the day it comes of
    /// age.
    /// </summary>
    ComingOfAgeNotice,

    /// <summary>
    /// Selects the notice that tells the association a consent was withdrawn.
    /// </summary>
    ConsentWithdrawalNotice,

    /// <summary>
    /// Selects the notice that tells the association a consultation of the blocked data was
    /// opened.
    /// </summary>
    BlockedDataConsultationNotice,
}

/// <summary>
/// File attached to an email.
/// </summary>
/// <param name="FileName">Name the file is attached under.</param>
/// <param name="ContentType">
/// MIME type of the file; an unparseable or composite type is sent as
/// <c>application/octet-stream</c>.
/// </param>
/// <param name="Content">Bytes of the file.</param>
public sealed record EmailAttachment(string FileName, string ContentType, byte[] Content);

/// <summary>
/// Image embedded in the HTML body of an email, which shows it through a <c>cid:</c> reference.
/// </summary>
/// <param name="ContentId">Content identifier the HTML body references after <c>cid:</c>.</param>
/// <param name="FileName">Name of the image file.</param>
/// <param name="ContentType">
/// MIME type of the image; an unparseable or composite type is sent as
/// <c>application/octet-stream</c>.
/// </param>
/// <param name="Content">Bytes of the image.</param>
public sealed record EmailInlineImage(
    string ContentId,
    string FileName,
    string ContentType,
    byte[] Content
);

/// <summary>
/// Email to one addressee.
/// </summary>
/// <param name="Kind">Email category whose limits are applied.</param>
/// <param name="ToAddress">Address of the addressee.</param>
/// <param name="ToName">Display name of the addressee.</param>
/// <param name="Subject">Subject line.</param>
/// <param name="HtmlBody">HTML body.</param>
/// <param name="TextBody">Plain-text alternative of the HTML body.</param>
/// <param name="Attachments">Files attached, if any.</param>
/// <param name="InlineImages">Images the HTML body embeds, if any.</param>
/// <param name="OneClickUnsubscribeUrl">
/// Address to which the mail client of the addressee posts to withdraw their news
/// consent, sent in the <c>List-Unsubscribe</c> header (RFC 8058); <see langword="null"/> for
/// email that is not news.
/// </param>
public sealed record EmailMessage(
    EmailKind Kind,
    string ToAddress,
    string ToName,
    string Subject,
    string HtmlBody,
    string TextBody,
    IReadOnlyList<EmailAttachment>? Attachments = null,
    IReadOnlyList<EmailInlineImage>? InlineImages = null,
    string? OneClickUnsubscribeUrl = null
)
{
    /// <summary>
    /// Gets what the message is about, from <see cref="EmailTopics"/>: a new message on the same
    /// topic to the same address replaces the one still waiting, so a repeated action never piles
    /// up mail. <see langword="null"/> keeps every message.
    /// </summary>
    public string? Topic { get; init; }

    /// <summary>
    /// Gets how long the message is worth delivering, such as the lifetime of the code it carries;
    /// <see langword="null"/> leaves it to the kind.
    /// </summary>
    public TimeSpan? ValidFor { get; init; }
}

/// <summary>
/// Represents one addressee of an email batch.
/// </summary>
/// <param name="Address">Address of the addressee.</param>
/// <param name="Name">Display name of the addressee.</param>
/// <param name="UnsubscribeUrl">
/// Link with which this addressee withdraws their news consent, shown in the place the
/// shared content of a news email leaves for it; <see langword="null"/> for other email.
/// </param>
/// <param name="OneClickUnsubscribeUrl">
/// Address to which the mail client of this addressee posts to withdraw their news
/// consent, sent in the <c>List-Unsubscribe</c> header of a news email (RFC 8058);
/// <see langword="null"/> for other email.
/// </param>
public sealed record EmailRecipient(
    string Address,
    string Name,
    string? UnsubscribeUrl = null,
    string? OneClickUnsubscribeUrl = null
);

/// <summary>
/// Represents one email written once and delivered to every listed recipient, so the shared
/// subject, bodies and binary content are stored once instead of once per addressee.
/// </summary>
/// <param name="Kind">Email category whose limits are applied.</param>
/// <param name="Subject">Subject line shared by every recipient.</param>
/// <param name="HtmlBody">HTML body shared by every recipient.</param>
/// <param name="TextBody">Plain-text alternative of the HTML body.</param>
/// <param name="Recipients">Addressees, each stored as a pending message of its own.</param>
/// <param name="Attachments">Files attached, if any.</param>
/// <param name="InlineImages">Images the HTML body embeds, if any.</param>
public sealed record EmailBatch(
    EmailKind Kind,
    string Subject,
    string HtmlBody,
    string TextBody,
    IReadOnlyList<EmailRecipient> Recipients,
    IReadOnlyList<EmailAttachment>? Attachments = null,
    IReadOnlyList<EmailInlineImage>? InlineImages = null
)
{
    /// <summary>
    /// Gets what every message of the batch is about; see <see cref="EmailMessage.Topic"/>.
    /// </summary>
    public string? Topic { get; init; }

    /// <summary>
    /// Gets how long the batch is worth delivering; see <see cref="EmailMessage.ValidFor"/>.
    /// </summary>
    public TimeSpan? ValidFor { get; init; }

    /// <summary>
    /// Wraps a single message as a batch with one recipient.
    /// </summary>
    /// <param name="message">Email message to deliver.</param>
    /// <returns>
    /// The batch, with the topic and validity of the message; its one-click unsubscribe address is
    /// not carried over.
    /// </returns>
    public static EmailBatch ForOne(EmailMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return new EmailBatch(
            message.Kind,
            message.Subject,
            message.HtmlBody,
            message.TextBody,
            [new EmailRecipient(message.ToAddress, message.ToName)],
            message.Attachments,
            message.InlineImages
        )
        {
            Topic = message.Topic,
            ValidFor = message.ValidFor,
        };
    }
}
