using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Emails;

/// <summary>
/// Represents one addressee of a manual email.
/// </summary>
/// <param name="UserId">Account the message is written to, which its unsubscribe link names.</param>
/// <param name="Address">Email address of the account.</param>
/// <param name="Name">First name of the account.</param>
public sealed record ManualEmailRecipient(UserId UserId, string Address, string Name);

/// <summary>
/// Composes the batch of an email written by an administrator.
/// </summary>
public interface IManualEmailComposer
{
    /// <summary>
    /// Renders the subject and body into the site layout and addresses the batch. A promotional
    /// email also carries the contact address and, for each recipient, their own unsubscribe link.
    /// </summary>
    /// <param name="subject">Subject written by the administrator.</param>
    /// <param name="body">Plain-text body written by the administrator; blank lines separate paragraphs.</param>
    /// <param name="purpose">What the email is written for.</param>
    /// <param name="recipients">Recipients of the batch.</param>
    /// <param name="attachments">Files attached to every message.</param>
    /// <returns>The composed batch.</returns>
    public EmailBatch Compose(
        string subject,
        string body,
        ManualEmailPurpose purpose,
        IReadOnlyList<ManualEmailRecipient> recipients,
        IReadOnlyList<EmailAttachment> attachments
    );
}
