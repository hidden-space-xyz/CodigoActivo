using CodigoActivo.Application.Abstractions.Email;

namespace CodigoActivo.Application.Emails;

/// <summary>
/// Composes the batch of an email written by an administrator.
/// </summary>
public interface IManualEmailComposer
{
    /// <summary>
    /// Renders the subject and body into the site layout and addresses the batch.
    /// </summary>
    /// <param name="subject">Subject written by the administrator.</param>
    /// <param name="body">Plain-text body written by the administrator; blank lines separate paragraphs.</param>
    /// <param name="recipients">Recipients of the batch.</param>
    /// <param name="attachments">Files attached to every message.</param>
    /// <returns>The composed batch.</returns>
    public EmailBatch Compose(
        string subject,
        string body,
        IReadOnlyList<EmailRecipient> recipients,
        IReadOnlyList<EmailAttachment> attachments
    );
}
